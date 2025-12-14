## Getting Started
The SDK relies on .NET dependency injection in order to expose services to the consuming application. Use of the SDK can be implemented by calling the extension method for registering the services:

```csharp
builder.Services.AddLANCommanderClient<Settings>();
```

The generic type parameter (here `Settings`) can be used to allow extension of settings by providing your own class that inherits from `LANCommander.SDK.Settings`.

## Configuration
Standard .NET configuration has been implemented in the SDK. By default, `IOptions<Settings>` can be used to retrieve any settings, while `SettingsProvider` is to be used for updating any settings. Any configuration that gets bound to the `Settings` class will be stored in a file called `Settings.yml` on update.

## Authentication
By injecting `LANCommander.SDK.AuthenticationClient`, you can authenticate against a LANCommander server: