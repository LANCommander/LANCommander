using System;
using System.Threading.Tasks;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Abstractions;

public interface ISettingsProvider
{
    Settings CurrentValue { get; }
    Task UpdateAsync(Settings settings);
    Task UpdateAsync(Action<Settings> patch);
}