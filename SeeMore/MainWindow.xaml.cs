using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SeeMore {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private CollectionFeedView selected_coll = null;
        private List<ArticleView> coll_articles = null;
        private ArticleView selected_art = null;

        public MainWindow() {
            InitializeComponent();
        }

        //TODO: menu handlers

        private void populate_articles(List<ArticleView> articles, CollectionFeedView coll) {
            FeedView feed = coll as FeedView;
            if (feed != null) {
                foreach (Guid artId in feed.feed.articles.articles.Keys) {
                    Article art = feed.feed.articles.articles[artId];
                    articles.Add(new ArticleView(feed.guid, artId, art));
                }
            }
            if (coll.children != null) {
                foreach (CollectionFeedView child in coll.children) {
                    this.populate_articles(articles, child);
                }
            }
        }

        private int artview_compare(ArticleView x, ArticleView y) {
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

        private void coll_list_sel_changed(object sender, RoutedEventArgs e) {
            CollectionFeedView sel = this.coll_list.SelectedItem as CollectionFeedView;
            if (sel == this.selected_coll) {
                return;
            }
            this.selected_coll = sel;
            this.coll_edit_but.IsEnabled = (sel != null);
            this.coll_move_but.IsEnabled = (sel != null);
            this.coll_rem_but.IsEnabled = (sel != null);
            this.coll_update_but.IsEnabled = (sel != null);
            //TODO: collection thumbnail
            this.coll_name_box.Content = (sel == null ? "" : sel.name);
            this.coll_desc_box.Text = (sel == null ? "" : sel.description);
            //TODO: coll_updated_box
            List<ArticleView> articles = new List<ArticleView>();
            this.populate_articles(articles, sel);
            articles.Sort((x, y) => this.artview_compare(x, y));
            this.coll_articles = articles;
            this.art_list.ItemsSource = articles;
        }

        //TODO: coll_add, coll_edit, coll_move, coll_remove, coll_update

        private void art_list_sel_changed(object sender, RoutedEventArgs e) {
            ArticleView sel = null;
            int selIdx = this.art_list.SelectedIndex;
            if ((selIdx >= 0) && (this.coll_articles != null) && (selIdx < this.coll_articles.Count)) {
                sel = this.coll_articles[selIdx];
            }
            if (sel == this.selected_art) {
                return;
            }
            this.selected_art = sel;
            //TODO: feed thumbnail, feed_name_box
            this.art_title_box.Content = (sel == null ? "" : sel.title);
            this.art_timestamp_box.Content = (sel == null ? "" : sel.timestamp);
            //TODO: article contents
            this.art_open_but.IsEnabled = (sel != null);
            this.art_del_but.IsEnabled = (sel != null);
        }

        //TODO: art_open, art_del

        //TODO: other handlers
    }
}
