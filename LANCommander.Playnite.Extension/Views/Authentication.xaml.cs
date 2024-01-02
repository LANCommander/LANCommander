using BeaconLib;
using LANCommander.SDK.Models;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
        public static readonly ILogger Logger = LogManager.GetLogger();

        private LANCommanderLibraryPlugin Plugin;
        private ViewModels.Authentication Context { get { return (ViewModels.Authentication)DataContext; } }

        public Authentication(LANCommanderLibraryPlugin plugin)
        {
            Plugin = plugin;

            InitializeComponent();

            var probe = new Probe("LANCommander");

            Logger.Trace("Attempting to find a LANCommander server on the local network...");

            probe.BeaconsUpdated += beacons => Dispatcher.BeginInvoke((System.Action)(() =>
            {
                var beacon = beacons.First();

                if (!String.IsNullOrWhiteSpace(beacon.Data) && Uri.TryCreate(beacon.Data, UriKind.Absolute, out var beaconUri))
                    Context.ServerAddress = beaconUri.ToString();
                else
                    Context.ServerAddress = $"http://{beacon.Address.Address}:{beacon.Address.Port}";

                this.ServerAddress.Text = Context.ServerAddress;

                Logger.Trace($"The beacons have been lit! LANCommander calls for aid! {Context.ServerAddress}");

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

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            Register();
        }

        private void Password_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext != null)
            {
                ((ViewModels.Authentication)DataContext).Password = ((PasswordBox)sender).Password;
            }
        }

        public RelayCommand<object> LoginCommand
        {
            get => new RelayCommand<object>(async (a) =>
            {
                await Authenticate();
            });
        }

        private async Task Authenticate()
        {
            try
            {
                LoginButton.Dispatcher.Invoke(new System.Action(() =>
                {
                    LoginButton.IsEnabled = false;
                    LoginButton.Content = "Logging in...";
                }));

                if (Plugin.LANCommanderClient == null)
                    Plugin.LANCommanderClient = new LANCommander.SDK.Client(Context.ServerAddress, Plugin.Settings.InstallDirectory);
                else
                    Plugin.LANCommanderClient.UseServerAddress(Context.ServerAddress);

                var response = await Plugin.LANCommanderClient.AuthenticateAsync(Context.UserName, Context.Password);

                Plugin.Settings.ServerAddress = Context.ServerAddress;
                Plugin.Settings.AccessToken = response.AccessToken;
                Plugin.Settings.RefreshToken = response.RefreshToken;

                var profile = Plugin.LANCommanderClient.Profile.Get();

                Plugin.Settings.PlayerName = String.IsNullOrWhiteSpace(profile.Alias) ? profile.UserName : profile.Alias;

                // Probably unneeded, but why not be more secure?
                Context.Password = String.Empty;

                Plugin.SavePluginSettings(Plugin.Settings);

                Window.GetWindow(this).Close();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, ex.Message);

                Plugin.PlayniteApi.Dialogs.ShowErrorMessage(ex.Message);

                LoginButton.Dispatcher.Invoke(new System.Action(() =>
                {
                    LoginButton.IsEnabled = true;
                    LoginButton.Content = "Login";
                }));
            }
        }

        private async Task Register()
        {
            try
            {
                LoginButton.IsEnabled = false;
                RegisterButton.IsEnabled = false;
                RegisterButton.Content = "Working...";

                if (Plugin.LANCommanderClient == null)
                    Plugin.LANCommanderClient = new LANCommander.SDK.Client(Context.ServerAddress, Plugin.Settings.InstallDirectory);

                var response = await Plugin.LANCommanderClient.RegisterAsync(Context.UserName, Context.Password);

                Plugin.Settings.ServerAddress = Context.ServerAddress;
                Plugin.Settings.AccessToken = response.AccessToken;
                Plugin.Settings.RefreshToken = response.RefreshToken;
                Plugin.Settings.PlayerName = Context.UserName;

                Context.Password = String.Empty;

                Plugin.SavePluginSettings(Plugin.Settings);

                Window.GetWindow(this).Close();
            }
            catch (Exception ex)
            {
                Plugin.PlayniteApi.Dialogs.ShowErrorMessage(ex.Message);

                LoginButton.IsEnabled = true;
                RegisterButton.IsEnabled = true;
                RegisterButton.Content = "Register";
            }
        }
    }
}
