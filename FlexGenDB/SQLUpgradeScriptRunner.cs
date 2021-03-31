using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

namespace FlexGenDB
{
    public static class SQLUpgradeScriptRunner
    {
        private static string Database;
        private static string UpgradeScriptDirectory;
        private static string UpgradeScriptGuidMonikerPrefix;
        private static List<string> NewUpgradeScripts;
        private static Regex UpgradeScriptFilterRegex;
        private static string UseDatabase;

        private static readonly string UpgradeScriptRunTableName = "UpgradeScriptRun";
        private static readonly string UpgradeScriptRunErrorTableName = "UpgradeScriptRunError";
        private static readonly DateTime Epoch = Convert.ToDateTime("1/1/1900");
        private static readonly Random random = new Random();
        private static readonly Regex CorrectFileNameRegex = new Regex("^[0-9]{1,}_.*");

        public static void Run()
        {
            Logger.Log("Starting SQL Upgrade Script Runner");
            LoadConfiguration();
            CheckDatabase();
            CheckSchemaForUpgradeTables();
            RenameScriptFiles();
            GetNewUpgradeScripts();
            AssignGUIDsToUpgradeScripts();
            RunNewDatabaseUpgradeScripts();
        }


        private static void LoadConfiguration()
        {
            Logger.Log("Loading session configuration");
            Database = SessionConfiguration.Database;
            UseDatabase = $"USE [{Database}]";
            Logger.LogVerbose($"Database: {Database}");
            UpgradeScriptDirectory = SessionConfiguration.UpgradeScriptDirectory;
            Logger.LogVerbose($"Source directory: {UpgradeScriptDirectory}");
            UpgradeScriptFilterRegex = new Regex(SessionConfiguration.UpgradeScriptFileRegex);
            Logger.LogVerbose($"Upgrade file regex: {SessionConfiguration.UpgradeScriptFileRegex}");
            UpgradeScriptGuidMonikerPrefix = SessionConfiguration.UpgradeScriptTemplateGuidMonikerPrefix;
            Logger.LogVerbose($"Upgrade script guid moniker prerfix: {UpgradeScriptGuidMonikerPrefix}");
        }


        private static void CheckDatabase()
        {
            string databaseQuery =
                $"SELECT COUNT(*) FROM sys.databases WHERE [name] = '{Database}'";

            var queryResults = SQLInterface.ExecuteQueryIntoDataTable(databaseQuery);
            int count = (int)queryResults.Rows[0].ItemArray[0];
            if (count == 0)
            {
                string createDatabase = $"CREATE DATABASE [{Database}]";
                SQLInterface.ExecuteNonQuery(createDatabase);
            }
        }


        private static void CheckSchemaForUpgradeTables()
        {
            Logger.Log("Checking for UpgradeScript tables in database");

            if(!TableExists(UpgradeScriptRunTableName))
            {
                Logger.Log($"Creating table {UpgradeScriptRunTableName}");
                string createTable =
                    $"{UseDatabase} " +
                    $"CREATE TABLE {UpgradeScriptRunTableName} ( " +
                    $"{UpgradeScriptRunTableName}Id INTEGER NOT NULL UNIQUE IDENTITY(1, 1), " +
                    "ScriptFile VARCHAR(255) NOT NULL, " +
                    "ExecutedTime DATETIME NOT NULL DEFAULT GETUTCDATE(), " +
                    "IsSuccessful BIT NOT NULL DEFAULT 0 " +
                    "PRIMARY KEY (UpgradeScriptRunId) )";
                SQLInterface.ExecuteNonQuery(createTable);
                Logger.LogVerbose($"Table {UpgradeScriptRunTableName} created");
            }

            if (!TableExists(UpgradeScriptRunErrorTableName))
            {
                Logger.Log($"Creating table {UpgradeScriptRunErrorTableName}");
                string createTable =
                    $"{UseDatabase} " +
                    $"CREATE TABLE {UpgradeScriptRunErrorTableName} ( " +
                    $"{UpgradeScriptRunErrorTableName}Id INTEGER NOT NULL UNIQUE IDENTITY(1, 1), " +
                    $"{UpgradeScriptRunTableName}Id INTEGER NOT NULL, " +
                    "ErrorMessage NTEXT, " +
                    $"PRIMARY KEY ({UpgradeScriptRunErrorTableName}Id), " +
                    $"FOREIGN KEY ({UpgradeScriptRunTableName}Id) REFERENCES {UpgradeScriptRunTableName}({UpgradeScriptRunTableName}Id) )";
                SQLInterface.ExecuteNonQuery(createTable);
                Logger.LogVerbose($"Table {UpgradeScriptRunErrorTableName} created");
            }
        }


