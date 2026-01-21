# Cookie Policy
:::warning
This section is for advanced users only. You should not have to modify these options unless your setup is more complex.
:::

LANCommander allows for the customization of security policy for authentication cookies. If you are seeing `Correlation failed.` in your logs, your cookie policy is probably misconfigured.

LANCommander by default provides the following policy:

```yaml
HTTP:
- SameSite: Lax
- Secure: None
HTTPS:
- SameSite: None
- Secure: SameAsRequest
```

Different configurations for HTTP/S are required to adhere to modern browser practices. Most modern browsers (and by extension, the LANCommander Launcher) will reject the storage of any cookie on HTTP responses if `SameSite` is set to `None`.

The default policy above is by far not the most secure configuration out there, but is flexible for the majority of installations. If you would like to read more about the available options, review the [official ASP.NET Core documentation](https://learn.microsoft.com/en-us/aspnet/core/security/samesite?view=aspnetcore-9.0).