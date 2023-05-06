using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.ServiceModel.Syndication;
using System.Xml;

namespace SeeMore {
    [Serializable]
    public class FeedArticles {
        public Dictionary<Guid, Article> articles;
        public Dictionary<string, Guid> idToGuid;

        public FeedArticles() {
            this.articles = new Dictionary<Guid, Article>();
            this.idToGuid = new Dictionary<string, Guid>();
        }
    }

    public class Feed {
        public string pathBase;
        public FeedMetadata metadata;
        public FeedArticles articles;
        public HashSet<Guid> deleted;

        public DateTimeOffset due {
            get {
                if (this.metadata.lastUpdated == null) {
                    return DateTimeOffset.MinValue;
                }
                return (DateTimeOffset)(this.metadata.lastUpdated) + this.metadata.updateInterval;
            }
        }

        public Feed(string pathBase, FeedMetadata metadata) {
            this.pathBase = pathBase;
            this.metadata = metadata;
            this.articles = new FeedArticles();
            this.deleted = new HashSet<Guid>();
        }

        public void loadArticles(Stream articleFile, Stream deletedFile) {
            // load articles
            if (articleFile.Length > 0) {
                DataContractSerializer serializer = new DataContractSerializer(typeof(FeedArticles));
                XmlDictionaryReader xmlReader = XmlDictionaryReader.CreateTextReader(articleFile, new XmlDictionaryReaderQuotas());
                FeedArticles loadedArticles = (FeedArticles)(serializer.ReadObject(xmlReader, true));
                foreach (Guid key in loadedArticles.articles.Keys) {
                    if (this.articles.articles.ContainsKey(key)) {
                        continue;
                    }
                    this.articles.articles[key] = loadedArticles.articles[key];
                }
                foreach (string key in loadedArticles.idToGuid.Keys) {
                    this.articles.idToGuid[key] = loadedArticles.idToGuid[key];
                }
            }

            // load deleted list
            StreamReader txtReader = new StreamReader(deletedFile);
            string line;
            while ((line = txtReader.ReadLine()) != null) {
                try {
                    this.deleted.Add(new Guid(line));
                }
                catch (Exception e) when ((e is ArgumentException) || (e is FormatException) || (e is OverflowException)) {
                    // ignore bad GUIDs
                }
            }
        }

        public void loadArticles() {
            string artPath = this.pathBase + ".dat";
            string delPath = this.pathBase + ".del";
            using (FileStream af = new FileStream(artPath, FileMode.OpenOrCreate), df = new FileStream(delPath, FileMode.OpenOrCreate)) {
                this.loadArticles(af, df);
            }
        }

        public void saveArticles(Stream articleFile, Stream deletedFile) {
            // prune deleted articles (don't delete from idToGuid here; we use that to track ids we've seen)
            foreach (Guid delKey in this.deleted) {
                if (this.articles.articles.ContainsKey(delKey)) {
                    this.articles.articles.Remove(delKey);
                }
            }

            // save articles
            DataContractSerializer serializer = new DataContractSerializer(typeof(FeedArticles));
            serializer.WriteObject(articleFile, this.articles);

            // deleted list is empty, so just truncate file
            deletedFile.SetLength(0);
        }

        public void saveArticles() {
            string artPath = this.pathBase + ".dat";
            string delPath = this.pathBase + ".del";
            // write to tmp files...
            string artPathTmp = artPath + ".tmp";
            string delPathTmp = delPath + ".tmp";
            using (FileStream af = new FileStream(artPathTmp, FileMode.Create), df = new FileStream(delPathTmp, FileMode.Create)) {
                this.saveArticles(af, df);
            }
            // ...then move tmp files into place
            if (File.Exists(artPath)) {
                File.Delete(artPath);
            }
            File.Move(artPathTmp, artPath);
            if (File.Exists(delPath)) {
                File.Delete(delPath);
            }
            File.Move(delPathTmp, delPath);
        }

        public void deleteArticle(Guid guid) {
            string delPath = this.pathBase + ".del";
            this.deleted.Add(guid);
            using (FileStream f = new FileStream(delPath, FileMode.Append)) {
                StreamWriter writer = new StreamWriter(f);
                writer.WriteLine(guid.ToString());
            }
        }

        public static FeedMetadata getMetadata(string url) {
            SyndicationFeed feed;
            using (XmlReader reader = XmlReader.Create(url)) {
                feed = SyndicationFeed.Load(reader);
            }
            return new FeedMetadata(feed.Title.Text, feed.Description.Text, url);
        }

        public virtual void backLoad() { }

        public virtual bool update() {
            bool modified = false;

            SyndicationFeed feed;
            using (XmlReader reader = XmlReader.Create(this.metadata.url)) {
                feed = SyndicationFeed.Load(reader);
            }
            Dictionary<string, Guid> idToGuid = new Dictionary<string, Guid>();
            foreach (SyndicationItem item in feed.Items) {
                Guid guid = Guid.NewGuid();

                // skip articles we've seen before unless they've been updated
                if (this.articles.idToGuid.ContainsKey(item.Id)) {
                    guid = this.articles.idToGuid[item.Id];
                    // maintain reference to deleted articles still in the feed
                    idToGuid[item.Id] = guid;
                    if (!this.articles.articles.ContainsKey(guid)) {
                        continue;
                    }
                    Article existingArticle = this.articles.articles[guid];
                    if (existingArticle.timestamp >= item.LastUpdatedTime) {
                        continue;
                    }
                }

                // add article
                Article article = this.syndicationItemToArticle(item);
                this.articles.articles[guid] = article;
                idToGuid[item.Id] = guid;
                modified = true;
            }

            this.metadata.lastUpdated = DateTimeOffset.UtcNow;

            // rebuild idToGuid for all existing articles (plus deleted ones added above)
            foreach (Guid key in this.articles.articles.Keys) {
                idToGuid[this.articles.articles[key].id] = key;
            }
            if (idToGuid.Count != this.articles.idToGuid.Count) {
                modified = true;
            }
            else {
                foreach (string key in idToGuid.Keys) {
                    if ((!this.articles.idToGuid.ContainsKey(key)) || (idToGuid[key] != this.articles.idToGuid[key])) {
                        modified = true;
                        break;
                    }
                }
            }
            this.articles.idToGuid = idToGuid;

            return modified;
        }

        public virtual Article syndicationItemToArticle(SyndicationItem item) {
            string url = null;
            if ((item.Links != null) && (item.Links.Count > 0)) {
                url = item.Links[0].Uri.ToString();
            }
            return new Article(item.Id, item.LastUpdatedTime, item.Title?.Text, item.Summary?.Text, url);
        }
    }
}
