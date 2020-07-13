# ExeLauncher

Launch Console and Windows apps packaged as NuGet packages. Keep them auto-updated.

## Install

```
dotnet tool install exelauncher --global
```

Or optionally clone the source code and build the exe-file.

## Usage

```
exelauncher MyCompany.MyApp
```

Package your application as a NuGet package. The binaries should be in the root of the package or in the root of the package's content-folder. Make sure that the package doesn't depent on any other package.

Add the package into NuGet or into any other NuGet feed.

Use ExeLauncher to launch your application. It will automatically download the latest version of the application from the package feed and then run it.

## Auto-update

If the package gets updated, your application gets automatically updated the next time the ExeLauncher is started.

## Configuration

One parameter is mandatory: package id. This can be given as command line arguments or through the ExeLauncher's configuration file:

* package = NuGet package id.

Optional parameters:

* app = Name of your application. Displayed in the launcher and when creating the folders for the application. Please don't use special characters. Defaults to value of package.
* cmd = The launch command for your application. If empty, ExeLauncher scans for an .exe-file.
* gui = true or false. True is default = shows launcher gui.
* nuget = Path to Nuget.config. By default (if missing) uses machine level configuration.
* approot = Application's root folder. The packages are downloaded and installed here. Defaults to AppData + app if missing.
* interval = Interval of how often new versions are scanned for and downloaded, in minutes. Defaults to 3 minutes.
* clean = Fresh start for the application. All the files are deleted, then the latest version is downloaded and launched.
* workingdir = Your application's working directory. Defaults to executable's root.
* arguments = Arguments passed into your application when it is launched.
* feedurl = Url of a custom Nuget-feed. Can be used instead of Nuget.config.
* feeduser = Username for the custom Nuget-feed. Can be used instead of Nuget.config.
* feedpassword= Password for the custom Nuget-feed. Can be used instead of Nuget.config.

Example of launching an app from command line without GUI, with a custom Nuget.config and with a custom launch command and with arguments:

```
exelauncher MyApp.App -app "My Application" -gui "false" -nuget "C:\temp\NuGet.Config" -cmd "content\MyApp.exe" -args "-datadir c:\temp\mydb56"
```

Example of configuring the ExeLauncher through appsettings.json configuration file. All the parameters listed above can be used:

```json
{
  "package": "HourManagement.App", 
  "feedurl": "https://pkgs.dev.azure.com/adafy/df962856-ce0c-4e96-8999-bee7c8b0582c/_packaging/AdafyPublic/nuget/v3/index.json"
}
```

## Support

Support is available through the GitHub issues.

If you need enterprise support, please contact Adafy Oy: info@adafy.com

## License

MIT