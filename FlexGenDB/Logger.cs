using System;
using System.IO;

namespace FlexGenDB
{
    public static class Logger
    {
        public static void Log(object message)
        {
            Console.WriteLine(message);
            if(SessionConfiguration.DoLogging)
            {
                BuildLogDirectory();
                string messageString = BuildMessageString(message);
                File.AppendAllLines(SessionConfiguration.LogFile, new string[] { messageString });
            }
        }


        public static void LogVerbose(object message)
        {
            if(SessionConfiguration.VerboseLogging)
                Log(message);
        }


        private static void BuildLogDirectory()
        {
            if(!File.Exists(SessionConfiguration.LogFile))
            {
                string directory = Path.GetDirectoryName(SessionConfiguration.LogFile);
                Directory.CreateDirectory(directory);
                LogVerbose($"Log directory {directory} created");
            }
        }


        // Helper methods

        private static string BuildMessageString(object message)
        {
            string prefix = ParsePrefix();
            return prefix + message;
        }


        private static string ParsePrefix()
        {
            string prefix = SessionConfiguration.LogMessagePrefix;
            prefix = prefix.Replace("%t", DateTime.Now.ToString("HH:mm:ss.fff"));
            prefix = prefix.Replace("%T", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
            return prefix;
        }
    }
}
