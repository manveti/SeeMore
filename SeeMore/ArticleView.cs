using System;

namespace SeeMore {
    public class ArticleView {
        public Guid feed;
        public Guid guid;
        public Article article;

        public string timestamp {
            get => this.article.timestamp.ToLocalTime().ToString("G");
        }
        public string title {
            get => this.article.title;
        }
        public string description {
            get => this.article.description;
        }

        public ArticleView(Guid feed, Guid guid, Article article) {
            this.feed = feed;
            this.guid = guid;
            this.article = article;
        }
    }
}
