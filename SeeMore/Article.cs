using System;
using System.Runtime.Serialization;

namespace SeeMore {
    [KnownType(typeof(YouTubeArticle))]
    [Serializable]
    public class Article {
        public string id;
        public DateTimeOffset timestamp;
        public string title;
        public string description;
        public string url;

        public Article(string id, DateTimeOffset timestamp, string title, string description, string url) {
            this.id = id;
            this.timestamp = timestamp;
            this.title = title;
            this.description = description;
            this.url = url;
        }
    }
}
