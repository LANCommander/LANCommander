using System;
using System.Threading.Tasks;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Abstractions;

public interface ISettingsProvider
{
    Settings CurrentValue { get; }
    void Update(Action<Settings> patch);
}