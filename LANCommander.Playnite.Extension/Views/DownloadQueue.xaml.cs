using LANCommander.PlaynitePlugin.Models;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LANCommander.PlaynitePlugin.Views
{
    /// <summary>
    /// Interaction logic for DownloadQueue.xaml
    /// </summary>
    public partial class DownloadQueue : UserControl
    {
        private readonly LANCommanderLibraryPlugin Plugin;

        public DownloadQueue(LANCommanderLibraryPlugin plugin)
        {
            Plugin = plugin;

            InitializeComponent();
        }

        private void CancelDownload(object sender, EventArgs e)
        {
            Plugin.DownloadQueue.CancelInstall();
        }

        private void BackToLibrary(object sender, RoutedEventArgs e)
        {
            Plugin.PlayniteApi.MainView.SwitchToLibraryView();
        }

        private void RemoveItem(object sender, RoutedEventArgs e)
        {
            var hyperlink = (Hyperlink)sender;

            Plugin.DownloadQueue.Remove(hyperlink.DataContext as DownloadQueueItem);
        }

        private void Play(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var item = button.DataContext as DownloadQueueItem;

            Plugin.PlayniteApi.StartGame(item.Game.Id);
        }

        private void ViewInLibrary(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var item = button.DataContext as DownloadQueueItem;

            Plugin.PlayniteApi.MainView.SwitchToLibraryView();
            Plugin.PlayniteApi.MainView.SelectGame(item.Game.Id);
        }
    }
}
