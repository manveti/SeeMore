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
            using (Stream str = HttpUtils.openStream(url)) {
                using (XmlReader reader = XmlReader.Create(str)) {
                    feed = SyndicationFeed.Load(reader);
                }
            }
            //TODO: fall back to ${site_url}/favicon.ico
            byte[] icon = HttpUtils.downloadFile(feed.ImageUrl?.AbsoluteUri);
            return new FeedMetadata(feed.Title?.Text, feed.Description?.Text, url, icon);
        }

        public virtual FeedArticles getBackloadArticles() {
            return null;
        }

        public FeedArticles getUpdateArticles() {
            FeedArticles updateArticles = new FeedArticles();

            SyndicationFeed feed;
            using (Stream str = HttpUtils.openStream(this.metadata.url)) {
                using (XmlReader reader = XmlReader.Create(str)) {
                    feed = SyndicationFeed.Load(reader);
                }
            }
            foreach (SyndicationItem item in feed.Items) {
                Guid guid = Guid.NewGuid();

                // skip articles we've seen before unless they've been updated
                if (this.articles.idToGuid.ContainsKey(item.Id)) {
                    guid = this.articles.idToGuid[item.Id];
                    // maintain reference to deleted articles still in the feed
                    updateArticles.idToGuid[item.Id] = guid;
                    if ((this.deleted.Contains(guid)) || (!this.articles.articles.ContainsKey(guid))) {
                        continue;
                    }
                    Article existingArticle = this.articles.articles[guid];
                    if (existingArticle.timestamp >= item.LastUpdatedTime) {
                        continue;
                    }
                }

                // add article
                Article article = this.syndicationItemToArticle(item);
                updateArticles.articles[guid] = article;
                updateArticles.idToGuid[item.Id] = guid;
            }

            this.metadata.lastUpdated = DateTimeOffset.UtcNow;

            return updateArticles;
        }

        public bool applyUpdate(FeedArticles updateArticles, bool pruneDeleted = true) {
            // NOTE: only call this function from a locked context
            bool modified = updateArticles.articles.Count > 0;

            // add pre-existing articles to idToGuid
            HashSet<Guid> removeArticles = new HashSet<Guid>();
            foreach (Guid key in this.articles.articles.Keys) {
                if ((pruneDeleted) && (this.deleted.Contains(key))) {
                    removeArticles.Add(key);
                    modified = true;
                }
                else {
                    updateArticles.idToGuid[this.articles.articles[key].id] = key;
                }
            }
            if (pruneDeleted) {
                // prune deleted articles that aren't in the feed anymore
                foreach (Guid key in removeArticles) {
                    this.articles.articles.Remove(key);
                }
            }

            // add new articles
            foreach (Guid key in updateArticles.articles.Keys) {
                this.articles.articles[key] = updateArticles.articles[key];
            }
            this.articles.idToGuid = updateArticles.idToGuid;

            return modified;
        }

        public virtual Article syndicationItemToArticle(SyndicationItem item) {
            DateTimeOffset timestamp = item.LastUpdatedTime;
            string description = item.Summary?.Text;
            string url = null;
            if (item.PublishDate > timestamp) {
                timestamp = item.PublishDate;
            }
            if ((item.Content is TextSyndicationContent txtContent) && (txtContent.Text != null) && (txtContent.Text.Length > 0)) {
                description = txtContent.Text;
            }
            if ((item.Links != null) && (item.Links.Count > 0)) {
                url = item.Links[0].Uri.AbsoluteUri;
            }
            return new Article(item.Id, timestamp, item.Title?.Text, description, url);
        }
    }
}
