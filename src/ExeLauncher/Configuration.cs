using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;

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
                var result = GetParameter("app", true);

                if (!string.IsNullOrWhiteSpace(result))
                {
                    return result;
                }

                return Package;
            }
        }

        public static string NugetConfigPath
        {
            get
            {
                return GetParameter("nuget", true);
            }
        }

        public static ShowLauncherEnum ShowLauncher
        {
            get
            {
                var guiValue = GetParameter("gui", true);

                if (string.IsNullOrWhiteSpace(guiValue))
                {
                    return ShowLauncherEnum.Always;
                }

                return Enum.Parse<ShowLauncherEnum>(guiValue);
            }
        }

        public static string GetApplicationRootFolder()
        {
            return GetApplicationRootFolder(GetParameter("approot", true), Package);
        }
        
        public static string GetApplicationRootFolder(string appRoot, string packageName)
        {
            if (!string.IsNullOrWhiteSpace(appRoot))
            {
                return appRoot;
            }

            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var result = Path.Combine(appDataPath, packageName);

            return result;
        }

        public static string LaunchCommand
        {
            get
            {
                return GetParameter("cmd", true);
            }
        }

        public static int AutoUpdateInterval
        {
            get
            {
                var result = GetParameter("interval", true);

                if (string.IsNullOrWhiteSpace(result))
                {
                    return 3;
                }

                return int.Parse(result);
            }
        }

        public static bool Clean
        {
            get
            {
                var result = GetParameter("clean", true);

                if (string.IsNullOrWhiteSpace(result))
                {
                    return false;
                }

                return bool.Parse(result);
            }
        }

        public static string WorkingDir
        {
            get
            {
                var result = GetParameter("workingdir", true);

                return result;
            }
        }

        public static string Arguments
        {
            get
            {
                var result = GetParameter("args", true);

                return result;
            }
        }

        public static string PackageFeedUrl
        {
            get
            {
                return GetParameter("feedurl", true);
            }
        }

        public static string PackageFeedUsername
        {
            get
            {
                return GetParameter("feeduser", true);
            }
        }

        public static string PackageFeedPassword
        {
            get
            {
                return GetParameter("feedpassword", true);
            }
        }

        public static string ExportPath
        {
            get
            {
                return GetParameter("exportpath", true);
            }
        }

        private static string GetParameter(string parameterName, bool allowMissing = false)
        {
            InitializeConfiguration();

            var keyNames = GetFullConfiguration();

            if (!keyNames.ContainsKey(parameterName) && allowMissing == false)
            {
                throw new Exception($"Missing required parameter: {parameterName}. Either add it to configuration file or provide a command line argument.");
            }

            if (!keyNames.ContainsKey(parameterName))
            {
                return string.Empty;
            }

            return keyNames[parameterName];
        }

        public static Dictionary<string, string> GetFullConfiguration()
        {
            return _configuration.AsEnumerable().ToDictionary(x => x.Key, x => x.Value);
        }

        private static IConfiguration _configuration;
        private static IConfiguration _defaultConfiguration;
        private static string _initializationLock = "lock";

        private static void InitializeConfiguration()
        {
            if (_configuration != null)
            {
                return;
            }

            lock (_initializationLock)
            {
                if (_configuration == null)
                {
                    // Configuration order:
                    // 1. ExeLauncher's Appsettings.json
                    // 2. Command line arguments
                    // 3. Application's Appsettings.json

                    IConfigurationBuilder configBuilder = new ConfigurationBuilder();

#if EXE
                    // Required because we publish a single exe file which doesn't include appsettings.json
                    var hostDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
#else
                    var hostDirectory = Path.GetDirectoryName(typeof(Configuration).Assembly.Location);
#endif

                    DefaultConfigurationFilePath = Path.Combine(hostDirectory, "appsettings.json");

                    var args = Environment.GetCommandLineArgs();
                    var dict = new Dictionary<string, string>();

                    if (args.Length > 1 && !args[1].StartsWith("-"))
                    {
                        // Package name is given as the "default" parameter, without -package. For example: ExeLauncher MyPackage
                        dict.Add("package", args[1]);
                    }

                    configBuilder = configBuilder.AddJsonFile(DefaultConfigurationFilePath, true)
                        .AddCommandLine(Environment.GetCommandLineArgs(), new Dictionary<string, string>()
                        {
                            { "-package", "package" },
                            { "-app", "app" },
                            { "-nuget", "nuget" },
                            { "-gui", "gui" },
                            { "-approot", "approot" },
                            { "-cmd", "cmd" },
                            { "-interval", "interval" },
                            { "-clean", "clean" },
                            { "-workingdir", "workingdir" },
                            { "-args", "args" },
                            { "-feedurl", "feedurl" },
                            { "-feeduser", "feeduser" },
                            { "-feedpassword", "feedpassword" },
                            { "-exportpath", "exportpath" },
                        })
                        .AddInMemoryCollection(dict);

                    _defaultConfiguration = configBuilder.Build();

                    var appRootFolder = GetApplicationRootFolder(_defaultConfiguration["approot"], _defaultConfiguration["package"]);
                    ApplicationConfigurationFilePath = Path.Combine(appRootFolder, "appsettings.json");

                    _configuration = configBuilder.AddJsonFile(ApplicationConfigurationFilePath, true).Build();
                }
            }
        }

        public static string ApplicationConfigurationFilePath { get; private set; }
        public static string DefaultConfigurationFilePath { get; private set; }
        public static Dictionary<string, string> GetLaunchArguments()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var args = Environment.GetCommandLineArgs();

            if (args.Length == 1)
            {
                return result;
            }

            var containsPackageName = !args[1].StartsWith("-");

            if (containsPackageName)
            {
                result.Add("package", args[1]);
            }

            if (args.Length == 2)
            {
                return result;
            }

            try
            {
                var index = containsPackageName ? 2 : 1;

                for (; index < args.Length; index += 2)
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
