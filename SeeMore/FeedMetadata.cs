using System;
using System.Runtime.Serialization;

namespace SeeMore {
    [KnownType(typeof(YouTubeChannelMetadata))]
    [Serializable]
    public class FeedMetadata {
        public static readonly TimeSpan DEFAULT_UPDATE = new TimeSpan(hours: 6, minutes: 0, seconds: 0);

        public string name;
        public string description;
        public string url;
        public byte[] icon;
        public TimeSpan updateInterval;
        public DateTimeOffset? lastUpdated;
        public Guid? collection;

        public FeedMetadata(
            string name, string description, string url, byte[] icon = null, TimeSpan? updateInterval = null, Guid? collection = null
        ) {
            if (updateInterval == null) {
                updateInterval = DEFAULT_UPDATE;
            }

            this.name = name;
            this.description = description;
            this.url = url;
            this.icon = icon;
            this.updateInterval = (TimeSpan)(updateInterval);
            this.lastUpdated = null;
            this.collection = collection;
        }
    }
}
