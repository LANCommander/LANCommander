---
sidebar_label: Webhooks
---

# Server events and webhooks

Administrators can create subscriptions under **Settings > Integrations > Webhooks**.
Set a name, HTTP(S) endpoint, signing secret (32-1024 characters), event types,
timeout (1-30 seconds), and maximum attempts (1-10, including the first attempt).
Use a unique random signing secret for each subscription. Keep the same secret at
the receiver; LANCommander never returns a saved secret. Leave the secret field
blank when editing to preserve it.

URLs cannot contain embedded credentials, query strings, or fragments. HTTPS is
recommended; HTTP and private network addresses are permitted for trusted LAN
integrations. Only administrators may configure destinations. Restrict outbound
network access at the host/firewall if administrators must not reach certain
services. Redirects, cookies, proxies, and automatic HTTP-client request logging are
disabled for webhook delivery.

## Event envelope

Event names are stable, lowercase, dot-separated names describing a resource and
its completed transition. Times are UTC; IDs are UUIDs. Consumers should ignore
unknown properties to allow additive schema changes.

```json
{
  "eventId": "8071d99c-cd01-495e-95c7-28355a10a814",
  "eventType": "game.archive.upload_completed",
  "occurredAt": "2026-11-09T05:00:00Z",
  "source": "LANCommander.Server",
  "actorId": null,
  "targetType": "game",
  "targetId": "56ad8a83-31e7-437b-8882-45c5dcf47e28",
  "status": "completed",
  "correlationId": null,
  "payload": {
    "archiveId": "88cc1ede-95fd-45ca-969c-ea4ec210dc81",
    "gameVersionId": "bfdd87ef-f3aa-4ac4-83d6-c24c17d79f47",
    "version": "1.0"
  }
}
```

`actorId` identifies the authenticated user when known, otherwise it is null.
`correlationId` is optional and currently null for built-in events. The status is
`completed`, except for `game.install.failed`, which uses `failed`. Targets
identify the game, user, dedicated server, tool, or redistributable, not a local
file path.

| Events | Payload |
| --- | --- |
| `game.created` | `title` |
| `game.archive.upload_completed` | `archiveId`, `gameVersionId`, `version` |
| `game.play.started` | `sessionId`, `start` |
| `game.play.ended` | `sessionId`, `start`, `end`, `timedOut` |
| `user.login`, `user.logout` | Empty object |
| `game.save.uploaded` | `saveId`, `size`, `platforms` |
| `server.started`, `server.stopped` | `serverName` |
| `game.install.completed`, `game.install.failed`, `game.uninstall.completed`, `tool.installed`, `redistributable.installed` | `reportedByLauncher: true` |
| `webhook.test` | `message` |

Game creation, archive uploads, and play sessions reuse the server's existing
plugin lifecycle events. Archive completion is raised only after an uploaded
archive is stored and linked, not for packages created by import or packaging.
Save completion is raised after the file is written. Ended play sessions include
those ended by the stale-session sweep. Dedicated server notifications describe
transitions into running and out of running, including an unexpected exit; an
initial stopped status or repeated running status does not emit a notification.

Login/logout cover token authentication and application-cookie sign-in/sign-out,
including external providers. Token refresh does not emit a login. Logout means
the client reported a logout or its application cookie was signed out; it does not
mean an already-issued bearer token was revoked.

Installation events are authenticated **launcher reports**, not proof of an
installation. The server supplies the actor and accepts only the five supported
installation outcomes for existing targets. The report contains no local paths,
script output, credentials, or arbitrary payload. Reports to offline/older
servers are best-effort and logged by the launcher; they are not replayed later.
Older launchers do not report these outcomes. Already-detected redistributables
do not emit a new installation notification. Both queued launcher installations
and direct SDK game installations report their outcomes; canceled direct
installations do not report a failure.

## Signing and receiving

Each POST contains UTF-8 JSON with these headers:

| Header | Meaning |
| --- | --- |
| `X-LANCommander-Event-Id` | Stable event ID across subscriptions and retries |
| `X-LANCommander-Delivery-Id` | Stable ID for this subscription's delivery |
| `X-LANCommander-Timestamp` | Unix timestamp in seconds, regenerated for each attempt |
| `X-LANCommander-Signature` | `sha256=` followed by lowercase hexadecimal HMAC-SHA256 |

The signature covers the UTF-8 bytes of `timestamp + "." + raw_request_body`,
using the UTF-8 signing secret as the key. Verify against the **raw bytes**, not
reformatted/deserialized JSON; compare signatures in constant time, reject stale
timestamps (for example, over five minutes old), and deduplicate by delivery ID.
Return a 2xx response promptly, then do expensive work asynchronously.

For example, the receiver computes:

```csharp
var signed = Encoding.UTF8.GetBytes(timestamp + "." + rawBody);
var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signed);
var supplied = Convert.FromHexString(signature["sha256=".Length..]);
bool valid = CryptographicOperations.FixedTimeEquals(expected, supplied);
```

