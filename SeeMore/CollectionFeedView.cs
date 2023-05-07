using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace SeeMore {
    public abstract class CollectionFeedView : INotifyPropertyChanged {
        protected bool _is_selected = false;
        public Guid guid;
        protected BitmapSource _icon;
        protected int _count = 0;
        protected ObservableCollection<CollectionFeedView> _children;

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public virtual bool is_expanded {
            get => false;
            set { }
        }
        public bool is_selected {
            get => this._is_selected;
            set {
                this._is_selected = value;
                this.NotifyPropertyChanged();
            }
        }
        public CollectionFeedView ident { get => this; }
        public abstract string name { get; set; }
        public abstract string description { get; }
        public BitmapSource icon {
            get => this._icon;
            set {
                this._icon = value;
                this.NotifyPropertyChanged();
            }
        }
        protected abstract byte[] raw_icon { get; set; }
        public int count {
            get => this._count;
            set {
                this._count = value;
                this.NotifyPropertyChanged();
            }
        }
        public ObservableCollection<CollectionFeedView> children { get => this._children; }

        public CollectionFeedView(Guid guid) {
            this.guid = guid;
            this._children = null;
        }

        protected void set_icon() {
            if (this.raw_icon == null) {
                this.icon = null;
                return;
            }
            this.icon = ViewManager.getImageSource(this.raw_icon);
        }
    }

    public class CollectionView : CollectionFeedView {
        protected bool _is_expanded = true;
        public Collection collection;

        public override bool is_expanded {
            get => this._is_expanded;
            set {
                this._is_expanded = value;
                this.NotifyPropertyChanged();
            }
        }
        public override string name {
            get => this.collection.name;
            set {
                this.collection.name = value;
                this.NotifyPropertyChanged();
            }
        }
        public override string description { get => this.collection.description; }
        protected override byte[] raw_icon {
            get => this.collection.icon;
            set {
                this.collection.icon = value;
                this.set_icon();
            }
        }

        public CollectionView(Guid ident, Collection collection) : base(ident) {
            this.collection = collection;
            this._children = new ObservableCollection<CollectionFeedView>();
            this.set_icon();
        }
    }

    public class FeedView : CollectionFeedView {
        public Feed feed;

        public override string name {
            get => this.feed.metadata.name;
            set {
                this.feed.metadata.name = value;
                this.NotifyPropertyChanged();
            }
        }
        public override string description { get => this.feed.metadata.description; }
        protected override byte[] raw_icon {
            get => this.feed.metadata.icon;
            set {
                this.feed.metadata.icon = value;
                this.set_icon();
            }
        }

        public FeedView(Guid ident, Feed feed) : base(ident) {
            this.feed = feed;
            this.set_icon();
        }
    }
}
