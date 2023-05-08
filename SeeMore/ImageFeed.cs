using System;
using System.ServiceModel.Syndication;
using System.Xml;

namespace SeeMore {
    [Serializable]
    public class ImageFeedMetadata : FeedMetadata {
        public ImageFeedMetadata(
            string name, string description, string url, byte[] icon = null, TimeSpan? updateInterval = null, Guid? collection = null
        ) : base(name, description, url, icon, updateInterval, collection) { }

        public ImageFeedMetadata(FeedMetadata md) : base(md.name, md.description, md.url, md.icon, md.updateInterval, md.collection) { }

        public override Feed constructFeed(string pathBase) {
            return new ImageFeed(pathBase, this);
        }
    }

    [Serializable]
    public class ImageArticle : Article {
        public byte[] image;

        public ImageArticle (
            string id, DateTimeOffset timestamp, string title, string description, string url, byte[] image = null
        ) : base(id, timestamp, title, description, url) {
            this.image = image;
        }
    }

    public class ImageFeed : Feed {
        public ImageFeed(string pathBase, ImageFeedMetadata metadata) : base(pathBase, metadata) { }

        public static new ImageFeedMetadata getMetadata(string url) {
            return new ImageFeedMetadata(Feed.getMetadata(url));
        }

        public override Article syndicationItemToArticle(SyndicationItem item) {
            Article article = base.syndicationItemToArticle(item);
            byte[] image = null;
            foreach (SyndicationElementExtension ext in item.ElementExtensions) {
                XmlElement element = ext.GetObject<XmlElement>();
                if (element.Name == "media:thumbnail") {
                    XmlNode urlNode = element.Attributes.GetNamedItem("url");
                    if (urlNode != null) {
                        image = ViewManager.downloadImage(urlNode.Value);
                    }
                }
            }
            return new ImageArticle(article.id, article.timestamp, article.title, article.description, article.url, image);
        }
    }
}
