using LANCommander.Client.Data.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Client.Models
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
        public object DataItem { get; set; }
        public ObservableCollection<LibraryItem> Children { get; set; }

        public LibraryItem(Collection collection)
        {
            Key = collection.Id;
            Type = LibraryItemType.Game;
            Name = collection.Name;
            DataItem = collection;
            Children = new ObservableCollection<LibraryItem>(collection.Games.Select(g => new LibraryItem(g)).ToList());
        }

        public LibraryItem(Game game)
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

            var icon = game.Media.FirstOrDefault(m => m.Type == Data.Enums.MediaType.Icon);

            if (icon != null)
                IconId = icon.Id;
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
