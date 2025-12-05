using Microsoft.AspNetCore.Components;

namespace LANCommander.Server.UI.Components
{
    public class DataActions<TData> : AntDesign.Column<TData>
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
            ClassMapper.Add("table-data-actions");
            
            StateHasChanged();

            if (!String.IsNullOrWhiteSpace(Include))
            {
                var includes = Include.Split(',');

                foreach (var include in includes)
                {
                    if (!String.IsNullOrWhiteSpace(include) && !Includes.Contains(include))
                        Includes.Add(include);
                }
            }

            base.OnParametersSet();
        }
    }
}
