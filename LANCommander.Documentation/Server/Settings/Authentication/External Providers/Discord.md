# Discord

In order to enable authentication via Discord, you must create an app in the [Discord Developers Portal](https://discord.com/developers/applications). Once an application is created in the portal, you can retrieve the client ID and secret from the **OAuth2** section.

The external provider in LANCommander should be configured with the following information:
```yaml
Type: OAuth2
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

## Claim mappings

Discord does not use OIDC discovery, so its claims must be mapped by hand. Without these
mappings login attempts fail with an unhandled exception, because LANCommander cannot
resolve the required `nameidentifier` claim from Discord's user-info response.

Configure the following claim mappings, where the **Claim** (source) is the field
returned by `https://discord.com/api/users/@me` and the **Destination** is the
LANCommander target:

| Claim (source) | Destination |
| --- | --- |
| `id` | `nameidentifier` |
| `email` | `email` |
| `username` | `name` |
| `global_name` | `alias` |

In the provider's YAML the same mappings look like this:
```yaml
ClaimMappings:
    - Name: http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier
      Value: id
    - Name: http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress
      Value: email
    - Name: http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name
      Value: username
    - Name: alias
      Value: global_name
```

:::info
The `nameidentifier` mapping is **required** — it links the Discord account to a
LANCommander user. See the [Authentication overview](/Server/Settings/Authentication/Overview#claim-mappings)
for the full list of recognized destinations.
:::