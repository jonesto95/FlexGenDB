using System;
using System.Diagnostics;
using System.IO;

namespace FlexGenDB
{
    public static class CSharpCodeBuilder
    {
        private static string OutputDirectory;
        private static string TemplateDirectory;
        private static string Database;
        private static string[] Schemas;
        private static string ProjectName;
        private static string currentSchema;
        private static string projectFilePath;

        public static void Run()
        {
            Logger.Log("Starting code builder");
            LoadConfiguration();

            foreach(string schema in Schemas)
            {
                currentSchema = schema;
                PrepareProjectFile();
                BuildCode();
            }
        }


        private static void LoadConfiguration()
        {
            Logger.Log("Loading session configuration");
            Logger.Log("Loading session configuration");
            Database = SessionConfiguration.Database;
            Logger.LogVerbose($"Database: {Database}");

            Schemas = SessionConfiguration.DatabaseSchemas;
            string schemaLog = string.Empty;
            foreach (string schema in Schemas)
                schemaLog += $"{schema}, ";

            Logger.LogVerbose($"Schemas: ({schemaLog})");

            OutputDirectory = SessionConfiguration.EntityCodeOutputDirectory;
            Directory.CreateDirectory(OutputDirectory);
            Logger.LogVerbose($"Output directory: {OutputDirectory}");

            TemplateDirectory = SessionConfiguration.TemplateDirectory;
            Logger.LogVerbose($"Tempalte directory: {TemplateDirectory}");

            ProjectName = SessionConfiguration.ProjectName;
            Logger.LogVerbose($"Project name: {ProjectName}");
        }


        private static void PrepareProjectFile()
        {
            string outputDirectory = $"{OutputDirectory}/{Database}.{currentSchema}";
            string[] projectFiles = Directory.GetFiles(outputDirectory, "*.csproj");
            if(SessionConfiguration.DeleteProjectFile)
                foreach (string file in projectFiles)
                    File.Delete(file);

            if(SessionConfiguration.DeleteProjectFile || projectFiles.Length == 0)
            {
                string projectFileContents = ReadTemplate("CSharpProjectFile.txt");
                string projectFileName = ProjectName
                    .Replace("%_DATABASE_%", Database)
                    .Replace("%_SCHEMA_%", currentSchema)
                    + ".csproj";
                projectFilePath = Path.Combine(outputDirectory, projectFileName);
                File.WriteAllText(projectFilePath, projectFileContents);
            }
        }


        private static void BuildCode()
        {
            var buildProcess = new Process()
            {
                StartInfo = new ProcessStartInfo("cmd", $"/c dotnet build \"{projectFilePath}\"")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }
            };
            buildProcess.Start();

            string processResult = buildProcess.StandardOutput.ReadToEnd();
            if(buildProcess.ExitCode != 0)
            {
                Logger.Log("Error in build");
                Logger.LogVerbose(processResult);
                Environment.Exit((int)ExitCode.BuildError);
            }
        }

        #region Helper Methods



        private static string ReadTemplate(string filePath)
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

            return result;
        }

        #endregion
    }
}
