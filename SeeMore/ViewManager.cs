﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml;

namespace SeeMore {
    public class ViewManager {
        public static readonly HttpClient httpClient;
        private MainWindow window;
        private string dataDir;
        public object indexLock;
        public FeedsIndex index;
        private CollectionView collectionViews;
        private Dictionary<Guid, CollectionFeedView> guidToCollectionFeedView;
        private Dictionary<Guid, Feed> guidToFeed;
        private List<Guid> priorityUpdates;
        public CollectionFeedView selectedColl = null;
        private List<ArticleView> collectionArticles = null;
        public ArticleView selectedArt = null;

        static ViewManager() {
            httpClient = new HttpClient();
        }

        public ViewManager(MainWindow window, string dataDir) {
            this.window = window;
            this.dataDir = dataDir;
            this.indexLock = new object();
            this.index = new FeedsIndex();
            this.collectionViews = new CollectionView(Guid.NewGuid(), new Collection("All", ""));
            this.guidToCollectionFeedView = new Dictionary<Guid, CollectionFeedView>();
            this.guidToFeed = new Dictionary<Guid, Feed>();
            this.priorityUpdates = new List<Guid>();
            this.window.coll_list.ItemsSource = new ObservableCollection<CollectionFeedView> { this.collectionViews };
        }

        public void clear() {
            lock (this.indexLock) {
                this.dataDir = null;
                this.index = new FeedsIndex();
                this.collectionViews = new CollectionView(Guid.NewGuid(), new Collection("All", ""));
                this.guidToCollectionFeedView.Clear();
                this.guidToFeed.Clear();
                this.priorityUpdates.Clear();
                this.selectedColl = null;
                this.collectionArticles = null;
                this.selectedArt = null;
                this.window.coll_list.ItemsSource = new ObservableCollection<CollectionFeedView> { this.collectionViews };
            }
        }

        public static byte[] downloadImage(string url) {
            return httpClient.GetByteArrayAsync(url).Result;
        }

        public string getIndexPath(string dataDir = null) {
            if (dataDir == null) {
                dataDir = this.dataDir;
            }
            return Path.Join(dataDir, "index.dat");
        }

        public string getFeedPath(Guid guid, string dataDir = null) {
            if (dataDir == null) {
                dataDir = this.dataDir;
            }
            return Path.Join(dataDir, guid.ToString());
        }

        private void populateCounts(CollectionFeedView coll) {
            FeedView feedView = coll as FeedView;
            if (feedView != null) {
                coll.count = feedView.feed.articles.articles.Count;
                return;
            }
            coll.count = 0;
            foreach (CollectionFeedView child in coll.children) {
                this.populateCounts(child);
                coll.count += child.count;
            }
        }

