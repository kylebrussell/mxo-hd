using System;
using System.IO;
using System.Xml;

namespace hds{
	
    public class XmlParser{
	
		public static void loadDBParams(string fileName, out DbParams _params){

            _params = new DbParams();

            var xDoc = new XmlDocument();
            fileName = ResolveConfigFile(fileName);
            xDoc.Load(fileName);

            XmlElement values = FirstSection(xDoc, "DBConfig");
            if (values != null)
            {
                _params.Host = RequiredText(values, "serverHost");
                _params.Port = int.Parse(RequiredText(values, "serverPort"));
                _params.DatabaseName = RequiredText(values, "databaseName");
                _params.Username = RequiredText(values, "databaseUser");
                _params.Password = RequiredText(values, "databasePassword");
                _params.Motd = OptionalText(values, "motd", "");
                _params.DbType = OptionalText(values, "dbType", "mysql").ToLower();
                return;
            }

            values = FirstSection(xDoc, "Database");
            if (values == null)
            {
                throw new InvalidOperationException("Config is missing a DBConfig or Database section.");
            }

            _params.Host = RequiredText(values, "Host");
            _params.Port = int.Parse(RequiredText(values, "Port"));
            _params.DatabaseName = RequiredText(values, "Database");
            _params.Username = RequiredText(values, "Username");
            _params.Password = RequiredText(values, "Password");
            _params.Motd = OptionalText(values, "Motd", "");
            _params.DbType = OptionalText(values, "DbType", "mysql").ToLower();

		}
	
		public static void loadServerParams(string fileName, out ServerParams _params){
            _params = new ServerParams();
            
            var xDoc = new XmlDocument();
            fileName = ResolveConfigFile(fileName);
            xDoc.Load(fileName);
			XmlElement values = FirstSection(xDoc, "ServerConfig");
            if (values != null)
            {
                _params.AdminConsoleEnabled = IsEnabled(OptionalText(values, "adminConsoleEnabled", "off"));
                _params.ServerType = OptionalText(values, "ServerType", "cr2").ToLower();
                return;
            }

            values = FirstSection(xDoc, "Server");
            _params.AdminConsoleEnabled = values != null &&
                IsEnabled(OptionalText(values, "AdminConsoleEnabled", "false"));
            _params.ServerType = values != null
                ? OptionalText(values, "ServerType", "cr2").ToLower()
                : "cr2";
		}

        private static XmlElement FirstSection(XmlDocument document, string sectionName)
        {
            XmlNodeList nodes = document.GetElementsByTagName(sectionName);
            if (nodes.Count == 0)
            {
                return null;
            }

            return nodes[0] as XmlElement;
        }

        private static string RequiredText(XmlElement parent, string tagName)
        {
            XmlNodeList nodes = parent.GetElementsByTagName(tagName);
            if (nodes.Count == 0)
            {
                throw new InvalidOperationException("Config section " + parent.Name + " is missing " + tagName + ".");
            }

            return nodes[0].InnerText;
        }

        private static string OptionalText(XmlElement parent, string tagName, string defaultValue)
        {
            XmlNodeList nodes = parent.GetElementsByTagName(tagName);
            if (nodes.Count == 0)
            {
                return defaultValue;
            }

            return nodes[0].InnerText;
        }

        private static bool IsEnabled(string value)
        {
            string normalized = value.Trim().ToLower();
            return normalized == "on" || normalized == "true" || normalized == "1" || normalized == "yes";
        }

        private static string ResolveConfigFile(string fileName)
        {
            if (File.Exists(fileName))
            {
                return fileName;
            }

            if (fileName.Equals("Config.xml", StringComparison.OrdinalIgnoreCase) &&
                File.Exists("Config.xml.dist"))
            {
                return "Config.xml.dist";
            }

            string outputPath = Path.Combine(AppContext.BaseDirectory, fileName);
            if (File.Exists(outputPath))
            {
                return outputPath;
            }

            if (fileName.Equals("Config.xml", StringComparison.OrdinalIgnoreCase))
            {
                string outputDistPath = Path.Combine(AppContext.BaseDirectory, "Config.xml.dist");
                if (File.Exists(outputDistPath))
                {
                    return outputDistPath;
                }
            }

            return fileName;
        }

	}
}
