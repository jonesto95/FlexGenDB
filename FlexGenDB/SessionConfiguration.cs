using System;
using System.Data;
using System.Linq;

namespace FlexGenDB
{
    public static class SessionConfiguration
    {
        // Upgrade script configuration
        public static bool RunUpgradeScripts { get; private set; }
        public static string UpgradeScriptDirectory { get; private set; }
        public static string UpgradeScriptTemplateGuidMonikerPrefix { get; private set; }
        public static string UpgradeScriptFileRegex { get; private set; }

        // Entity code generation configuration
        public static bool RunEntityCodeGeneration { get; private set; }
        public static string TemplateDirectory { get; private set; }
        public static string EntityCodeOutputDirectory { get; private set; }
        public static string[] DatabaseSchemas { get; private set; }
        
        // Code build configuration
        public static bool RunCodeBuild { get; private set; }
        public static string ProjectName { get; private set; }

        public static bool DeleteProjectFile { get; private set; }

        // Logging configuration
        public static bool DoLogging { get; private set; }
        public static bool VerboseLogging { get; private set; }
        public static string LogFile { get; private set; }
        public static string LogMessagePrefix { get; private set; }

        // Other/Shared configuration
        public static string ConnectionString { get; private set; }
        public static string Database { get; private set; }


        private const string HelpSwitch = "-help";
        private const string ConfigFileSwitch = "-config";

        private const string UpgradeScriptSwitch = "-runscript";
        private const string UpgradeScriptDirectorySwitch = "-scrdir";
        private const string UpgradeScriptTemplateGuidMonikerSwitch = "-guidprefix";
        private const string UpgradeScriptFileRegexSwitch = "-regex";

        private const string EntityCodeGenerationSwitch = "-gencode";
        private const string TemplateDirectorySwitch = "-templates";
        private const string DatabaseSchemaListSwitch = "-schemas";
        private const string CodeGenOutputSwitch = "-codeout";

        private const string DatabaseSwitch = "-database";
        private const string DatabaseConnectionStringSwitch = "-connstring";

        private const string CodeBuildSwitch = "-buildcode";
        private const string ProjectNameSwitch = "-projname";
        private const string KeepProjectFileSwitch = "-keepprojfile";

        private const string LogFileSwitch = "-logfile";
        private const string DebugSwitch = "-debug";
        private const string VerboseLoggingSwitch = "-verbose";
        private const string LogMessagePrefixSwitch = "-logprefix";


        private static void SetDefaultConfiguration()
        {
            RunUpgradeScripts = false;
            UpgradeScriptDirectory = "./UpgradeScripts";
            UpgradeScriptTemplateGuidMonikerPrefix = "GUID";
            UpgradeScriptFileRegex = @".*\.sql";

            RunEntityCodeGeneration = false;
            TemplateDirectory = "./Templates";
            DatabaseSchemas = new string[] { "*" };
            EntityCodeOutputDirectory = "./Output";
            
            RunCodeBuild = false;
            ProjectName = "FlexGenDb.%_DATABASE_%.%_SCHEMA_%";
            DeleteProjectFile = true;

            DoLogging = false;
            VerboseLogging = false;
            SetLogFile("./Logs/FlexGenDB_<t>.LOG");
            LogMessagePrefix = "[%T] - ";

            Database = "master";
            ConnectionString = "Server=localhost;Trusted_Connection=True;initial catalog=master;";
        }


