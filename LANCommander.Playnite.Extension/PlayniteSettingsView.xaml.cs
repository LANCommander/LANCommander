using LANCommander.SDK.Models;
using Playnite.SDK;
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

namespace LANCommander.PlaynitePlugin
{
    public partial class PlayniteSettingsView : UserControl
    {
        private PlayniteLibraryPlugin Plugin;

        public PlayniteSettingsView(PlayniteLibraryPlugin plugin)
        {
            Plugin = plugin;

            InitializeComponent();

            UpdateAuthenticationButtonVisibility();
        }

        private void UpdateAuthenticationButtonVisibility()
        {
            try
            {
                if (Plugin.LANCommander.ValidateToken(new AuthToken()
                {
                    AccessToken = Plugin.Settings.AccessToken,
                    RefreshToken = Plugin.Settings.RefreshToken,
                }))
                {
                    var authenticateButton = FindName("AuthenticateButton") as Button;

                    authenticateButton.Visibility = Visibility.Hidden;
                }
            }
            catch
            {

            }
        }

        private void AuthenticateButton_Click(object sender, RoutedEventArgs e)
        {
            var authWindow = Plugin.ShowAuthenticationWindow();

            authWindow.Closed += AuthWindow_Closed;
        }

        private void AuthWindow_Closed(object sender, EventArgs e)
        {
            UpdateAuthenticationButtonVisibility();
        }
    }
}