Validate the header format, timestamp, and signature length before comparison.
Never log the signing secret or untrusted response bodies.

## Delivery behavior and administration

Matching, enabled subscriptions get database-backed pending deliveries before an
event is handed to Rebus. HTTP runs in a background consumer, not in the original
upload/login/installation request. Pending deliveries are recovered every five
seconds, including after a server restart. Up to four deliveries and consumers
run concurrently, so a slow destination does not delay core operations or hold up
all other notifications. Each attempt is claimed with a conditional database
update, so concurrent handlers (including other server instances sharing the
database) never send the same attempt twice.

Any non-2xx response, timeout, or connection failure retries with exponential
backoff: 5, 10, 20, 40 seconds, and so on, capped at one hour. The configured
attempt limit is absolute, including attempts interrupted by a restart. Delivery
is at-least-once within that limit: a receiver may accept a request immediately
before the server exits without recording success. There is no global ordering
guarantee when retries are involved.

**Deliveries** shows the latest 100 records, including IDs, pending/delivered/
failed/canceled status, attempt count, next attempt time, HTTP status, and a
redacted failure classification. Response bodies, reason phrases, exception
messages, and secrets are not retained. Disable a subscription to cancel pending
work when it is next processed; an HTTP request already in flight may finish.
Edits apply to pending deliveries, including key rotation and destination changes.
Deleting a subscription removes its pending deliveries and history. History is
otherwise retained until the subscription is deleted.

**Test** queues a signed `webhook.test` event for that subscription only,
regardless of its event filter; disabled subscriptions cannot be tested. Refresh
the delivery list to see the result. A test payload has `targetType: "webhook"`,
the subscription's ID as `targetId`, and:

```json
{ "message": "LANCommander test webhook" }
```

Administrator API endpoints are `/api/Webhooks`, `/api/Webhooks/EventTypes`,
`/api/Webhooks/{id}`, `/api/Webhooks/{id}/Test`, and
`/api/Webhooks/{id}/Deliveries`. Create with POST, edit with PUT, and delete with
DELETE. Creation/edit accepts:

```json
{
  "name": "Archive announcements",
  "endpoint": "https://receiver.example/events",
  "secret": "replace-with-a-unique-random-secret-at-least-32-characters",
  "eventTypes": ["game.archive.upload_completed"],
  "enabled": true,
  "timeoutSeconds": 10,
  "maxAttempts": 5
}
```

Secrets are encrypted with ASP.NET Core Data Protection. Preserve the server
identity's Data Protection key ring as well a## Architecture and plugins

Core producers use `IServerEventPublisher`, not HTTP or a notification provider.
The common `ServerEvent` message is carried by Rebus using System.Text.Json.
`ServerEvent`, `ServerEventTypes`, and the extension points below are in the
`LANCommander.Server.Plugins.Messaging` namespace of the server plugin package.
Register implementations from a plugin's `ConfigureServices`.

**Consumers.** Implement `IServerEventConsumer` to react to every published event,
for example to post to Slack. Webhooks are the built-in consumer. Consumers run in
the background after the originating operation and are isolated: an exception is
logged (type only) and does not affect other consumers. Consumers own their own
filtering, retries, persistence, and secrets; they do not inherit webhook
durability.

```csharp
public sealed class SlackPlugin : IPlugin
{
    public void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<IServerEventConsumer, SlackEventConsumer>();
}

public sealed class SlackEventConsumer : IServerEventConsumer
{
    public async Task HandleAsync(ServerEvent serverEvent, CancellationToken cancellationToken)
    {
        if (serverEvent.EventType != ServerEventTypes.ArchiveUploaded)
            return;
        // Post to Slack, handling your own retries.
    }
}
```

**Transports.** By default events travel over an in-process transport that needs
no broker. Implement `IServerEventTransport` to replace it, for example with a
RabbitMQ transport from the corresponding Rebus package. If several are
registered, the first is used and a warning is logged. All instances consume from
the `server-events` queue, so each event, and each scheduled webhook delivery, is
processed by one instance. Transports without native publish/subscribe must also
configure subscription storage through `ConfigureSubscriptions`.

```csharp
public sealed class RabbitMqTransport(string connectionString) : IServerEventTransport
{
    public void ConfigureTransport(StandardConfigurer<ITransport> transport, string queueName) =>
        transport.UseRabbitMq(connectionString, queueName);
}
```

## Limitations

Webhook deliveries are durable once persisted, but the originating domain change
and notification persistence are **not one atomic transaction**. A process exit
between them, or a database failure while publishing, can lose a notification.
Publication failures are logged without turning a successful operation into a
failure. With the default transport, events not yet handed to a plugin consumer
are lost if the process exits; webhooks recover from the database.

Delivery is at-least-once: receivers must deduplicate by delivery ID. Running
several instances requires them to share the database, the Data Protection key
ring, and a broker transport. Routing commands to a particular instance (for
example satellite servers) is not part of this event system. Plugins cannot yet
publish custom event types. Discord-specific formatting is a follow-up; a generic
event body cannot be posted directly to a Discord webhook.