        public void load(string dataDir) {
            // load index
            FeedsIndex index;
            string indexPath = this.getIndexPath(dataDir);
            DataContractSerializer serializer = new DataContractSerializer(typeof(FeedsIndex));
            using (FileStream f = new FileStream(indexPath, FileMode.OpenOrCreate)) {
                XmlDictionaryReader xmlReader = XmlDictionaryReader.CreateTextReader(f, new XmlDictionaryReaderQuotas());
                index = (FeedsIndex)(serializer.ReadObject(xmlReader, true));
            }

            // load collections
            CollectionView allColl = new CollectionView(Guid.NewGuid(), new Collection("All", ""));
            Dictionary<Guid, CollectionFeedView> guidToCollectionFeedView = new Dictionary<Guid, CollectionFeedView>();
            List<CollectionView> collViews = new List<CollectionView>();
            foreach (Guid key in index.collections.Keys) {
                CollectionView collView = new CollectionView(key, index.collections[key]);
                guidToCollectionFeedView[key] = collView;
                collViews.Add(collView);
            }

            // load feeds
            Dictionary<Guid, Feed> guidToFeed = new Dictionary<Guid, Feed>();
            List<FeedView> feedViews = new List<FeedView>();
            foreach (Guid key in index.feeds.Keys) {
                string feedPath = this.getFeedPath(key, dataDir);
                Feed feed = new Feed(feedPath, index.feeds[key]);
                guidToFeed[key] = feed;
                FeedView feedView = new FeedView(key, feed);
                feed.loadArticles();
                guidToCollectionFeedView[key] = feedView;
                feedViews.Add(feedView);
            }

            // populate feeds list
            collViews.Sort((x, y) => x.name.CompareTo(y.name));
            foreach (CollectionView collView in collViews) {
                CollectionFeedView parent = allColl;
                Guid? parentId = collView.collection.parent;
                if (parentId != null) {
                    parent = guidToCollectionFeedView[(Guid)parentId];
                }
                parent.children.Add(collView);
            }
            feedViews.Sort((x, y) => x.name.CompareTo(y.name));
            foreach (FeedView feedView in feedViews) {
                CollectionFeedView parent = allColl;
                Guid? parentId = feedView.feed.metadata.collection;
                if (parentId != null) {
                    parent = guidToCollectionFeedView[(Guid)parentId];
                }
                parent.children.Add(feedView);
            }
            this.populateCounts(allColl);

            // swap in what we've loaded
            lock (this.indexLock) {
                this.dataDir = dataDir;
                this.index = index;
                this.collectionViews = allColl;
                this.guidToCollectionFeedView = guidToCollectionFeedView;
                this.guidToFeed = guidToFeed;
                this.priorityUpdates.Clear();
                this.selectedColl = null;
                this.collectionArticles = null;
                this.selectedArt = null;
                this.window.coll_list.ItemsSource = new ObservableCollection<CollectionFeedView> { allColl };
            }
        }

        private void saveIndex() {
            // NOTE: caller must hold indexLock
            // write index to tmp path...
            string indexPath = this.getIndexPath();
            string indexPathTmp = indexPath + ".tmp";
            using (FileStream f = new FileStream(indexPathTmp, FileMode.Create)) {
                DataContractSerializer serializer = new DataContractSerializer(typeof(FeedsIndex));
                serializer.WriteObject(f, this.index);
            }
            // ...then move tmp file into place
            if (File.Exists(indexPath)) {
                File.Delete(indexPath);
            }
            File.Move(indexPathTmp, indexPath);
        }

        public void save() {
            Directory.CreateDirectory(this.dataDir);
            lock (this.indexLock) {
                // write articles
                foreach (Feed feed in this.guidToFeed.Values) {
                    feed.saveArticles();
                }
                // write index
                this.saveIndex();
            }
        }

        //TODO: update feed

        private int feedUpdateCompare(Feed x, Feed y) {
            int result = x.due.CompareTo(y.due);
            if (result != 0) {
                return result;
            }
            if (x.metadata.lastUpdated == null) {
                if (y.metadata.lastUpdated == null) {
                    return 0;
                }
                return -1;
            }
            if (y.metadata.lastUpdated == null) {
                return 1;
            }
            DateTimeOffset xUpdated = (DateTimeOffset)(x.metadata.lastUpdated);
            DateTimeOffset yUpdated = (DateTimeOffset)(y.metadata.lastUpdated);
            return xUpdated.CompareTo(yUpdated);
        }

        public Guid? updateNextFeed() {
            Guid guid;

            lock (this.indexLock) {
                //TODO: backload
                if (this.priorityUpdates.Count > 0) {
                    guid = this.priorityUpdates[0];
                    this.priorityUpdates.RemoveAt(0);
                }
                else {
                    List<Guid> feedIds = new List<Guid>(this.guidToFeed.Keys);
                    if (feedIds.Count <= 0) {
                        return null;
                    }
                    feedIds.Sort((x, y) => this.feedUpdateCompare(this.guidToFeed[x], this.guidToFeed[y]));
                    guid = feedIds[0];
                }
            }

            //TODO: this.updateFeed(guid)
            return guid;
        }

