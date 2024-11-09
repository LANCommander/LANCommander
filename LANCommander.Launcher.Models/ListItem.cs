using LANCommander.Launcher.Data.Models;
using LANCommander.SDK.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Models
{
    public enum ListItemState
    {
        NotInstalled,
        Installed,
        Queued,
        Installing,
        UpdateAvailable
    }

    public enum ListItemType
    {
        Game,
        Redistributable
    }

    public class ListItem
    {
        public Guid Key { get; set; }
        public Guid IconId { get; set; }
        public ListItemType Type { get; set; }
        public ListItemState State { get; set; }
        public string Name { get; set; }
        public string SortName { get; set; }
        public string[] Groups { get; set; }
        public object DataItem { get; set; }

        public ListItem(Collection collection)
        {
            Key = collection.Id;
            Type = ListItemType.Game;
            Name = collection.Name;
            DataItem = collection;
        }

        public ListItem(Game game)
        {
            Key = game.Id;
            Type = ListItemType.Game;
            Name = game.Title;
            SortName = game.SortTitle;
            DataItem = game;

            var manifestPath = ManifestHelper.GetPath(game.InstallDirectory, game.Id);

            if (game.Installed && !String.IsNullOrWhiteSpace(game.LatestVersion) && game.InstalledVersion != game.LatestVersion)
                State = ListItemState.UpdateAvailable;
            else if (game.Installed && File.Exists(manifestPath))
                State = ListItemState.Installed;
            else
                State = ListItemState.NotInstalled;

            var icon = game.Media.FirstOrDefault(m => m.Type == SDK.Enums.MediaType.Icon);

            if (icon != null)
                IconId = icon.Id;
        }

        public ListItem(Redistributable redistributable)
        {
            Key = redistributable.Id;
            Type = ListItemType.Redistributable;
            Name = redistributable.Name;
            DataItem = redistributable;
        }

        public ListItem(SDK.Models.Game game)
        {
            Key = game.Id;
            Type = ListItemType.Game;
            Name = game.Title;
            SortName = game.SortTitle;
            DataItem = game;
        }
    }
}
