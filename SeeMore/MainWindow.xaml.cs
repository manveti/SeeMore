﻿using System;
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
        private ViewManager view_manager;

        public MainWindow() {
            this.InitializeComponent();
        }

        //TODO: menu handlers

        private void populate_articles(List<ArticleView> articles, CollectionFeedView coll) {
            if (coll is FeedView feed) {
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

        private void coll_list_sel_changed(object sender, RoutedEventArgs e) {
            CollectionFeedView sel = this.coll_list.SelectedItem as CollectionFeedView;
            if (!this.view_manager.selectCollection(sel)) {
                return;
            }
            this.coll_edit_but.IsEnabled = (sel != null);
            this.coll_move_but.IsEnabled = (sel != null);
            this.coll_rem_but.IsEnabled = (sel != null);
            this.coll_update_but.IsEnabled = (sel != null);
            this.coll_icon.Source = sel?.icon;
            this.coll_name_box.Content = (sel == null ? "" : sel.name);
            this.coll_desc_box.Text = (sel == null ? "" : sel.description);
            FeedView feed = sel as FeedView;
            DateTimeOffset? updated = feed?.feed?.metadata?.lastUpdated;
            this.coll_updated_box.Content = (updated == null ? "" : ((DateTimeOffset)updated).ToString("G"));
        }

        //TODO: coll_add, coll_edit, coll_move, coll_remove, coll_update

        private void art_list_sel_changed(object sender, RoutedEventArgs e) {
            ArticleView prevSel = this.view_manager.selectedArt;
            ArticleView sel = this.view_manager.selectArticle(this.art_list.SelectedIndex);
            if (sel == prevSel) {
                return;
            }
            CollectionFeedView feedView = this.view_manager.getCollectionFeedView(sel?.feed);
            this.feed_icon.Source = feedView?.icon;
            this.feed_name_box.Content = (feedView == null ? "" : feedView.name);
            this.art_title_box.Content = (sel == null ? "" : sel.title);
            this.art_timestamp_box.Content = (sel == null ? "" : sel.timestamp);
            this.art_open_but.IsEnabled = (sel != null);
            this.art_del_but.IsEnabled = (sel != null);
        }

        //TODO: art_open, art_del

        public void hide_article_content() {
            this.content_box_default.Visibility = Visibility.Collapsed;
            this.content_box_image.Visibility = Visibility.Collapsed;
            //TODO: other content boxes
        }

        //TODO: other handlers
    }
}
