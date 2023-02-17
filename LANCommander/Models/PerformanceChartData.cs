using MudBlazor;
using System.Diagnostics;

namespace LANCommander.Models
{
    public class PerformanceChartData
    {
        public PerformanceCounterData ProcessorUtilization { get; set; }
        public Dictionary<string, PerformanceCounterData> NetworkUploadRate { get; set; }
        public Dictionary<string, PerformanceCounterData> NetworkDownloadRate { get; set; }
    }

    public class PerformanceCounterData
    {
        public PerformanceCounter PerformanceCounter { get; set; }
        public double[] Data { get; set; }

        public ChartSeries ToSeries(string name)
        {
            return new ChartSeries
            {
                Name = name,
                Data = Data
            };
        }

        public List<ChartSeries> ToSeriesList(string name)
        {
            return new List<ChartSeries>
            {
                ToSeries(name)
            };
        }
    }
}
