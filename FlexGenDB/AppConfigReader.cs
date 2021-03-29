using Microsoft.Extensions.Configuration;

namespace FlexGenDB
{
    public static class AppConfigReader
    {
        private static IConfiguration Configuration
        {
            get
            {
                if (_configuration == null)
                    LoadConfigurationFromFile("appconfig.json");

                return _configuration;
            }
        }
        private static IConfiguration _configuration;

        private static string ConfigurationFilePath;


        public static void LoadConfigurationFromFile(string filePath)
        {
            ConfigurationFilePath = filePath;
            _configuration = new ConfigurationBuilder()
                .AddJsonFile(ConfigurationFilePath)
                .Build();
        }


        public static string GetString(params string[] keys)
        {
            string key = ConcatenateConfigKeys(keys);
            string result = Configuration[key];
            if (result == null)
                throw new ConfigurationNotFoundException(key, ConfigurationFilePath);

            return result;
        }

        public static string GetStringOrDefault(params string[] keys)
        {
            string key = ConcatenateConfigKeys(keys);
            string result = Configuration[key];
            return result;
        }


        public static string[] GetArrayOrDefault(params string[] keys)
        {
            string key = ConcatenateConfigKeys(keys);
            string[] result = Configuration.GetSection(key).Get<string[]>();
            return result;
        }


        private static string ConcatenateConfigKeys(string[] keys)
        {
            string result = "";
            foreach(string key in keys)
            {
                result += $"{key}:";
            }
            return result.Substring(0, result.Length - 1);
        }
    }
}
