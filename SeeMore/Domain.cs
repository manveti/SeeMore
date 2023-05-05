using System;
using System.Collections.Generic;

namespace SeeMore {
    [Serializable]
    public class Domain {
        public Dictionary<Guid, Collection> collections;
        public Dictionary<Guid, FeedMetadata> feeds;

        public Domain() {
            this.collections = new Dictionary<Guid, Collection>();
            this.feeds = new Dictionary<Guid, FeedMetadata>();
        }
    }
}
