using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SeeMore {
    public abstract class CollectionFeedView : INotifyPropertyChanged {
        public bool _is_selected = false;
        public Guid guid;
        //TODO: count
        public ObservableCollection<CollectionFeedView> _children;

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
        //public Guid ident { get => this._ident; }
        //public CollectionFeedView ident { get => this; }
        public CollectionFeedView ident { get => this; }
        public abstract string name { get; set; }
        public abstract string description { get; }
        //TODO: thumbnail
        //TODO: count
        public ObservableCollection<CollectionFeedView> children { get => this._children; }

        public CollectionFeedView(Guid guid) { //TODO: count?, children?
            this.guid = guid;
            this._children = null;
        }
    }

    public class CollectionView : CollectionFeedView {
        public bool _is_expanded = true;
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
        //TODO: thumbnail

        public CollectionView(Guid ident, Collection collection) : base(ident) { //TODO: count?, children?
            this.collection = collection;
            this._children = new ObservableCollection<CollectionFeedView>();
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
        //TODO: thumbnail

        public FeedView(Guid ident, Feed feed) : base(ident) { //TODO: count?
            this.feed = feed;
        }
    }
}
