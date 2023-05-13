using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SeeMore {
    /// <summary>
    /// Interaction logic for AddEditWindow.xaml
    /// </summary>
    public partial class AddEditWindow : Window {
        public const string TYPE_COLLECTION = "Collection";
        public const string TYPE_FEED = "Text Feed";
        public const string TYPE_HTML_FEED = "HTML Feed";
        public const string TYPE_YOUTUBE_FEED = "YouTube Channel";

        public bool valid = false;
        public byte[] icon = null;

        public AddEditWindow(string title) {
            this.InitializeComponent();
            this.Title = title;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.SizeToContent = SizeToContent.WidthAndHeight;
        }

        private void type_box_sel_changed(object sender, RoutedEventArgs e) {
            string type = (string)(this.type_box.SelectedValue);
            this.channel_id_lbl.Visibility = (type == TYPE_YOUTUBE_FEED ? Visibility.Visible : Visibility.Collapsed);
            this.channel_id_box.Visibility = (type == TYPE_YOUTUBE_FEED ? Visibility.Visible : Visibility.Collapsed);
            this.video_id_lbl.Visibility = (type == TYPE_YOUTUBE_FEED ? Visibility.Visible : Visibility.Collapsed);
            this.video_id_box.Visibility = (type == TYPE_YOUTUBE_FEED ? Visibility.Visible : Visibility.Collapsed);
            this.url_lbl.Visibility = (type != TYPE_COLLECTION ? Visibility.Visible : Visibility.Collapsed);
            this.url_box.Visibility = (type != TYPE_COLLECTION ? Visibility.Visible : Visibility.Collapsed);
            this.interval_lbl.Visibility = (type != TYPE_COLLECTION ? Visibility.Visible : Visibility.Collapsed);
            this.interval_box.Visibility = (type != TYPE_COLLECTION ? Visibility.Visible : Visibility.Collapsed);
            this.backload_lbl.Visibility = (type != TYPE_COLLECTION ? Visibility.Visible : Visibility.Collapsed);
            this.backload_box.Visibility = (type != TYPE_COLLECTION ? Visibility.Visible : Visibility.Collapsed);
            this.load_but.Content = (type == TYPE_COLLECTION ? "Load Icon" : "Load Details");
            this.preview_but.Visibility = (type != TYPE_COLLECTION ? Visibility.Visible : Visibility.Collapsed);
        }

        private void load_details(object sender, RoutedEventArgs e) {
            string type = (string)(this.type_box.SelectedValue);
            string channelId = this.channel_id_box.Text;
            string videoId = this.video_id_box.Text;
            string url = this.url_box.Text;
            string iconPath = this.icon_box.Text;
            byte[] icon = null;
            FeedMetadata metadata = null;
            switch (type) {
            case TYPE_FEED:
                metadata = Feed.getMetadata(url);
                break;
            case TYPE_HTML_FEED:
                metadata = HtmlFeed.getMetadata(url);
                break;
            case TYPE_YOUTUBE_FEED:
                //TODO: ...
                metadata = YouTubeFeed.getMetadata(url);
                break;
            }
            if ((iconPath != null) && (iconPath.Length > 0)) {
                icon = HttpUtils.downloadFile(iconPath);
            }
            else if (type != TYPE_COLLECTION) {
                icon = metadata.icon;
            }
            this.icon = icon;
            this.icon_img.Source = ViewManager.getImageSource(this.icon);
            if (type == TYPE_COLLECTION) {
                return;
            }
            this.name_box.Text = metadata.name;
            this.desc_box.Text = metadata.description;
            if (type == TYPE_YOUTUBE_FEED) {
                //TODO: ...
            }
        }

        //TODO: preview feed

        private void do_ok(object sender, RoutedEventArgs e) {
            this.valid = true;
            this.Close();
        }

        private void do_cancel(object sender, RoutedEventArgs e) {
            this.Close();
        }
    }
}
