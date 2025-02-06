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
        [Parameter]
        public string Include { get; set; } = default!;
        
        [Parameter]
        public bool Hide { get; set; }

        [CascadingParameter]
        public Dictionary<int, bool> ColumnVisibility { get; set; } = default!;

        [CascadingParameter]
        public List<string> Includes { get; set; } = default!;

        protected override void OnParametersSet()
        {
            if (!ColumnVisibility.ContainsKey(ColIndex))
                ColumnVisibility.Add(ColIndex, !Hide);
            
            ClassMapper.If("column-hidden", () => ColumnVisibility.ContainsKey(ColIndex) && !ColumnVisibility[ColIndex]);

            StateHasChanged();

            if (!String.IsNullOrWhiteSpace(Include) && !Includes.Contains(Include))
            {
                Includes.Add(Include);
            }

            base.OnParametersSet();
        }
    }
}
