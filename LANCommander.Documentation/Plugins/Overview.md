---
sidebar_label: Overview
sidebar_position: 1
---

# Plugin Development

LANCommander ships a plugin framework that lets you extend both the **server** and the **launcher**
without modifying the core applications. A plugin is a standard .NET class library that is discovered
from a drop-in folder at startup, loaded in isolation, and given the opportunity to register services
and hook into the host.

Plugins can, among other things:

- Add new pages, settings sections, game detail tabs, context menu items, and footer widgets to the
  launcher UI.
- Register additional metadata providers on the server.
- Add custom PowerShell cmdlets and modules to the scripting runtime used during installs.
- React to host lifecycle events such as game install, launch, exit, and user login.

## How it works

The framework is intentionally small and built around a few ideas:

- **Discovery by convention.** At startup each host scans its `Plugins` drop-in folder. Every
  subfolder is treated as a candidate plugin; the loader looks for an assembly named after the folder
  (or the single assembly that ships a `.deps.json`) and reads its
  [`[LANCommanderPlugin]`](/Plugins/API%20Reference#lancommanderpluginattribute) assembly attribute.
- **Host targeting.** A plugin declares which hosts it supports (`Server`, `Launcher`, or both). A
  plugin that does not target the current host is skipped.
- **Version gating.** A plugin can declare a minimum and/or maximum compatible host version. Incompatible
  plugins are skipped with a warning rather than loaded.
- **Isolation.** Each plugin is loaded into its own
  [`AssemblyLoadContext`](/Plugins/API%20Reference#pluginloadcontext) so its private dependencies do not
  collide with the host or with other plugins. Contract assemblies shared with the host (the SDK, DI
  abstractions, Avalonia) are deferred to the host so shared types keep a single identity.
- **Two-phase lifecycle.** Because the host builds its dependency injection container exactly once,
  plugins participate in two phases: they register services first, then run an asynchronous
  initialization hook after the container is built. See [Getting Started](/Plugins/Getting%20Started)
  for details.
- **Fault isolation.** A plugin that throws during discovery, configuration, or initialization is
  logged and skipped. A misbehaving plugin cannot crash the host.

## Where to go next

- **[Getting Started](/Plugins/Getting%20Started)** — build, package, and deploy your first plugin.
- **[Extension Points](/Plugins/Extension%20Points)** — a tour of everything a plugin can extend, with
  examples.
- **[API Reference](/Plugins/API%20Reference)** — the full plugin surface, generated directly from the
  source.