        public static void ProcessArguments(string[] args)
        {
            SetDefaultConfiguration();
            try
            {
                for(int i = 0; i < args.Length; i++)
                {
                    string arg = args[i].ToLower();
                    switch(arg)
                    {
                        case HelpSwitch:
                            PrintHelpPage();
                            Environment.Exit((int)ExitCode.OK);
                            break;

                        case ConfigFileSwitch:
                            AppConfigReader.LoadConfigurationFromFile(args[++i]);
                            ImportFromConfigFile();
                            break;
                            
                        // Upgrade Script options
                        case UpgradeScriptSwitch:
                            RunUpgradeScripts = true;
                            break;

                        case UpgradeScriptDirectorySwitch:
                            UpgradeScriptDirectory = args[++i];
                            break;

                        case UpgradeScriptFileRegexSwitch:
                            UpgradeScriptFileRegex = args[++i];
                            break;

                        case UpgradeScriptTemplateGuidMonikerSwitch:
                            UpgradeScriptTemplateGuidMonikerPrefix = args[++i];
                            break;

                        // Entity code generation options
                        case EntityCodeGenerationSwitch:
                            RunEntityCodeGeneration = true;
                            break;

                        case TemplateDirectorySwitch:
                            TemplateDirectory = args[++i];
                            break;

                        case DatabaseSchemaListSwitch:
                            DatabaseSchemas = args[++i].Split(';');
                            break;

                        case CodeGenOutputSwitch:
                            EntityCodeOutputDirectory = args[++i];
                            break;

                        // Code build options
                        case CodeBuildSwitch:
                            RunCodeBuild = true;
                            break;

                        case ProjectNameSwitch:
                            ProjectName = args[++i];
                            break;


                        // Logging options
                        case LogFileSwitch:
                            DoLogging = true;
                            SetLogFile(args[++i]);
                            break;

                        case DebugSwitch:
                        case VerboseLoggingSwitch:
                            VerboseLogging = true;
                            break;

                        case LogMessagePrefixSwitch:
                            LogMessagePrefix = args[++i];
                            break;

                        // Database options
                        case DatabaseConnectionStringSwitch:
                            ConnectionString = args[++i];
                            break;

                        case DatabaseSwitch:
                            Database = args[++i];
                            break;
                    }
                }

                CheckDatabase();
                if (DatabaseSchemas.Contains("*"))
                {
                    string schemaQuery =
                        $"USE [{Database}] " +
                        "SELECT DISTINCT s.[name] FROM sys.tables t " +
                        "INNER JOIN sys.schemas s ON s.schema_id = t.schema_id " +
                        "WHERE s.[name] != 'dbo' " +
                        "UNION ALL SELECT 'dbo'";
                    var queryResults = SQLInterface.ExecuteQueryIntoDataTable(schemaQuery);
                    string result = "";
                    foreach(DataRow row in queryResults.Rows)
                        result += row.ItemArray[0].ToString() + ",";

                    DatabaseSchemas = result.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                }
            }
            catch(Exception e)
            {
                Console.WriteLine($"ERROR: {e.Message}");
                PrintHelpPage();
                Environment.Exit((int)ExitCode.ArgumentError);
            }
        }


        public static void PrintHelpPage()
        {
            Console.WriteLine(" ====== FlexGenDB Help ====== ");
            Console.WriteLine($"{HelpSwitch}: Print this help page.");
            Console.WriteLine($"{ConfigFileSwitch} <FilePath>: Specifies a JSON file containing application configuration.");
            Console.WriteLine(" ");
            Console.WriteLine(" -- Running Upgrade Scripts -- ");
            Console.WriteLine($"{UpgradeScriptSwitch}: Indicates to run database upgrade scripts during this execution.");
            Console.WriteLine($"{UpgradeScriptDirectorySwitch} <Path>: Specifies the directory containing database upgrade scripts.");
            Console.WriteLine($"{UpgradeScriptTemplateGuidMonikerSwitch} <Prefix>: Specifies the moniker prefix used in script templates to indicate placeholders for constant guid values. Moniker names are surrounded in {{brace characters}}.");
            Console.WriteLine(" ");
            Console.WriteLine(" -- Entity Code Generation -- ");
            Console.WriteLine($"{EntityCodeGenerationSwitch}: Indicates to generate entity code during this execution.");
            Console.WriteLine($"{DatabaseSchemaListSwitch} <SchemaList>: Specifies the database schemas to use when generating code, separated by semicolons. An asterisk (*) indicates all schemas.");
            Console.WriteLine($"{TemplateDirectorySwitch} <Directory>: Specifies the directory containing the code generation templates.");
            Console.WriteLine($"{CodeGenOutputSwitch} <Directory: Specifies the directory containing the generated source code");
            Console.WriteLine(" ");
            Console.WriteLine(" -- Database Config -- ");
            Console.WriteLine($"{DatabaseConnectionStringSwitch} <ConnectionString>: Specifies the connection string to the database server");
            Console.WriteLine($"{DatabaseSwitch} <Database>: Specifies the database to run these scripts against");
            Console.WriteLine(" ");
            Console.WriteLine(" -- Code Compilation -- ");
            Console.WriteLine($"{CodeBuildSwitch}: Indicates to build entity code during this execution.");
            Console.WriteLine($"{ProjectNameSwitch} <Name>: Specifies the name of the project when building");
            Console.WriteLine($"{KeepProjectFileSwitch}: Indicates the builder to not delete the project file if it exists");
            Console.WriteLine(" ");
            Console.WriteLine(" -- Process Logging -- ");
            Console.WriteLine($"{LogFileSwitch} <FilePath>: Specifies the file path to write process logs.");
            Console.WriteLine($"{VerboseLoggingSwitch}: Enables verbose process logging.");
            Console.WriteLine($"{DebugSwitch}: Enables verbose process logging.");
            Console.WriteLine($"{LogMessagePrefixSwitch} <Prefix>: Speciifes the prefix to use for each log entry in the file");
        }


