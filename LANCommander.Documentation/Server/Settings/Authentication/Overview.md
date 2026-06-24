---
title: Authentication
sidebar_label: Overview
sidebar_position: 1
---

# Authentication

LANCommander can delegate sign-in to external identity providers in addition to its
built-in local accounts. Two provider protocols are supported:

- **OpenID Connect (OIDC)** — recommended. The provider exposes a discovery document
  (the *well-known configuration URL*) that LANCommander uses to resolve all of its
  endpoints automatically.
- **OAuth2** — for providers that do not offer OIDC discovery. You supply each endpoint
  (authorization, token, user info) by hand.

:::info
SAML is listed in the provider type list but is **not implemented**. Selecting it will
prevent the provider from being registered.
:::

Providers are configured under **Settings → Authentication → External Providers**. A
**server restart is required** for changes to authentication providers to take effect.

## Configuring a provider

Each provider shares a common set of fields, plus a few that depend on the type.

| Field | Applies to | Description |
| --- | --- | --- |
| Name | All | Display name shown on the login button. |
| Color / Icon | All | Styling for the login button. |
| Type | All | `OAuth2` or `OpenIdConnect`. |
| Client ID / Client Secret | All | Credentials issued by the provider. |
| Configuration Endpoint | OIDC | The provider's `.well-known/openid-configuration` URL. |
| Authorization / Token / User Info Endpoint | OAuth2 | The provider's individual endpoints. |
| Scopes | All | Scopes requested during sign-in (see below). |
| Claim Mappings | All | How provider claims map onto LANCommander users (see below). |

### Redirect URLs

When registering LANCommander with your provider, configure the redirect (callback) URL
to match the protocol:

| Type | Redirect URL |
| --- | --- |
| OpenID Connect | `http(s)://<ServerAddress>/SignInOIDC` |
| OAuth2 | `http(s)://<ServerAddress>/SignInOAuth` |

:::info
If you see `Correlation failed.` errors in the logs, review your
[cookie policy settings](/Server/Settings/Authentication/Security).
:::

## Scopes

Scopes determine which information the provider releases during sign-in. At minimum an
OIDC provider needs `openid`; `profile` and `email` are commonly added so the user's
name and email claims are returned. Some providers expose a `roles` or `groups` scope
for [role synchronization](#role-synchronization).

## Claim mappings

A **claim mapping** projects a claim returned by the provider onto a destination claim
that LANCommander understands and applies to the user on login.

- **Claim** (the source) is a key in the provider's user-info response, e.g.
  `preferred_username`.
- **Destination** (the target) is one of the well-known names below.

For OIDC providers the configured claim mappings run over the user-info endpoint
response, so make sure the scopes you request actually cause those claims to be returned.

### Recognized destinations

| Destination | Maps to | Notes |
| --- | --- | --- |
| `nameidentifier` | External unique ID | **Required** — links the provider login to a LANCommander account. |
| `name` | Username | |
| `email` | Email address | |
| `alias` | Display alias | |
| `role` (or `roles`) | Role name(s) | Array values are expanded into multiple roles; nested keys are supported with dotted paths (e.g. `realm_access.roles`). Each value is used directly as a role name. |

The full `http://schemas.xmlsoap.org/...` claim URIs are also accepted for `name`,
`email`, and `nameidentifier`. When no username claim is available (or it collides with
an existing local account), the user is sent to manual registration to finish linking.

## Discovery (OIDC)

For OpenID Connect providers, the **Discover** button next to the claim mappings reads
the provider's discovery document and configures the provider for you:

- **Standard claims are mapped automatically.** When the provider advertises them, the
  following are mapped:

  | Destination | Source claim (first advertised wins) |
  | --- | --- |
  | `nameidentifier` | `sub` |
  | `email` | `email` |
  | `name` | `preferred_username` → `name` → `username` |
  | `alias` | `nickname` → `name` |
  | `role` | `roles` → `groups` |

- **Base scopes are added automatically.** `openid` is always added (it is required for
  the OIDC flow); `profile`, `email`, `roles`, and `groups` are added when the provider
  advertises them.
- Any other advertised claims appear as clickable suggestions you can add as mappings,
  and as autocomplete options while editing a mapping.

Discovery never overwrites mappings or scopes you have already configured, and re-running
it adds nothing new.

:::info
The discovery document's `claims_supported` and `scopes_supported` lists are **advisory**.
They are optional in the OIDC spec and many providers under-report them, so treat the
results as suggestions — you can always add claims and scopes manually.
:::

## Role synchronization

When a provider login supplies role claims (mapped to `role`), LANCommander syncs the
user's roles on every login:

- Roles named in the claims that don't yet exist are created automatically.
- Roles the user no longer has in the claims are removed — **except** the Administrator
  role and the configured default role, which are never removed automatically.

## Provider examples

See the [External Providers](/Server/Settings/Authentication/External%20Providers/Authentik)
section for ready-to-use configuration examples.
