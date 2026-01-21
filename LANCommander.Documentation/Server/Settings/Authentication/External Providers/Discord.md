# Discord

In order to enable authentication via Discord, you must create an app in the [Discord Developers Portal](https://discord.com/developers/applications). Once an application is created in the portal, you can retrieve the client ID and secret from the **OAuth2** section.

The external provider in LANCommander should be configured with the following information:
```yaml
Name: Discord
ClientId: <Your Application Client ID>
ClientSecret: <Your Application Client Secret>
Authority: https://discord.com/api
AuthorizationEndpoint: https://discord.com/api/oauth2/authorize
TokenEndpoint: https://discord.com/api/oauth2/token
UserInfoEndpoint: https://discord.com/api/users/@me
Scopes:
    - identify
    - email
    - guilds
```