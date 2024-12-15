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
        public Expression<Func<TItem, TProp>> Property { get; set; } = default!;

        [CascadingParameter]
        public Dictionary<int, bool> ColumnVisibility { get; set; } = default!;

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
                    var compiledProperty = Property.Compile();
                    GetValue = rowData => compiledProperty.Invoke(((RowData<TItem>)rowData).DataItem.Data);
                }

                base.OnInitialized();
            }
        }

        protected override void OnParametersSet()
        {
            ClassMapper.If("column-hidden", () => ColumnVisibility.ContainsKey(ColIndex) && !ColumnVisibility[ColIndex]);

            StateHasChanged();

            base.OnParametersSet();
        }
    }
}
