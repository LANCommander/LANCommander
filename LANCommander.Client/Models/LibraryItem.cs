using LANCommander.Client.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Client.Models
{
    public enum LibraryItemType
    {
        Game,
        Redistributable
    }

    public class LibraryItem
    {
        public Guid Key { get; set; }
        public LibraryItemType Type { get; set; }
        public string Name { get; set; }
        public string SortName { get; set; }
        public object DataItem { get; set; }
        public IEnumerable<LibraryItem> Children { get; set; }

        public LibraryItem(Collection collection)
        {
            Key = collection.Id;
            Type = LibraryItemType.Game;
            Name = collection.Name;
            DataItem = collection;
            Children = collection.Games.Select(g => new LibraryItem(g)).ToList();
        }

        public LibraryItem(Game game)
        {
            Key = game.Id;
            Type = LibraryItemType.Game;
            Name = game.Title;
            SortName = game.SortTitle;
            DataItem = game;
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