        private static void RenameScriptFiles()
        {
            Logger.Log("Renaming new upgrade scripts");

            Logger.LogVerbose($"Checking for source directory {UpgradeScriptDirectory}");
            if (!Directory.Exists(UpgradeScriptDirectory))
            {
                Logger.LogVerbose("Directory does not exist and will be created");
                Directory.CreateDirectory(UpgradeScriptDirectory);
                return;
            }
            Logger.LogVerbose("Directory found");

            Logger.LogVerbose("Getting new files that do not match expected naming convention");
            string[] newFiles = Directory.GetFiles(UpgradeScriptDirectory)
                .Where(e => UpgradeScriptFilterRegex.IsMatch(Path.GetFileName(e))
                    && !CorrectFileNameRegex.IsMatch(Path.GetFileName(e)))
                .ToArray();
            Logger.LogVerbose($"{newFiles.Length} new files found");
            int scriptIndex = 0;
            try
            {
                scriptIndex = Directory.GetFiles(UpgradeScriptDirectory)
                    .Where(e => CorrectFileNameRegex.IsMatch(Path.GetFileName(e)))
                    .Select(e => int.Parse(Path.GetFileName(e).Split('_')[0]))
                    .Max();
            }
            catch { }

            for (int i = 0; i < newFiles.Length; i++)
            {
                string sourceFilePath = newFiles[i];
                string sourceFileName = Path.GetFileName(sourceFilePath);
                string resultFileName = $"{++scriptIndex}_{sourceFileName}";
                string resultFilePath = $"{UpgradeScriptDirectory}/{resultFileName}";
                Logger.LogVerbose($"Renaming {sourceFileName} to {resultFileName}");
                File.Copy(sourceFilePath, resultFilePath);
                Logger.LogVerbose($"Deleting {sourceFilePath}");
                File.Delete(sourceFilePath);
            }
        }


        private static void GetNewUpgradeScripts()
        {
            Logger.Log("Determining upgrade scripts to run");

            NewUpgradeScripts = Directory.GetFiles(UpgradeScriptDirectory)
                .Where(e => CorrectFileNameRegex.IsMatch(Path.GetFileName(e))
                    && UpgradeScriptFilterRegex.IsMatch(Path.GetFileName(e)))
                .OrderBy(e => Path.GetFileName(e).Split('_')[0])
                .Select(e => Path.GetFileName(e))
                .ToList();
            Logger.LogVerbose($"{NewUpgradeScripts.Count} valid scripts found in source directory");

            string successfulScriptsQuery = $"SELECT DISTINCT ScriptFile FROM {UpgradeScriptRunTableName} WHERE IsSuccessful = 1";
            var queryResults = SQLInterface.ExecuteQueryIntoDataTable(successfulScriptsQuery);
            Logger.LogVerbose($"{queryResults.Rows.Count} successful scripts found in database records");
            foreach(DataRow row in queryResults.Rows)
            {
                string upgradeScript = row.ItemArray[0].ToString();
                Logger.LogVerbose($"Removing script {upgradeScript} from list");
                if(NewUpgradeScripts.Contains(upgradeScript))
                {
                    NewUpgradeScripts.Remove(upgradeScript);
                    Logger.LogVerbose("Script removed");
                }
                else
                {
                    Logger.LogVerbose("Script does not exist in directory");
                }
            }

            Logger.LogVerbose($"{NewUpgradeScripts.Count} new scripts found in directory");
            NewUpgradeScripts = NewUpgradeScripts
                .Select(e => Path.Combine(UpgradeScriptDirectory, e))
                .ToList();
        }


