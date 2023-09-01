using AntDesign;
using Microsoft.AspNetCore.Components;

namespace LANCommander.Components.Table
{
    public interface IPickableColumn : IColumn
    {
        public bool Visible { get; set; }
        public EventCallback<bool> VisibleChanged { get; set; }
    }
}