        private static void ImportFromConfigFile()
        {
            string connectionString = AppConfigReader.GetString("Database", "ConnectionString");
            if(!string.IsNullOrEmpty(connectionString))
                ConnectionString = connectionString;

            string database = AppConfigReader.GetStringOrDefault("Database", "Database");
            if(!string.IsNullOrEmpty(database))
                Database = database;

            string runUpgradeScripts = AppConfigReader.GetStringOrDefault("UpgradeScripts", "Enabled");
            RunUpgradeScripts = string.Equals(runUpgradeScripts, "true", StringComparison.InvariantCultureIgnoreCase);

            string upgradeScriptDirectory = AppConfigReader.GetStringOrDefault("UpgradeScripts", "ScriptDirectory");
            if(!string.IsNullOrEmpty(upgradeScriptDirectory))
                UpgradeScriptDirectory = upgradeScriptDirectory;

            string upgradeFileRegex = AppConfigReader.GetStringOrDefault("UpgradeScripts", "ScriptFileRegex");
            if (upgradeFileRegex != null)
                UpgradeScriptFileRegex = upgradeFileRegex;

            string runCodeGeneration = AppConfigReader.GetStringOrDefault("CodeGeneration", "Enabled");
            RunEntityCodeGeneration = string.Equals(runCodeGeneration, "true", StringComparison.InvariantCultureIgnoreCase);

            string codeTemplateDirectory = AppConfigReader.GetStringOrDefault("CodeGeneration", "TemplateDirectory");
            if(!string.IsNullOrEmpty(codeTemplateDirectory))
                TemplateDirectory = codeTemplateDirectory;

            string[] databaseSchemas = AppConfigReader.GetArrayOrDefault("CodeGeneration", "DatabaseSchemas");
            if (databaseSchemas != null)
                DatabaseSchemas = databaseSchemas;

            string runCodeBuild = AppConfigReader.GetStringOrDefault("CodeBuild", "Enabled");
            RunCodeBuild = string.Equals(runCodeBuild, "true", StringComparison.InvariantCultureIgnoreCase);

            string doLogging = AppConfigReader.GetStringOrDefault("Logging", "Enabled");
            DoLogging = string.Equals(doLogging, "true", StringComparison.InvariantCultureIgnoreCase);

            string logFile = AppConfigReader.GetStringOrDefault("Logging", "LogFile");
            if (!string.IsNullOrEmpty(logFile))
                SetLogFile(logFile);

            string logMessagePrefix = AppConfigReader.GetStringOrDefault("Logging", "LogLinePrefix");
            if (logMessagePrefix != null)
                LogMessagePrefix = logMessagePrefix;

            string verboseLogging = AppConfigReader.GetStringOrDefault("Logging", "Verbose");
            VerboseLogging = string.Equals(verboseLogging, "true", StringComparison.InvariantCultureIgnoreCase);
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


        private static void SetLogFile(string logFile)
        {
            logFile = logFile.Replace("<t>", DateTime.Now.ToString("yyyyMMdd"));
            logFile = logFile.Replace("<T>", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            logFile = logFile.Replace("<D>", Database);
            LogFile = logFile;
        }
    }
}
