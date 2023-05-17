using System;
using System.IO;
using System.ServiceModel.Syndication;
using System.Xml;

using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;

namespace SeeMore {
    [Serializable]
    public class YouTubeChannelMetadata : FeedMetadata {
        public string channelId;
        public string uploadsId;
        public string backloadPageToken;

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
            this.backloadPageToken = null;
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
        private const int MAX_API_RESULTS = 50;

        public YouTubeFeed(string pathBase, YouTubeChannelMetadata metadata) : base(pathBase, metadata) { }

        private static YouTubeService getYouTubeClient() {
            BaseClientService.Initializer initializer = new BaseClientService.Initializer() {
                ApiKey = MainWindow.config.youTubeApiKey,
                ApplicationName = "SeeMore",
            };
            return new YouTubeService(initializer);
        }

        public static YouTubeChannelMetadata getMetadataByChannelId(string channelId) {
            string name;
            string description;
            string uploadsId;
            byte[] icon;
            using (YouTubeService client = getYouTubeClient()) {
                ChannelsResource.ListRequest request = client.Channels.List("snippet,contentDetails");
                request.Id = channelId;
                ChannelListResponse resp = request.Execute();
                if ((resp.Items == null) || (resp.Items.Count <= 0)) {
                    return null;
                }
                name = resp.Items[0].Snippet.Title;
                description = resp.Items[0].Snippet.Description;
                uploadsId = resp.Items[0].ContentDetails.RelatedPlaylists.Uploads;
                icon = HttpUtils.downloadFile(resp.Items[0].Snippet.Thumbnails.Default__?.Url);
            }
            string url = "http://www.youtube.com/feeds/videos.xml?channel_id=" + channelId;
            return new YouTubeChannelMetadata(name, description, url, channelId, uploadsId, icon);
        }

        public static YouTubeChannelMetadata getMetadataByPlaylistId(string playlistId) {
            string channelId;
            using (YouTubeService client = getYouTubeClient()) {
                PlaylistsResource.ListRequest request = client.Playlists.List("snippet");
                request.Id = playlistId;
                PlaylistListResponse resp = request.Execute();
                if ((resp.Items == null) || (resp.Items.Count <= 0)) {
                    return null;
                }
                channelId = resp.Items[0].Snippet.ChannelId;
            }
            return getMetadataByChannelId(channelId);
        }

        public static YouTubeChannelMetadata getMetadataFromVideo(string videoId) {
            // allow videoId to be a full video URL
            int idx = videoId.LastIndexOf('=');
            if (idx > 0) {
                videoId = videoId.Substring(idx + 1);
            }
            string channelId;
            using (YouTubeService client = getYouTubeClient()) {
                VideosResource.ListRequest request = client.Videos.List("snippet");
                request.Id = videoId;
                VideoListResponse resp = request.Execute();
                if ((resp.Items == null) || (resp.Items.Count <= 0)) {
                    return null;
                }
                channelId = resp.Items[0].Snippet.ChannelId;
            }
            return getMetadataByChannelId(channelId);
        }

        public static new FeedMetadata getMetadata(string url) {
            SyndicationFeed feed;
            using (Stream str = HttpUtils.openStream(url)) {
                using (XmlReader reader = XmlReader.Create(str)) {
                    feed = SyndicationFeed.Load(reader);
                }
            }
            foreach (SyndicationItem item in feed.Items) {
                foreach (SyndicationElementExtension ext in item.ElementExtensions) {
                    XmlElement element = ext.GetObject<XmlElement>();
                    if (element.Name == "yt:channelId") {
                        return getMetadataByChannelId(element.InnerText);
                    }
                }
            }
            return null;
        }

        public override FeedArticles getBackloadArticles() {
            if (!this.metadata.needBackload) {
                return null;
            }

            FeedArticles backloadArticles = new FeedArticles();

            YouTubeChannelMetadata metadata = (YouTubeChannelMetadata)(this.metadata);
            string pageToken = metadata.backloadPageToken;
            if (pageToken == null) {
                pageToken = "";
            }
            using (YouTubeService client = getYouTubeClient()) {
                PlaylistItemsResource.ListRequest request = client.PlaylistItems.List("snippet");
                request.PlaylistId = metadata.uploadsId;
                request.MaxResults = MAX_API_RESULTS;
                request.PageToken = pageToken;
                PlaylistItemListResponse response = request.Execute();
                foreach (PlaylistItem item in response.Items) {
                    Guid guid = Guid.NewGuid();
                    string videoId = item.Snippet.ResourceId.VideoId;
                    string articleId = "yt:video:" + videoId;

                    // skip videos we've seen before
                    if (this.articles.idToGuid.ContainsKey(articleId)) {
                        continue;
                    }

                    // add article
                    DateTimeOffset timestamp = (DateTimeOffset)(item.Snippet.PublishedAt);
                    string title = item.Snippet.Title;
                    string description = item.Snippet.Description;
                    ThumbnailDetails thumbnails = item.Snippet.Thumbnails;
                    Thumbnail thumb = thumbnails.Standard ?? thumbnails.High ?? thumbnails.Medium ?? thumbnails.Default__;
                    byte[] thumbnail = HttpUtils.downloadFile(thumb?.Url);
                    string url = "https://www.youtube.com/watch?v=" + videoId;
                    YouTubeArticle article = new YouTubeArticle(articleId, timestamp, title, description, url, videoId, thumbnail);
                    backloadArticles.articles[guid] = article;
                    backloadArticles.idToGuid[articleId] = guid;
                }
                pageToken = response.NextPageToken;
            }

            metadata.backloadPageToken = pageToken;
            if (pageToken == null) {
                metadata.needBackload = false;
            }

            return backloadArticles;
        }

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
