# LANCommander.SDK
The LANCommander SDK is a .NET 8 assembly designed to allow developers to implement their own client. Most of the core functionality of the official launcher is implemented directly in the SDK and currently supports functionality such as:
- Authentication to a LANCommander server
- Installation of games / redistributables
- Launching of games and play session tracking
- Media linking and downloading
- Game save uploading and downloading, including packing and extraction
- PowerShell script execution
- Updating player's alias and custom fields
- Submission of issues
- Game lobby scanning

# Basic Use
In order to do almost anything through the SDK, you must have an authenticated instance of the `LANCommander.SDK.Client` class. Here is an example of how to instantiate the class and authenticate to a server:
```csharp
var client = new LANCommander.SDK.Client("http://localhost:1337", "C:\\Games");

await client.AuthenticateAsync("username", "password");
```

Once the client is authenticated, everything you need is available as a manager under the client. For example, to install a game by ID you might use the following:

```csharp
await client.Games.InstallAsync("114f653d-ea91-484b-8fe9-8e9bb58bde81");
```

# Documentation
For full documentation, review the [official LANCommander documentation](https://docs.lancommander.app/en/SDK).