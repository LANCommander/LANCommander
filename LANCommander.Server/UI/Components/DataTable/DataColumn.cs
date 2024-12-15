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
            ClassMapper.If("column-hidden", () => ColumnVisibility.ContainsKey(ColIndex) && !ColumnVisibility[ColIndex]);

            StateHasChanged();

            base.OnParametersSet();
        }
    }
}
