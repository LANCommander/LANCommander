using AntDesign.TableModels;
using Microsoft.AspNetCore.Components;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;

namespace LANCommander.Server.UI.Components
{
    public class DataColumn<TData> : AntDesign.Column<TData>
    {
        [CascadingParameter]
        public Dictionary<int, bool> ColumnVisibility { get; set; } = default!;

        protected override void OnParametersSet()
        {
            if (ColumnVisibility.ContainsKey(ColIndex))
            {
                Hidden = !ColumnVisibility[ColIndex];

                StateHasChanged();
            }
            else
            {
                Hidden = false;
            }

            base.OnParametersSet();
        }
    }
}
