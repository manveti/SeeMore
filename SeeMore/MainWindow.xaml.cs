﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;

namespace SeeMore {
    public class SizeConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            double ratio = (double)parameter;
            if (ratio < 0) {
                ratio = 0;
            }
            if (ratio > 1) {
                ratio = 1;
            }
            return ratio * (double)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        public static string settings_dir;
        public static Config config;
        private ViewManager view_manager;

        static MainWindow() {
            string appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            settings_dir = Path.Join(appDataDir, "manveti", "SeeMore");
            config = Config.loadConfig(settings_dir);
        }

        public MainWindow() {
            HttpUtils.userAgent = config.userAgent;
            this.InitializeComponent();
            string dataDir = config.dataDir;
            if (dataDir == null) {
                dataDir = Path.Join(Directory.GetCurrentDirectory(), "feeds");
            }
            this.view_manager = new ViewManager(this, dataDir);
            this.view_manager.load();
        }

        private void handle_close(object sender, CancelEventArgs e) {
            if (this.view_manager != null) {
                this.view_manager.running = false;
                this.view_manager.updateEvent.Set();
            }
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
            this.coll_updated_box.Content = (updated == null ? "" : ((DateTimeOffset)updated).ToLocalTime().ToString("G"));
        }

        private void coll_add(object sender, RoutedEventArgs e) {
            this.view_manager.addItem();
        }

        private void coll_edit(object sender, RoutedEventArgs e) {
            this.view_manager.editItem();
        }

        private void coll_move(object sender, RoutedEventArgs e) {
            this.view_manager.moveItem();
        }

        //TODO: coll_remove

        private void coll_update(object sender, RoutedEventArgs e) {
            this.view_manager.updateItem();
        }

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
            this.art_url_box.Text = (sel == null ? "" : sel.article.url);
            this.art_open_but.IsEnabled = (sel != null);
            this.art_del_but.IsEnabled = (sel != null);
        }

        private void art_list_open(object sender, RoutedEventArgs e) {
            ArticleView articleView = ((FrameworkElement)sender).DataContext as ArticleView;
            this.open_article(articleView);
        }

        private void art_open(object sender, RoutedEventArgs e) {
            this.open_article(this.view_manager.selectedArt);
        }

        private void open_article(ArticleView articleView) {
            if ((articleView == null) || (articleView.article.url == null) || (articleView.article.url.Length <= 0)) {
                return;
            }
            Process proc = new Process();
            proc.StartInfo.UseShellExecute = true;
            if (config.browserPath == null) {
                proc.StartInfo.FileName = articleView.article.url;
            }
            else {
                proc.StartInfo.FileName = config.browserPath;
                if (config.browserArgs == null) {
                    proc.StartInfo.Arguments = articleView.article.url;
                }
                else {
                    proc.StartInfo.Arguments = string.Format(config.browserArgs, articleView.article.url);
                }
            }
            proc.Start();
        }

        private void art_list_del(object sender, RoutedEventArgs e) {
            ArticleView articleView = ((FrameworkElement)sender).DataContext as ArticleView;
            this.delete_article(articleView);
        }

        private void art_del(object sender, RoutedEventArgs e) {
            this.delete_article(this.view_manager.selectedArt);
        }

        private void delete_article(ArticleView articleView) {
            if (articleView == null) {
                return;
            }
            this.view_manager.deleteArticle(articleView);
        }

        public void hide_article_content() {
            this.content_box_default.Visibility = Visibility.Collapsed;
            this.content_box_html.Visibility = Visibility.Collapsed;
            this.content_box_image.Visibility = Visibility.Collapsed;
            //TODO: other content boxes
        }

        //TODO: other handlers
    }
}
