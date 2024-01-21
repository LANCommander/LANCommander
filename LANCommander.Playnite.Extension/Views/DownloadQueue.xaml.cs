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

        private void RemoveItem(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;

            Plugin.DownloadQueue.Remove(button.DataContext as DownloadQueueItem);
        }
    }
}
