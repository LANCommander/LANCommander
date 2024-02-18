using LANCommander.SDK.Models;
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
    /// Interaction logic for SaveManager.xaml
    /// </summary>
    public partial class SaveManagerView : UserControl
    {
        private LANCommanderLibraryPlugin Plugin { get; set; }
        private Playnite.SDK.Models.Game Game { get; set; }

        public SaveManagerView(LANCommanderLibraryPlugin plugin, Playnite.SDK.Models.Game game)
        {
            Plugin = plugin;
            Game = game;

            InitializeComponent();

            LoadSaves();
        }

        private void LoadSaves()
        {
            var saves = Plugin.LANCommanderClient.Saves.Get(Game.Id);

            DataContext = saves;
            SaveList.ItemsSource = saves;
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            var item = SaveList.SelectedItem as GameSave;

            Plugin.PlayniteApi.MainView.UIDispatcher.InvokeAsync((System.Action)(async () =>
            {
                await Plugin.LANCommanderClient.Saves.Download(Game.InstallDirectory, Game.Id, item.Id);

                Window.GetWindow(this).Close();
            }));
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var item = SaveList.SelectedItem as GameSave;

            Plugin.LANCommanderClient.Saves.Delete(item.Id);

            LoadSaves();
        }
    }
}
