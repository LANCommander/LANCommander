using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.PowerShell.Debugging;

namespace LANCommander.Launcher.ViewModels.ScriptDebugger.Items;

public sealed class CallStackFrameViewModel
{
    public CallStackFrameViewModel(CallStackFrameInfo info) => Info = info;

    public CallStackFrameInfo Info { get; }

    public string Display => Info.Display;

    public bool HasSource => Info.ScriptName is not null;
}
