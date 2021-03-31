using System;
using System.Collections.Generic;
using System.Data;
using System.IO;

namespace FlexGenDB
{
    public static class DatabaseEntityCSharpGenerator
    {
        private static string Database;
        private static string[] Schemas;
        private static string TemplateDirectory;
        private static string OutputDirectory;
        private static DataTable DatabaseSchema;

        private static string currentSchema;
        private static string currentTableName;
        private static string currentTableAlias;
        private static string currentColumnName;
        private static string currentFunction;
        private static string currentFunctionParametersCSharp;
        private static string currentFunctionParametersSQL;
        private static int fieldPrecision;
        private static byte fieldScale;
        private static short? fieldMaxLength;

        private static List<string> headerLines = new List<string>();
        private static List<string> fieldLines = new List<string>();
        private static List<string> footerLines = new List<string>();
        private static List<DataRow> currentTableSchema = new List<DataRow>();


        public static void Run()
        {
            Logger.Log("Starting Database Entity Code Generator");
            LoadConfiguration();

            foreach(string schema in Schemas)
            {
                currentSchema = schema;   
                PrepareOutputDirectory();
                GetDatabaseSchema();
                BuildEntityClasses();
                BuildFunctionClass();
            }
        }


        private static void LoadConfiguration()
        {
            Logger.Log("Loading session configuration");
            Database = SessionConfiguration.Database;
            Logger.LogVerbose($"Database: {Database}");

            Schemas = SessionConfiguration.DatabaseSchemas;
            string schemaLog = string.Empty;
            foreach(string schema in Schemas)
                schemaLog += $"{schema}, ";

            Logger.LogVerbose($"Schemas: ({schemaLog})");

            TemplateDirectory = SessionConfiguration.TemplateDirectory;
            Directory.CreateDirectory(TemplateDirectory);
            Logger.LogVerbose($"Template directory: {TemplateDirectory}");

            OutputDirectory = SessionConfiguration.EntityCodeOutputDirectory;
            Directory.CreateDirectory(OutputDirectory);
            Logger.LogVerbose($"Output directory: {OutputDirectory}");
        }


        private static void PrepareOutputDirectory()
        {
            OutputDirectory = SessionConfiguration.EntityCodeOutputDirectory;
            OutputDirectory += $"/{Database}.{currentSchema}";
            Directory.CreateDirectory(OutputDirectory);

            foreach (string file in Directory.GetFiles(OutputDirectory, "*.cs"))
                File.Delete(file);
        }


        private static void GetDatabaseSchema()
        {
            Logger.Log("Retrieving table schema");
            string tableQueryFile = Path.Combine(TemplateDirectory, "TableSchemaQuery.txt");
            string tableSchemaQuery = File.ReadAllText(tableQueryFile);
            tableSchemaQuery = tableSchemaQuery.Replace("%_DATABASE_%", Database);
            tableSchemaQuery = tableSchemaQuery.Replace("%_SCHEMA_%", currentSchema);
            DatabaseSchema = SQLInterface.ExecuteQueryIntoDataTable(tableSchemaQuery);
        }


        private static void BuildEntityClasses()
        {
            Logger.Log("Building entity code");
            int rowIndex = 0;

            headerLines.Clear();
            fieldLines.Clear();
            footerLines.Clear();

            DataRow currentRow;
            while(rowIndex < DatabaseSchema.Rows.Count)
            {
                currentRow = DatabaseSchema.Rows[rowIndex];
                currentTableName = currentRow["TableName"].ToString();
                currentTableAlias = currentRow["TableAlias"].ToString();
                Logger.LogVerbose($"Building schema for table {currentTableName}");
                currentTableSchema = new List<DataRow>
                {
                    currentRow
                };
                currentRow = DatabaseSchema.Rows[++rowIndex];
                while(rowIndex < DatabaseSchema.Rows.Count
                    && string.Equals(currentTableName, currentRow["TableName"].ToString(), StringComparison.InvariantCultureIgnoreCase))
                {
                    currentTableSchema.Add(currentRow);
                    rowIndex++;
                    if (rowIndex < DatabaseSchema.Rows.Count)
                        currentRow = DatabaseSchema.Rows[rowIndex];
                }
                BuildTableClass();

                headerLines.Clear();
                fieldLines.Clear();
                footerLines.Clear();
            }
        }


        private static void BuildTableClass()
        {
            Logger.Log($"Building class file for table {currentTableName}");

            string fileText = string.Empty;
            string headerText = ParseTemplate("EntityHeader.txt");
            fileText += headerText;

            string dataType;
            bool isNullable, isPrimaryKey, isIdentity;
            foreach (var column in currentTableSchema)
            {
                currentColumnName = (string)column["ColumnName"];
                fieldMaxLength = (short?)column["MaxLength"];
                fieldPrecision = (int)column["Precision"];
                fieldScale = (byte)column["Scale"];

                isNullable = (bool)column["Nullable"];
                isPrimaryKey = (int)column["IsPrimaryKey"] == 1;
                isIdentity = (int)column["IsIdentity"] == 1;
                dataType = (string)column["DataType"];

                string templateFileName = dataType;
                if (isNullable)
                    templateFileName += "Nullable";

                templateFileName += ".txt";
                string fieldCode = ParseTemplate(templateFileName);
                fileText += fieldCode;
            }
            string footerText = ParseTemplate("EntityFooter.txt");
            fileText += footerText;

            string fileName = $"{currentTableName}.cs";
            string outputPath = Path.Combine(OutputDirectory, fileName);
            Logger.LogVerbose($"Writing code to {outputPath}");
            File.WriteAllText(outputPath, fileText);
            Logger.LogVerbose($"Code written successfully");
        }


