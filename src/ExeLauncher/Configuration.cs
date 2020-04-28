using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;

namespace ExeLauncher
{
    public static class Configuration
    {
        public static string Package
        {
            get
            {
                return GetParameter("package");
            }
        }

        public static string ApplicationName
        {
            get
            {
                return GetParameter("app");
            }
        }

        public static string NugetConfigPath
        {
            get
            {
                return GetParameter("nuget", true);
            }
        }
        
        public static bool ShowLauncher
        {
            get
            {
                var hideValue = GetParameter("gui", true);

                if (string.IsNullOrWhiteSpace(hideValue))
                {
                    return true;
                }

                return bool.Parse(hideValue);
            }
        }
        
        public static string ApplicationRootFolder(string applicationName)
        {
            var appRoot = GetParameter("approot", true);

            if (!string.IsNullOrWhiteSpace(appRoot))
            {
                return appRoot;
            }

            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            
            var result = Path.Combine(appDataPath, applicationName);

            return result;
        }

        public static string LaunchCommand
        {
            get
            {
                return GetParameter("cmd");
            }
        }

        private static string GetParameter(string parameterName, bool allowMissing = false)
        {
            var launchArgument = GetLaunchArguments();

            if (launchArgument.ContainsKey(parameterName))
            {
                return launchArgument[parameterName];
            }

            if (!string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings[parameterName]))
            {
                return ConfigurationManager.AppSettings[parameterName];
            }

            if (allowMissing)
            {
                return string.Empty;
            }

            throw new Exception($"Missing required parameter: {parameterName}. Either add it to configuration file or provide a command line argument.");
        }
        

        private static Dictionary<string, string> GetLaunchArguments()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var args = Environment.GetCommandLineArgs();

            try
            {
                for (var index = 1; index < args.Length; index += 2)
                {
                    result.Add(args[index].Trim('-'), args[index + 1]);
                }
            }
            catch (Exception e)
            {
                throw new Exception("Invalid launch arguments: " + string.Join(" ", args), e);
            }

            return result;
        }
    }
}
