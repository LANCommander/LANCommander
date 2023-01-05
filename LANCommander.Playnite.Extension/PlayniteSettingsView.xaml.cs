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

namespace LANCommander.Playnite.Extension
{
    public partial class PlayniteSettingsView : UserControl
    {
        private PlayniteLibraryPlugin Plugin;

        public PlayniteSettingsView(PlayniteLibraryPlugin plugin)
        {
            Plugin = plugin;

            InitializeComponent();

            var settings = Plugin.GetSettings(false) as PlayniteSettingsViewModel;

            if (Plugin.LANCommander.ValidateToken(new AuthToken()
            {
                AccessToken = settings.AccessToken,
                RefreshToken = settings.RefreshToken,
            }))
            {
                var authenticateButton = FindName("AuthenticateButton") as Button;

                authenticateButton.Visibility = Visibility.Hidden;
            }
        }

        private void AuthenticateButton_Click(object sender, RoutedEventArgs e)
        {
            Plugin.ShowAuthenticationWindow();
        }
    }
}
