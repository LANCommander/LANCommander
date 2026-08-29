---
sidebar_label: Overview
sidebar_position: 1
---

# Packaging

Packaging turns a game's own installer into an `.LCX` package your LANCommander server can serve. The launcher runs the installer, watches every file and registry change it makes, and walks you through selecting what belongs in the package.

Instead of manually creating archives, writing install scripts and filling out metadata by hand, the whole thing is one guided workflow — and because it runs inside the launcher, it uses the session you are already signed in with.

- [Getting Started](/Packaging/Getting%20Started) - requirements and how monitoring works
- [Wizard Walkthrough](/Packaging/Wizard) - step-by-step guide through the seven stages
- [LCX Package Format](/Packaging/LCX%20Format) - internal structure of `.LCX` files

## Requirements

- **Windows.** Monitoring works by injecting a native hook DLL into the installer, which has no equivalent on Linux or macOS. The menu entry is hidden on other platforms.
- **An administrator account on the server.** Packaging is offered only to accounts that may create games, which is the same permission the server enforces on upload and import.

## How it works

The launcher itself never injects anything. It starts small **worker** processes — one per architecture — and those do the injecting, reporting what they see back over a named pipe.

That split exists for a concrete reason: DLL injection only works between processes of the same bitness. A 64-bit process cannot inject into a 32-bit installer, or the reverse. Running the workers out of process means only they need to ship per architecture, and it means one installer that spawns children of both bitnesses is still captured completely — every worker reports into a single merged change set in the launcher.

Workers start without elevation. If the installer turns out to need administrator rights, the launcher offers to restart the capture elevated, which costs a single UAC prompt for the whole session.
