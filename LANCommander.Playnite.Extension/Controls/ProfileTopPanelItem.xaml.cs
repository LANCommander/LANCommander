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

namespace LANCommander.PlaynitePlugin.Controls
{
    /// <summary>
    /// Interaction logic for ProfileTopPanelItem.xaml
    /// </summary>
    public partial class ProfileTopPanelItem : UserControl
    {
        private readonly LANCommanderLibraryPlugin Plugin;

        public ProfileTopPanelItem(LANCommanderLibraryPlugin plugin)
        {
            Plugin = plugin;
            DataContext = Plugin.Settings;
            InitializeComponent();
        }

        private void OpenProfileMenu(object sender, RoutedEventArgs e)
        {
            if (Plugin.LANCommanderClient.IsConnected())
            {
                ContextMenu profileMenu = this.FindResource("ProfileMenu") as ContextMenu;

                profileMenu.PlacementTarget = sender as Button;
                profileMenu.IsOpen = true;
            }
            else
            {
                Plugin.ShowAuthenticationWindow();
            }
        }

        private void ChangeName(object sender, RoutedEventArgs e)
        {
            Plugin.ShowNameChangeWindow();
        }

        private void ViewProfile(object sender, RoutedEventArgs e)
        {
            var profileUri = new Uri(new Uri(Plugin.Settings.ServerAddress), "Profile");
            System.Diagnostics.Process.Start(profileUri.ToString());
        }

        private async void LogOut(object sender, RoutedEventArgs e)
        {
            await Plugin.Logout();
        }
    }
}
