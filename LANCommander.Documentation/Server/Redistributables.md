---
title: Redistributables
---

# Overview
Redistributables are common runtimes or libraries that a game might require in order to run. A common list of redistributables may include:
- Microsoft DirectX
- Microsoft Visual C++
- Microsoft .NET Framework
- NVIDIA PhysX
- Java Runtime
- OpenAL

LANCommander supports the ability to host and install these redistributables for clients. Similar to games, redistributable installation relies on scripts and archives.

# Required Configuration
A basic redistributable will need two types of scripts:
- [Detect Install](/Scripting/DetectInstallScripts)
- [Install](/Scripting/InstallScripts)

For more information on variables and requirements, please review the documentation for both script types. It is important to note that both scripts are required, where the **Detect Install** script will be used to verify if the redistributable is already installed and the **Install** script is used to actually handle the installation.

# Archives
In order to send the setup files to the client, an archive must be uploaded to the redistributable. These should be ZIP files and should include any files that might be required for the installation to execute.

# Assigning to Games
Games can be assigned redistributables in two ways:

- When editing a game, use the **Redistributables** multiselect field to choose any applicable redistributable
- When editing a redistributable, you may use the **Games** multiselect field to choose any game that might require the redistributable to be installed

# Install Process
When a game is installed via the [SDK](/SDK) or [launcher](/Launcher), it includes a list of redistributables that have been assigned. For each of these redistributables, the client will execute the [Detect Install](/Scripting/DetectInstallScripts) script. If the script has determined that there is no prior installation, the client will then download the archive and extract it to the user's temp directory. It will then execute the [Install](/Scripting/InstallScripts) script with the working directory set to the destination of the archive's extraction.