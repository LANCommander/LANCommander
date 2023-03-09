using BeaconLib;
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

namespace LANCommander.PlaynitePlugin.Views
{
    public partial class Authentication : UserControl
    {
        private LANCommanderLibraryPlugin Plugin;
        private ViewModels.Authentication Context { get { return (ViewModels.Authentication)DataContext; } }

        public Authentication(LANCommanderLibraryPlugin plugin)
        {
            Plugin = plugin;

            InitializeComponent();

            var probe = new Probe("LANCommander");

            probe.BeaconsUpdated += beacons => Dispatcher.BeginInvoke((System.Action)(() =>
            {
                var beacon = beacons.First();

                Context.ServerAddress = $"http://{beacon.Address.Address}:{beacon.Address.Port}";

                this.ServerAddress.Text = Context.ServerAddress;

                probe.Stop();
            }));

            probe.Start();

        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Return)
            {
                Authenticate();
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            Authenticate();
        }

        private void Password_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext != null)
            {
                ((ViewModels.Authentication)DataContext).Password = ((PasswordBox)sender).Password;
            }
        }

        private void Authenticate()
        {
            try
            {
                if (Plugin.LANCommander == null || Plugin.LANCommander.Client == null)
                    Plugin.LANCommander = new LANCommanderClient(Context.ServerAddress);
                else
                    Plugin.LANCommander.Client.BaseUrl = new Uri(Context.ServerAddress);

                var response = Plugin.LANCommander.Authenticate(Context.UserName, Context.Password);

                Plugin.Settings.ServerAddress = Context.ServerAddress;
                Plugin.Settings.AccessToken = response.AccessToken;
                Plugin.Settings.RefreshToken = response.RefreshToken;

                Plugin.LANCommander.Token = new AuthToken()
                {
                    AccessToken = response.AccessToken,
                    RefreshToken = response.RefreshToken,
                };

                // Probably unneeded, but why not be more secure?
                Context.Password = String.Empty;

                Plugin.SavePluginSettings(Plugin.Settings);

                Window.GetWindow(this).Close();
            }
            catch (Exception ex)
            {
                Plugin.PlayniteApi.Dialogs.ShowErrorMessage(ex.Message);
            }
        }
    }
}
