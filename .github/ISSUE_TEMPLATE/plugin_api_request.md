---
name: Plugin API request
about: Ask for a new or expanded extension point, contract, or service in the plugin API
title: "[Plugin API]"
labels: ''
assignees: ''

---

**Plugin type**
Which side of the plugin API this affects:
- [ ] Server (`LANCommander.Server.Plugins`)
- [ ] Server UI (`LANCommander.Server.UI`)
- [ ] Launcher (`LANCommander.Launcher.Plugins`)
- [ ] SDK (`LANCommander.SDK`)

**What are you trying to build?**
Describe the plugin or feature you're working on and what it needs to do.

**What's missing or limiting?**
Describe the gap in the current API. Name the existing interfaces, extensions, or services you looked at (e.g. `IServerEngineExtension`, `IPluginSettings<T>`) and why they don't cover your use case.

**Proposed API**
If you have an idea of what the API should look like, sketch it here. A rough interface, method signature, or extension point is enough.

```csharp
// e.g.
public interface IMyExtension
{
    Task DoSomethingAsync(Game game, CancellationToken cancellationToken);
}
```

**Current workaround**
Are you working around this today (reflection, forking, a separate service)? If so, describe how.

**Versions:**
 - LANCommander.Server version: [e.g. 2.2.1]
 - LANCommander.Launcher version: [e.g. 2.2.1]
 - Plugin package version(s) targeted: [e.g. 2.2.1]

**Links**
Link to your plugin's repository or any related issues/discussions, if available.

**Additional context**
Add any other context about the request here.
