using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace hds
{
    public class WorldConfig
    {
        private string filename;
        
        public string serverName;
        public string weather = "bluesky2";
        public bool IsPvpServer = false;
        public UInt16 PvpMaxSafeLevel = 16;
        public UInt16 FixedBinkIDOverride = 0;
        // Retail showed a character only the hardlines it had uploaded (6d lists of 1 to 52 per district). Off by
        // default: char_hardlines starts empty, so turning it on leaves existing characters with no hardlines
        // until they upload some.
        public bool HardlineUploadRequired = false;

        public Hashtable events = new Hashtable();

        public WorldConfig(string filename)
        {
            this.filename = filename;
            var xDoc = new XmlDocument();
            filename = ResolveConfigFile(filename);
            xDoc.Load(filename);
            
            serverName = xDoc.GetElementsByTagName("serverName")[0].InnerText;
            IsPvpServer = Boolean.Parse(xDoc.GetElementsByTagName("IsPvPServer")[0].InnerText);
            PvpMaxSafeLevel = UInt16.Parse(xDoc.GetElementsByTagName("PvPMaxSafeLevel")[0].InnerText);
            FixedBinkIDOverride = UInt16.Parse(xDoc.GetElementsByTagName("FixedBinkIDOverride")[0].InnerText);
            // Per-process overrides, so a test server can be a PvP server without its own configuration file
            string pvpServer = Environment.GetEnvironmentVariable("MXO_PVP_SERVER");
            if (!string.IsNullOrEmpty(pvpServer))
            {
                IsPvpServer = pvpServer == "1";
            }
            if (UInt16.TryParse(Environment.GetEnvironmentVariable("MXO_PVP_MAX_SAFE_LEVEL"), out UInt16 safeLevel))
            {
                PvpMaxSafeLevel = safeLevel;
            }
            XmlNodeList uploadRequired = xDoc.GetElementsByTagName("HardlineUploadRequired");
            if (uploadRequired.Count > 0)
            {
                HardlineUploadRequired = Boolean.Parse(uploadRequired[0].InnerText.Trim());
            }

            XmlNodeList eventList = xDoc.GetElementsByTagName("WorldEvents");
            foreach (XmlNode eventNode in eventList[0].ChildNodes)
            {
                if (eventNode.NodeType != XmlNodeType.Comment)
                {
                    events.Add(eventNode.Name, eventNode.InnerText);    
                }
                
            }

        }

        private static string ResolveConfigFile(string filename)
        {
            if (File.Exists(filename))
            {
                return filename;
            }

            if (filename.Equals("WorldConfig.xml", StringComparison.OrdinalIgnoreCase) &&
                File.Exists("WorldConfig.xml.dist"))
            {
                return "WorldConfig.xml.dist";
            }

            string outputPath = Path.Combine(AppContext.BaseDirectory, filename);
            if (File.Exists(outputPath))
            {
                return outputPath;
            }

            if (filename.Equals("WorldConfig.xml", StringComparison.OrdinalIgnoreCase))
            {
                string outputDistPath = Path.Combine(AppContext.BaseDirectory, "WorldConfig.xml.dist");
                if (File.Exists(outputDistPath))
                {
                    return outputDistPath;
                }
            }

            return filename;
        }
        
    }
}
