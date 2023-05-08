using System;
using System.ServiceModel.Syndication;

namespace SeeMore {
    [Serializable]
    public class HtmlFeedMetadata : FeedMetadata {
        public HtmlFeedMetadata(
            string name, string description, string url, byte[] icon = null, TimeSpan? updateInterval = null, Guid? collection = null
        ) : base(name, description, url, icon, updateInterval, collection) { }

        public HtmlFeedMetadata(FeedMetadata md) : base(md.name, md.description, md.url, md.icon, md.updateInterval, md.collection) { }

        public override Feed constructFeed(string pathBase) {
            return new HtmlFeed(pathBase, this);
        }
    }

    [Serializable]
    public class HtmlArticle : Article {
        public HtmlArticle(
            string id, DateTimeOffset timestamp, string title, string description, string url
        ) : base(id, timestamp, title, description, url) { }
    }


    public class HtmlFeed : Feed {
        public HtmlFeed(string pathBase, HtmlFeedMetadata metadata) : base(pathBase, metadata) { }

        public static new HtmlFeedMetadata getMetadata(string url) {
            return new HtmlFeedMetadata(Feed.getMetadata(url));
        }

        public override Article syndicationItemToArticle(SyndicationItem item) {
            Article article = base.syndicationItemToArticle(item);
            return new HtmlArticle(article.id, article.timestamp, article.title, article.description, article.url);
        }
    }
}
