# ExeLauncher

Launch Console and Windows apps packaged as NuGet packages. Keep them auto-updated.

## Install

```
dotnet tool install exelauncher --global
```

Or optionally clone the source code and build the exe-file.

## Usage

```
exelauncher -app "My Application" -package "MyCompany.MyApp"
```

Package your application as a NuGet package. The binaries should be in the root of the package or in the root of the package's content-folder. Make sure that the package doesn't depent on any other package.

Add the package into NuGet or into any other NuGet feed.

Use ExeLauncher to launch your application. It will automatically download the latest version of the application from the package feed and then run it.

## Auto-update

If the package gets updated, your application gets automatically updated the next time the ExeLauncher is started.

## Configuration

Two parameters are mandatory: app and package. These can be given as command line arguments or through the ExeLauncher's configuration file:

* app = Name of your application. Displayed in the launcher and when creating the folders the application. Please don't use special characters.
* package = NuGet package id.

Optional parameters:

* cmd = The launch command for your application. If empty, ExeLauncher scans for an .exe-file.
* gui = true or false. True is default = shows launcher gui.
* nuget = Path to Nuget.config. By default (if missing) uses machine level configuration.
* approot = Application's root folder. The packages are downloaded and installed here. Defaults to AppData + app if missing.

Example of launching an app from command line without GUI, with a custom Nuget.config and with a custom launch command:

```
exelauncher -app "Hour Manager" -package "HourManagement.App" -gui "false" -nuget "C:\temp\NuGet.Config" -cmd "content\MyApp.exe"
```

Example of configuring the ExeLauncher through config-file:

```xml
<?xml version="1.0" encoding="utf-8"?>

<configuration>

    <appSettings>

        <!-- Mandatory -->
        <add key="app" value="" />
        <add key="package" value="" />

        <!-- Optional -->
        <add key="cmd" value=""/> <!-- The launch command for your application. If empty, ExeLauncher scans for an .exe-file. -->
        <add key="gui" value=""/> <!-- true or false. true = show launcher gui -->
        <add key="nuget" value="C:\temp\NuGet.Config" /> <!--Path to Nuget.config. By default uses machine level configuration.-->
        <add key="approot" value=""/> <!-- Application's root folder. The packages are downloaded and installed here. Defaults to AppData + app if missing. -->

        <!-- Optionally can be provided through command line arguments: -app "Hour Manager" -package "HourManagement.App" -nuget "C:\temp\NuGet.Config" -gui "true" -cmd "content\HourManagement.App.exe" -->

    </appSettings>

</configuration>
```

## Support

Support is available through the GitHub issues.

If you need enterprise support, please contact Adafy Oy: info@adafy.com

## License

MIT