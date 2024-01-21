using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.PlaynitePlugin.Models
{
    public class DownloadQueue : ObservableObject
    {
        private DownloadQueueItem currentItem { get; set; }
        public DownloadQueueItem CurrentItem {
            get => currentItem;
            set
            {
                currentItem = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<DownloadQueueItem> Items { get; set; }

        public DownloadQueue()
        {
            Items = new ObservableCollection<DownloadQueueItem>();
        }
    }
}
