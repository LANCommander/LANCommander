# Authentik
In order to enable authentication via Authentik, you must create an OIDC application/provider. LANCommander supports OIDC configuration URLs, so setup is easy.

The external provider in LANCommander should be configured with the following information:
```yaml
Type: OIDC
Name: Authentik
ClientId: <Your Provider Client ID>
ClientSecret: <Your Provider Client Secret>
Configuration URL: <Your Provider OpenID Configuration URL>
Scopes:
    - profile
    - email
```

Make sure to set your redirect URLs appropriately. LANCommander expects the following redirect URL scheme:
```http(s)://<ServerAddress>/SignInOIDC```

:::info
If you're seeing `Correlation failed.` errors in the logs, review your [cookie policy settings](/Server/Settings/Authentication/Security).
:::