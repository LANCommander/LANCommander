﻿using AntDesign.TableModels;
using Microsoft.AspNetCore.Components;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;

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

        protected override void OnInitialized()
        {
            ClassMapper.Add("ant-flex");
            ClassMapper.Add("ant-flex-align-normal");
            ClassMapper.Add("ant-flex-justify-end");
            ClassMapper.Add("ant-flex-gap-small");
            ClassMapper.Add("ant-flex-wrap-nowrap");

            base.OnInitialized();
        }

        protected override void OnParametersSet()
        {
            if (!ColumnVisibility.ContainsKey(ColIndex))
                ColumnVisibility.Add(ColIndex, !Hide);

            ClassMapper.If("column-hidden", () => ColumnVisibility.ContainsKey(ColIndex) && !ColumnVisibility[ColIndex]);
            
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
