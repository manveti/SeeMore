using System;
using System.Collections.Generic;

namespace SeeMore {
    [Serializable]
    public class FeedsIndex {
        public Dictionary<Guid, Collection> collections;
        public Dictionary<Guid, FeedMetadata> feeds;

        public FeedsIndex() {
            this.collections = new Dictionary<Guid, Collection>();
            this.feeds = new Dictionary<Guid, FeedMetadata>();
        }
    }
}
