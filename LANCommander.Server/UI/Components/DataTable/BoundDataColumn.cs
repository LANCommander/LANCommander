using AntDesign.TableModels;
using Microsoft.AspNetCore.Components;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;

namespace LANCommander.Server.UI.Components
{
    public class BoundDataColumn<TItem, TProp> : AntDesign.Column<TProp>
    {
        [Parameter]
        public Expression<Func<TItem, TProp>>? Property { get; set; } = default!;
        
        [Parameter]
        public string Include { get; set; } = default!;

        [Parameter]
        public bool Hide { get; set; }

        [CascadingParameter]
        public Dictionary<int, bool> ColumnVisibility { get; set; } = default!;

        [CascadingParameter(Name = "Includes")]
        public List<string> Includes { get; set; } = default!;

        protected override void OnInitialized()
        {
            if (Property != null)
            {
                var memberExpression = Property.Body as MemberExpression;

                if (memberExpression != null && String.IsNullOrWhiteSpace(Title))
                {
                    var property = memberExpression.Member as PropertyInfo;

                    if (property != null)
                    {
                        var displayAttribute = property.GetCustomAttribute<DisplayAttribute>();
                        Title = displayAttribute != null ? displayAttribute.GetName() : property.Name;
                    }
                }

                if (IsHeader)
                {
                    GetFieldExpression = Property;
                }
                else if (IsBody)
                {
                    try
                    {
                        var compiledProperty = Property.Compile();
                        GetValue = rowData => compiledProperty.Invoke(((RowData<TItem>)rowData).DataItem.Data);
                    }
                    catch
                    {
                        GetValue = rowData => default;
                    }
                }

                if (Sortable)
                    SortDirections = new[] 
                        { AntDesign.SortDirection.Descending, AntDesign.SortDirection.Ascending, AntDesign.SortDirection.Descending };

                base.OnInitialized();
            }
        }

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
