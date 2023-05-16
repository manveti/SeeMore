using System;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace SeeMore {
    [Serializable]
    public class Config {
        [OptionalField]
        public string browserPath;
        [OptionalField]
        public string browserArgs;
        public string dataDir;
        public Guid? defaultCollection;
        public string userAgent;
        public string youTubeApiKey;

        public Config() {
            this.reset();
        }

        public void reset() {
            this.dataDir = Directory.GetCurrentDirectory();
            this.defaultCollection = null;
            this.userAgent = HttpUtils.DEFAULT_USER_AGENT;
            this.youTubeApiKey = null;
        }

        public static Config loadConfig(Stream configFile) {
            DataContractSerializer serializer = new DataContractSerializer(typeof(Config));
            XmlDictionaryReader xmlReader = XmlDictionaryReader.CreateTextReader(configFile, new XmlDictionaryReaderQuotas());
            return (Config)(serializer.ReadObject(xmlReader, true));
        }

        public static Config loadConfig(string settingsDir) {
            Directory.CreateDirectory(settingsDir);
            string path = Path.Join(settingsDir, "config.xml");
            if (!File.Exists(path)) {
                return new Config();
            }
            using (FileStream f = new FileStream(path, FileMode.Open)) {
                return Config.loadConfig(f);
            }
        }

        public void saveConfig(Stream configFile) {
            DataContractSerializer serializer = new DataContractSerializer(typeof(Config));
            serializer.WriteObject(configFile, this);
        }

        public void saveConfig(string settingsDir) {
            Directory.CreateDirectory(settingsDir);
            string path = Path.Join(settingsDir, "config.xml");
            using (FileStream f = new FileStream(path, FileMode.Create)) {
                this.saveConfig(f);
            }
        }
    }
}
