using System;

namespace FlexGenDB
{
    public class ConfigurationNotFoundException : Exception
    {
        public ConfigurationNotFoundException(string configurationKey, string configurationFilePath)
            : base($"Configuration {configurationKey} not found in config file {configurationFilePath}") { }
    }


    public class ArgumentParsingException : Exception
    {
        public ArgumentParsingException() : base("") { }
        public ArgumentParsingException(string message) : base(message) { }
    }
}