        private static void AssignGUIDsToUpgradeScripts()
        {
            foreach(string file in NewUpgradeScripts)
            {
                string fileContents = File.ReadAllText(file);

                string buffer;
                string newFileContent = "";
                var savedPrimaryKeys = new Dictionary<string, string>(10);

                for(int i = 0; i < fileContents.Length; i++)
                {
                    string c = fileContents[i].ToString();
                    if(c == "{")
                    {
                        buffer = c;
                        do
                        {
                            buffer += fileContents[++i];
                        }
                        while (fileContents[i] != '}' && i < fileContents.Length);

                        if(buffer.StartsWith("{" + UpgradeScriptGuidMonikerPrefix, StringComparison.InvariantCultureIgnoreCase))
                        {
                            if(buffer.Equals($"{{{UpgradeScriptGuidMonikerPrefix}}}"))
                            {
                                newFileContent += NewGUID();
                            }
                            else
                            {
                                buffer = buffer.ToLower();
                                if (!savedPrimaryKeys.ContainsKey(buffer))
                                    savedPrimaryKeys[buffer] = NewGUID();

                                newFileContent += savedPrimaryKeys[buffer];
                            }
                        }
                        else
                        {
                            newFileContent += buffer;
                        }
                    }
                    else
                    {
                        newFileContent += c;
                    }
                }
                File.WriteAllText(file, newFileContent);
            }
        }


        private static void RunNewDatabaseUpgradeScripts()
        {
            if(NewUpgradeScripts.Count == 0)
            {
                Logger.Log("No new upgrade scripts to run");
                return;
            }

            SQLInterface.ExecuteNonQuery(UseDatabase);

            string fileContent;
            string[] statements;
            var goSplit = new Regex("\r\n[ \t]{0,}GO([ \t]{0,}\r\n{0,}){1,}");

            foreach(string file in NewUpgradeScripts)
            {
                Logger.Log($"Executing script {file}");
                fileContent = $"{UseDatabase}\r\n GO\r\n {File.ReadAllText(file)}";
                statements = goSplit.Split(fileContent);
                try
                {
                    foreach (string statement in statements)
                    {
                        if(!string.IsNullOrEmpty(statement))
                            SQLInterface.ExecuteNonQuery(statement);
                    }

                    LogUpgradeScriptRun(file);
                }
                catch (Exception e)
                {
                    Logger.Log($"Error when running script: {e.Message}");
                    LogUpgradeScriptRun(file, e);
                }
            }
        }

        #region Helper Methods

        private static bool TableExists(string tableName)
        {
            Logger.LogVerbose($"Checking for table {tableName} in database");
            SQLInterface.ExecuteNonQuery(UseDatabase);
            string tableQuery = $"SELECT COUNT(*) FROM sys.tables WHERE [name] = '{tableName}'";
            var queryResult = SQLInterface.ExecuteQueryIntoDataTable(tableQuery);
            bool result = (int)queryResult.Rows[0].ItemArray[0] == 1;
            if (result)
            {
                Logger.LogVerbose($"Table {tableName} found");
            }
            else
            {
                Logger.LogVerbose($"Table {tableName} not found");
            }
            return result;
        }


        private static string NewGUID()
        {
            var createdDate = DateTime.UtcNow;
            string idSuffix = random.Next(0, 100000000).ToString();
            double totalDays = (createdDate - Epoch).TotalDays;
            string createdDateString = string.Format("{0:0.00000000}", totalDays).Replace(".", "");
            return string.Concat(createdDateString, idSuffix);
        }


        private static void LogUpgradeScriptRun(string filePath, Exception exception = null)
        {
            Logger.LogVerbose($"Logging upgrade script for script {filePath}");
            int isSuccessful = (exception == null ? 1 : 0);
            filePath = Path.GetFileName(filePath);

            string query =
                $"INSERT INTO UpgradeScriptRun (ScriptFile, IsSuccessful) VALUES " +
                $"('{filePath}', {isSuccessful})";
            SQLInterface.ExecuteNonQuery(query);
            if (isSuccessful == 0)
            {
                string message = exception.Message.Replace("'", "''");
                query =
                    "INSERT INTO UpgradeScriptRunError (UpgradeScriptRunId, ErrorMessage) " +
                    $"SELECT TOP 1 UpgradeScriptRunId, '{message}' FROM UpgradeScriptRun WHERE ScriptFile = '{filePath}' " +
                    "ORDER BY ExecutedTime DESC";
                SQLInterface.ExecuteNonQuery(query);
            }
        }

        #endregion
    }
}
