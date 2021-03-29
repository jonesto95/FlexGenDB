using System;

namespace FlexGenDB
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                SessionConfiguration.ProcessArguments(args);
                Logger.Log(" ======== Starting FlexGenDB ========");
                if(SessionConfiguration.RunUpgradeScripts)
                {
                    SQLUpgradeScriptRunner.Run();
                }
                if(SessionConfiguration.RunEntityCodeGeneration)
                {
                    DatabaseEntityCSharpGenerator.Run();
                }
                if(SessionConfiguration.RunCodeBuild)
                {
                    CSharpCodeBuilder.Run();
                }
                Logger.Log(" ======== FlexGenDB completed successfully ========");
                Environment.Exit((int)ExitCode.OK);
            }
            catch(ArgumentParsingException e)
            {
                Console.WriteLine($"ERROR: {e.Message}");
                SessionConfiguration.PrintHelpPage();
                Environment.Exit((int)ExitCode.ArgumentError);
            }
            catch(Exception e)
            {
                Console.WriteLine($"ERROR: {e.Message}");
                Environment.Exit((int)ExitCode.UnknownError);
            }
        }
    }
}
