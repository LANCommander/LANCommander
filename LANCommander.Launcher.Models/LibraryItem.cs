using LANCommander.Launcher.Data.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Models
{
    public enum LibraryItemState
    {
        NotInstalled,
        Installed,
        Queued,
        Installing,
        UpdateAvailable
    }

    public enum LibraryItemType
    {
        Game,
        Redistributable
    }

    public class LibraryItem
    {
        public Guid Key { get; set; }
        public Guid IconId { get; set; }
        public LibraryItemType Type { get; set; }
        public LibraryItemState State { get; set; }
        public string Name { get; set; }
        public string SortName { get; set; }
        public string[] Groups { get; set; }
        public object DataItem { get; set; }

        public LibraryItem(Collection collection)
        {
            Key = collection.Id;
            Type = LibraryItemType.Game;
            Name = collection.Name;
            DataItem = collection;
        }

        public LibraryItem(Game game, Func<LibraryItem, string[]> groupSelector)
        {
            Key = game.Id;
            Type = LibraryItemType.Game;
            Name = game.Title;
            SortName = game.SortTitle;
            DataItem = game;

            if (game.Installed && !String.IsNullOrWhiteSpace(game.LatestVersion) && game.InstalledVersion != game.LatestVersion)
                State = LibraryItemState.UpdateAvailable;
            else if (game.Installed)
                State = LibraryItemState.Installed;
            else
                State = LibraryItemState.NotInstalled;

            var icon = game.Media.FirstOrDefault(m => m.Type == SDK.Enums.MediaType.Icon);

            if (icon != null)
                IconId = icon.Id;

            Groups = groupSelector.Invoke(this);
        }

        public LibraryItem(Redistributable redistributable)
        {
            Key = redistributable.Id;
            Type = LibraryItemType.Redistributable;
            Name = redistributable.Name;
            DataItem = redistributable;
        }
    }
}