        private static void BuildFunctionClass()
        {
            Logger.Log("Retrieving function schema");
            string outputFileName = "DbFunctions.cs";
            string functionSchemaQuery = ParseTemplate("FunctionSchemaQuery.txt");
            functionSchemaQuery = functionSchemaQuery.Replace("%_DATABASE_%", Database);
            functionSchemaQuery = functionSchemaQuery.Replace("%_SCHEMA_%", currentSchema);
            var functionSchema = SQLInterface.ExecuteQueryIntoDataTable(functionSchemaQuery);
            if(functionSchema.Rows.Count == 0)
            {
                Logger.Log("No functions found");
                return;
            }

            headerLines.Clear();
            fieldLines.Clear();
            footerLines.Clear();

            string fileText = ParseTemplate("FunctionHeader.txt");
            int i = 0;
            List<DataRow> parameters = new List<DataRow>();
            DataRow currentRow;
            while(i < functionSchema.Rows.Count)
            {
                currentRow = functionSchema.Rows[i];
                currentFunction = currentRow["FunctionName"].ToString();
                parameters.Add(currentRow);
                i++;
                if(i < functionSchema.Rows.Count)
                {
                    currentRow = functionSchema.Rows[i];
                    while(i < functionSchema.Rows.Count
                        && currentFunction.Equals(currentRow["FunctionName"].ToString()))
                    {
                        parameters.Add(currentRow);
                        i++;
                        if (i < functionSchema.Rows.Count)
                            currentRow = functionSchema.Rows[i];
                    }
                }
                fileText += BuildFunctionMethodDefinition(parameters);
            }
            fileText += ParseTemplate("FunctionFooter.txt");
            string outputPath = Path.Combine(OutputDirectory, outputFileName);
            Logger.Log($"Writing code to {outputPath}");
            File.WriteAllText(outputPath, fileText);
        }


        #region Helper Methods

        private static string BuildFunctionMethodDefinition(List<DataRow> parameters)
        {
            string functionName = parameters[0]["FunctionName"].ToString();
            Logger.Log($"Getting method definition for function {functionName}");
            string parametersCSharp = string.Empty;
            string parametersSql = string.Empty;

            foreach(var param in parameters)
            {
                string parameterName = param["ParamName"].ToString();
                if (string.IsNullOrEmpty(parameterName))
                    continue;

                int userTypeId = (int)param["DataType"];
                bool isNullable = (bool)param["Nullable"];

                parameterName = parameterName.Substring(1);
                string dataType = GetCSharpDataType(userTypeId, isNullable);
                parametersCSharp += $" {dataType} {parameterName},";
                parametersSql += $" {{{parameterName}.ToSqlString()}},";
            }
            if(!string.IsNullOrEmpty(parametersCSharp))
                parametersCSharp = parametersCSharp.Substring(1, parametersCSharp.Length - 2);

            if(!string.IsNullOrEmpty(parametersSql))
                parametersSql = parametersSql.Substring(1, parametersSql.Length - 2);

            Logger.LogVerbose($"C# Parameters: {parametersCSharp}");
            Logger.LogVerbose($"SQL Parameters: {parametersSql}");
            var additionalParameters = new Dictionary<string, string>
            {
                { "FUNCTION", functionName },
                { "FUNCTIONPARAMS_CSHARP", parametersCSharp },
                { "FUNCTIONPARAMS_SQL", parametersSql }
            };
            return ParseTemplate("FunctionMethod.txt", additionalParameters);
        }


        private static string GetCSharpDataType(int userTypeId, bool isNullable)
        {
            string result = "";
            switch (userTypeId)
            {
                case 35:
                case 99:
                case 167:
                case 231:
                    return "string";

                case 36:
                    result = "Guid";
                    break;

                case 56:
                    result = "int";
                    break;

                case 61:
                    result = "DateTime";
                    break;

                case 104:
                    result = "bool";
                    break;

                case 108:
                    result = "decimal";
                    break;
            }
            if (isNullable)
                result += "?";
            return result;
        }

        private static string ParseTemplate(string filePath, Dictionary<string, string> additionalMonikers = null)
        {
            string result = string.Empty;
            Directory.CreateDirectory(TemplateDirectory);
            filePath = Path.Combine(TemplateDirectory, filePath);
            Logger.LogVerbose($"Searching for template file {filePath}");
            if (!File.Exists(filePath))
            {
                Logger.Log($"Template file {filePath} does not exist");
                return result;
            }
            result = File.ReadAllText(filePath);

            // Database level monikers
            result = result.Replace("%_CONNECTIONSTRING_%", SessionConfiguration.ConnectionString)
                .Replace("%_DATABASE_%", Database)
                .Replace("%_SCHEMA_%", currentSchema)
                .Replace("%_TABLE_%", currentTableName)
                .Replace("%_TABLEALIAS_%", currentTableAlias)
                .Replace("%_COLUMN_%", currentColumnName)
                .Replace("%_MAXLENGTH_%", fieldMaxLength.ToString())
                .Replace("%_PRECISION_%", fieldPrecision.ToString())
                .Replace("%_SCALE_%", fieldScale.ToString());

            if(additionalMonikers != null)
            {
                foreach(var moniker in additionalMonikers)
                {
                    string monikerName = $"%_{moniker.Key.ToUpper()}_%";
                    result = result.Replace(monikerName, moniker.Value);
                }
            }

            return result;
        }

        #endregion
    }
}
