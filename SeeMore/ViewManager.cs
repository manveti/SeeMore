using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Xml;

namespace SeeMore {
    public class ViewManager {
        private readonly MainWindow window;
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

        public static BitmapSource getImageSource(byte[] image) {
            if (image == null) {
                return null;
            }
            using (MemoryStream stream = new MemoryStream(image)) {
                BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                return decoder.Frames[0];
            }
        }

        private string getIndexPath(string dataDir = null) {
            if (dataDir == null) {
                dataDir = this.dataDir;
            }
            return Path.Join(dataDir, "index.dat");
        }

        private string getFeedPath(Guid guid, string dataDir = null) {
            if (dataDir == null) {
                dataDir = this.dataDir;
            }
            return Path.Join(dataDir, guid.ToString());
        }

        private void populateCounts(CollectionFeedView coll) {
            int count = 0;
            if (coll is FeedView feedView) {
                foreach (Guid key in feedView.feed.articles.articles.Keys) {
                    if (!feedView.feed.deleted.Contains(key)) {
                        count += 1;
                    }
                }
                coll.count = count;
                return;
            }
            foreach (CollectionFeedView child in coll.children) {
                this.populateCounts(child);
                coll.count += child.count;
            }
            coll.count = count;
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

        //TODO: update feed: upd=feed.getUpdateArticles(); lock { mod=feed.applyUpdate(upd); save if mod }

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

        public CollectionFeedView getCollectionFeedView(Guid? guid) {
            if (guid == null) {
                return null;
            }
            lock (this.indexLock) {
                if (this.guidToCollectionFeedView.ContainsKey((Guid)guid)) {
                    return this.guidToCollectionFeedView[(Guid)guid];
                }
            }
            return null;
        }

        private void populateArticles(List<ArticleView> articles, CollectionFeedView coll) {
            if (coll is FeedView feed) {
                foreach (Guid artId in feed.feed.articles.articles.Keys) {
                    if (feed.feed.deleted.Contains(artId)) {
                        continue;
                    }
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
                this.selectedColl = sel;
                if (sel == null) {
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
                int insertIdx = siblings.FindIndex((x) => (x is FeedView) || (x.name.CompareTo(coll.name) > 0));
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
            Feed feed = metadata.constructFeed(this.getFeedPath(guid));
            FeedView feedView = new FeedView(guid, feed);
            lock (this.indexLock) {
                this.index.feeds[guid] = metadata;
                this.guidToCollectionFeedView[guid] = feedView;
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

        public static void setBrowserContents(WebBrowser browser, string contents) {
            if ((contents == null) || (contents.Length <= 0)) {
                browser.Navigate("about:blank");
            }
            else {
                browser.NavigateToString(contents);
            }
        }

        public ArticleView selectArticle(int idx) {
            ArticleView sel = null;
            lock (this.indexLock) {
                if ((idx >= 0) && (this.collectionArticles != null) && (idx < this.collectionArticles.Count)) {
                    sel = this.collectionArticles[idx];
                }
                if (sel == this.selectedArt) {
                    return sel;
                }
                this.selectedArt = sel;
            }

            // display article contents
            this.window.hide_article_content();
            if (sel == null) {
                return sel;
            }
            if (sel.article is HtmlArticle htmlArt) {
                ViewManager.setBrowserContents(this.window.content_box_html.content_browser, htmlArt.description);
                this.window.content_box_html.Visibility = Visibility.Visible;
                return sel;
            }
            if (sel.article is ImageArticle imgArt) {
                this.window.content_box_image.image.Source = ViewManager.getImageSource(imgArt.image);
                this.window.content_box_image.desc_box.Text = imgArt.description;
                this.window.content_box_image.Visibility = Visibility.Visible;
                return sel;
            }
            if (sel.article is YouTubeArticle ytArt) {
                this.window.content_box_image.image.Source = ViewManager.getImageSource(ytArt.thumbnail);
                this.window.content_box_image.desc_box.Text = ytArt.description;
                this.window.content_box_image.Visibility = Visibility.Visible;
                return sel;
            }
            this.window.content_box_default.desc_box.Text = sel.article.description;
            this.window.content_box_default.Visibility = Visibility.Visible;
            return sel;
        }

        public void addItem() {
            AddEditWindow dlg = new AddEditWindow("Add...");
            dlg.Owner = this.window;
            dlg.type_box.SelectedValue = AddEditWindow.TYPE_COLLECTION;
            dlg.interval_box.Text = FeedMetadata.DEFAULT_UPDATE.ToString();
            Guid? parentGuid = null;
            if ((this.selectedColl != null) && (this.selectedColl is CollectionView)) {
                parentGuid = this.selectedColl.guid;
            }
            CollectionView collSnapshot;
            lock (this.indexLock) {
                collSnapshot = this.collectionViews.copy_collections(parentGuid);
            }
            if (parentGuid == null) {
                collSnapshot.is_selected = true;
            }
            dlg.collections_lst.ItemsSource = new ObservableCollection<CollectionFeedView> { collSnapshot };
            dlg.ShowDialog();
            if (!dlg.valid) {
                return;
            }

            string selType = (string)(dlg.type_box.SelectedValue);
            string url = dlg.url_box.Text;
            string name = dlg.name_box.Text;
            //TODO: validate name
            string desc = dlg.desc_box.Text;
            byte[] icon = dlg.icon;
            TimeSpan? updateInterval = null;
            Guid? parent = null;
            CollectionFeedView selParent = dlg.collections_lst.SelectedItem as CollectionFeedView;
            if ((selParent != null) && (selParent != collSnapshot)) {
                parent = selParent.guid;
            }

            if (selType != AddEditWindow.TYPE_COLLECTION) {
                //TODO: validate url
                string updateStr = dlg.interval_box.Text;
                TimeSpan ts;
                if (TimeSpan.TryParse(updateStr, out ts)) {
                    updateInterval = ts;
                }
                //TODO: backload
            }

            FeedMetadata feed;
            switch (selType) {
            case AddEditWindow.TYPE_COLLECTION:
                Collection coll = new Collection(name, desc, icon, parent);
                this.addCollection(coll);
                return;
            case AddEditWindow.TYPE_FEED:
                feed = new FeedMetadata(name, desc, url, icon, updateInterval, parent);
                break;
            case AddEditWindow.TYPE_HTML_FEED:
                feed = new HtmlFeedMetadata(name, desc, url, icon, updateInterval, parent);
                break;
            case AddEditWindow.TYPE_YOUTUBE_FEED:
                string channelId = dlg.channel_id_box.Text;
                //TODO: validate channel id
                string uploadsId = channelId;
                //TODO: uploads id
                feed = new YouTubeChannelMetadata(name, desc, url, channelId, uploadsId, icon, updateInterval, parent);
                break;
            default:
                //TODO: error
                return;
            }
            this.addFeed(feed);
        }

        public void editItem() {
            if (this.selectedColl == null) {
                return;
            }
            AddEditWindow dlg = new AddEditWindow("Edit...");
            dlg.Owner = this.window;
            if (this.selectedColl is CollectionView) {
                dlg.type_box.SelectedValue = AddEditWindow.TYPE_COLLECTION;
            }
            else if (this.selectedColl is FeedView feedView) {
                if (feedView.feed is HtmlFeed) {
                    dlg.type_box.SelectedValue = AddEditWindow.TYPE_HTML_FEED;
                }
                else if (feedView.feed is YouTubeFeed youTubeFeed) {
                    dlg.type_box.SelectedValue = AddEditWindow.TYPE_YOUTUBE_FEED;
                    dlg.channel_id_box.Text = ((YouTubeChannelMetadata)(youTubeFeed.metadata)).channelId;
                    dlg.video_id_lbl.Visibility = Visibility.Collapsed;
                    dlg.video_id_box.Visibility = Visibility.Collapsed;
                }
                else {
                    dlg.type_box.SelectedValue = AddEditWindow.TYPE_FEED;
                }
                dlg.url_box.Text = feedView.feed.metadata.url;
                dlg.interval_box.Text = feedView.feed.metadata.updateInterval.ToString();
                dlg.backload_lbl.Visibility = Visibility.Collapsed;
                dlg.backload_box.Visibility = Visibility.Collapsed;
            }
            else {
                return;
            }
            dlg.type_box.IsEnabled = false;
            dlg.name_box.Text = this.selectedColl.name;
            dlg.desc_box.Text = this.selectedColl.description;
            dlg.icon = this.selectedColl.raw_icon;
            dlg.icon_img.Source = this.selectedColl.icon;
            dlg.parent_lbl.Visibility = Visibility.Collapsed;
            dlg.collections_lst.Visibility = Visibility.Collapsed;
            dlg.ShowDialog();
            if (!dlg.valid) {
                return;
            }

            bool modified = false;

            string url = dlg.url_box.Text;
            string name = dlg.name_box.Text;
            //TODO: validate name
            string desc = dlg.desc_box.Text;
            byte[] icon = dlg.icon;

            if (this.selectedColl is FeedView fv) {
                //TODO: validate url
                TimeSpan updateInterval;
                string updateStr = dlg.interval_box.Text;
                if (!TimeSpan.TryParse(updateStr, out updateInterval)) {
                    //TODO: error
                }
                if (fv.feed is YouTubeFeed youTubeFeed) {
                    string channelId = dlg.channel_id_box.Text;
                    //TODO: validate channel id
                    string uploadsId = channelId;
                    //TODO: uploads id
                    YouTubeChannelMetadata ytMd = (YouTubeChannelMetadata)(youTubeFeed.metadata);
                    ytMd.channelId = channelId;
                    ytMd.uploadsId = uploadsId;
                }
                fv.feed.metadata.url = url;
                fv.feed.metadata.updateInterval = updateInterval;
            }
            if (name != this.selectedColl.name) {
                this.selectedColl.name = name;
                modified = true;
            }
            if (desc != this.selectedColl.description) {
                this.selectedColl.description = desc;
                modified = true;
            }
            if (icon != this.selectedColl.raw_icon) {
                this.selectedColl.raw_icon = icon;
                modified = true;
            }

            if (modified) {
                //TODO: this.saveIndex();
            }
        }

        public void moveItem() {
            if (this.selectedColl == null) {
                return;
            }
            Guid? parentGuid = null;
            if (this.selectedColl is CollectionView collView) {
                parentGuid = collView.collection.parent;
            }
            else if (this.selectedColl is FeedView feedView) {
                parentGuid = feedView.feed.metadata.collection;
            }
            else {
                return;
            }
            AddEditWindow dlg = new AddEditWindow("Move...");
            dlg.Owner = this.window;
            dlg.type_lbl.Visibility = Visibility.Collapsed;
            dlg.type_box.Visibility = Visibility.Collapsed;
            dlg.channel_id_lbl.Visibility = Visibility.Collapsed;
            dlg.channel_id_box.Visibility = Visibility.Collapsed;
            dlg.video_id_lbl.Visibility = Visibility.Collapsed;
            dlg.video_id_box.Visibility = Visibility.Collapsed;
            dlg.url_lbl.Visibility = Visibility.Collapsed;
            dlg.url_box.Visibility = Visibility.Collapsed;
            dlg.name_box.Text = this.selectedColl.name;
            dlg.name_box.IsEnabled = false;
            dlg.desc_lbl.Visibility = Visibility.Collapsed;
            dlg.desc_box.Visibility = Visibility.Collapsed;
            dlg.icon_lbl.Visibility = Visibility.Collapsed;
            dlg.icon_box.Visibility = Visibility.Collapsed;
            dlg.icon_img.Visibility = Visibility.Collapsed;
            dlg.interval_lbl.Visibility = Visibility.Collapsed;
            dlg.interval_box.Visibility = Visibility.Collapsed;
            dlg.backload_lbl.Visibility = Visibility.Collapsed;
            dlg.backload_box.Visibility = Visibility.Collapsed;
            CollectionView collSnapshot;
            lock (this.indexLock) {
                collSnapshot = this.collectionViews.copy_collections(parentGuid, this.selectedColl.guid);
            }
            if (parentGuid == null) {
                collSnapshot.is_selected = true;
            }
            dlg.collections_lst.ItemsSource = new ObservableCollection<CollectionFeedView> { collSnapshot };
            dlg.ShowDialog();
            if (!dlg.valid) {
                return;
            }

            Guid? newParent = null;
            CollectionFeedView selParent = dlg.collections_lst.SelectedItem as CollectionFeedView;
            if ((selParent != null) && (selParent != collSnapshot)) {
                newParent = selParent.guid;
            }
            if (newParent != parentGuid) {
                //TODO: move item
                //TODO: this.saveIndex();
            }
        }

        //TODO: remove item
    }
}
