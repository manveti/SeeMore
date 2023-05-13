﻿using System;
using System.ServiceModel.Syndication;
using System.Xml;

namespace SeeMore {
    [Serializable]
    public class YouTubeChannelMetadata : FeedMetadata {
        public string channelId;
        public string uploadsId;
        //TODO: uploads playlist page token

        public YouTubeChannelMetadata(
            string name,
            string description,
            string url,
            string channelId,
            string uploadsId,
            byte[] icon = null,
            TimeSpan? updateInterval = null,
            Guid? collection = null
        ) : base(name, description, url, icon, updateInterval, collection) {
            this.channelId = channelId;
            this.uploadsId = uploadsId;
        }

        public override Feed constructFeed(string pathBase) {
            return new YouTubeFeed(pathBase, this);
        }
    }

    [Serializable]
    public class YouTubeArticle : Article {
        public string videoId;
        public byte[] thumbnail;

        public YouTubeArticle(
            string id, DateTimeOffset timestamp, string title, string description, string url, string videoId, byte[] thumbnail = null
        ) : base(id, timestamp, title, description, url) {
            this.videoId = videoId;
            this.thumbnail = thumbnail;
        }
    }

    public class YouTubeFeed : Feed {
        public YouTubeFeed(string pathBase, YouTubeChannelMetadata metadata) : base(pathBase, metadata) { }

        //TODO: getMetadata, getMetadataByChannelId, getMetadataFromVideo (url and/or id)

        public override void backLoad() { }//TODO: load prior content in subclasses where that makes sense
        //note that rss id is "yt:video:${videoId}"

        public override Article syndicationItemToArticle(SyndicationItem item) {
            Article article = base.syndicationItemToArticle(item);
            string description = article.description;
            string videoId = null;
            byte[] thumbnail = null;
            foreach (SyndicationElementExtension ext in item.ElementExtensions) {
                XmlElement element = ext.GetObject<XmlElement>();
                switch (element.Name) {
                case "media:group":
                    XmlNodeList children = element.GetElementsByTagName("media:description");
                    if ((children != null) && (children.Count > 0)) {
                        description = children[0].InnerText;
                    }
                    children = element.GetElementsByTagName("media:thumbnail");
                    if ((children != null) && (children.Count > 0)) {
                        XmlNode urlNode = children[0].Attributes.GetNamedItem("url");
                        if (urlNode != null) {
                            thumbnail = HttpUtils.downloadFile(urlNode.Value);
                        }
                    }
                    break;
                case "yt:videoId":
                    videoId = element.InnerText;
                    break;
                }
            }
            return new YouTubeArticle(article.id, article.timestamp, article.title, description, article.url, videoId, thumbnail);
        }
    }
}
