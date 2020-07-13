using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Newtonsoft.Json;
using NLog;

namespace ExeLauncher
{
    public class ExporterService
    {
        private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        public void Export()
        {
            try
            {
                _logger.Info("Exporting exelauncher with configuration");
                var args = Configuration.GetLaunchArguments();
                var exportPath = Configuration.ExportPath;

                var configurationFilePath = Path.Combine(exportPath, "appsettings.json");
            
                _logger.Info("Export path is set to {ExportPath}. Exported configuration file path is set to {ConfigurationFilePath}", exportPath, configurationFilePath);

                var argElements = new List<XElement>();

                foreach (var arg in args)
                {
                    var keyAttribute = new XAttribute("key", arg.Key);
                    var valueAttribute = new XAttribute("value", arg.Value);
                
                    var argElement = new XElement("add", keyAttribute, valueAttribute);
                
                    argElements.Add(argElement);
                }    
            
                var appSettingsElement = new XElement("appSettings");

                foreach (var argElement in argElements)
                {
                    appSettingsElement.Add(argElement);
                }
            
                var configDocument = new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement("configuration", appSettingsElement));

                if (!Directory.Exists(exportPath))
                {
                    _logger.Debug("Export path {ExportPath} doesn't exist, creating directory");
                    Directory.CreateDirectory(exportPath);
                }

                var xml = configDocument.Declaration + Environment.NewLine + configDocument;
                var json = JsonConvert.SerializeObject(args, Formatting.Indented);
                
                File.WriteAllText(configurationFilePath, json);
            
                _logger.Info("Export complete");
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to export configuration");

                throw;
            }
        }
    }
}