        //TODO: update loop

        private void populateArticles(List<ArticleView> articles, CollectionFeedView coll) {
            FeedView feed = coll as FeedView;
            if (feed != null) {
                foreach (Guid artId in feed.feed.articles.articles.Keys) {
                    Article art = feed.feed.articles.articles[artId];
                    articles.Add(new ArticleView(feed.guid, artId, art));
                }
            }
            if (coll.children != null) {
                foreach (CollectionFeedView child in coll.children) {
                    this.populateArticles(articles, child);
                }
            }
        }

        private int artviewCompare(ArticleView x, ArticleView y) {
            int result = y.article.timestamp.CompareTo(x.article.timestamp);
            if (result != 0) {
                return result;
            }
            result = x.title.CompareTo(y.title);
            if (result != 0) {
                return result;
            }
            return x.description.CompareTo(y.description);
        }

        public bool selectCollection(CollectionFeedView sel) {
            lock (this.indexLock) {
                if (sel == this.selectedColl) {
                    return false;
                }
                if (sel == null) {
                    this.selectedColl = null;
                    this.collectionArticles = null;
                    this.selectedArt = null;
                    this.window.art_list.ItemsSource = null;
                    return true;
                }
                List<ArticleView> articles = new List<ArticleView>();
                this.populateArticles(articles, sel);
                articles.Sort((x, y) => this.artviewCompare(x, y));
                this.collectionArticles = articles;
                this.window.art_list.ItemsSource = articles;
            }
            return true;
        }

        public Guid addCollection(Collection coll) {
            Guid guid = Guid.NewGuid();
            CollectionView collView = new CollectionView(guid, coll);
            lock (this.indexLock) {
                this.index.collections[guid] = coll;
                this.guidToCollectionFeedView[guid] = collView;
                CollectionFeedView parent = this.collectionViews;
                if (coll.parent != null) {
                    parent = this.guidToCollectionFeedView[(Guid)(coll.parent)];
                }
                List<CollectionFeedView> siblings = new List<CollectionFeedView>(parent.children);
                int insertIdx = siblings.FindIndex((x) => (x is FeedView) ||(x.name.CompareTo(coll.name) > 0));
                if (insertIdx < 0) {
                    insertIdx = siblings.Count;
                }
                parent.children.Insert(insertIdx, collView);
                //TODO: this.saveIndex();
            }
            return guid;
        }

        public Guid addFeed(FeedMetadata metadata) {
            Guid guid = Guid.NewGuid();
            Feed feed = new Feed(this.getFeedPath(guid), metadata);
            FeedView feedView = new FeedView(guid, feed);
            lock (this.indexLock) {
                this.index.feeds[guid] = metadata;
                this.guidToFeed[guid] = feed;
                CollectionFeedView parent = this.collectionViews;
                if (metadata.collection != null) {
                    parent = this.guidToCollectionFeedView[(Guid)(metadata.collection)];
                }
                List<CollectionFeedView> siblings = new List<CollectionFeedView>(parent.children);
                int insertIdx = siblings.FindIndex((x) => (x is FeedView) && (x.name.CompareTo(metadata.name) > 0));
                if (insertIdx < 0) {
                    insertIdx = siblings.Count;
                }
                parent.children.Insert(insertIdx, feedView);
                //TODO: this.saveIndex();
                //TODO: set up backload
            }
            return guid;
        }

        //TODO: editCollectionFeed, moveCollectionFeed, removeCollectionFeed

        public ArticleView selectArticle(int idx) {
            ArticleView sel = null;
            lock (this.indexLock) {
                if ((idx >= 0) && (this.collectionArticles != null) && (idx < this.collectionArticles.Count)) {
                    sel = this.collectionArticles[idx];
                }
                this.selectedArt = sel;
            }
            return sel;
        }
    }
}
