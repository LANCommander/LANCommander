---
title: Match Statistics
---

# Match Statistics

The server can collect what happens in multiplayer matches: lap times and final standings in a racing game, who killed whom in a shooter, when a player was defeated or how many units they built in an RTS. Matches are grouped by game and can be tagged with the game server they were played on. Players are linked to LANCommander users wherever the server can tell who they are.

The server doesn't draw any reports from this data. It exposes the data through a JSON API, which tools such as Grafana can query, and through plugin interfaces, so plugins can build leaderboards, tournament brackets and the like.

## Concepts

| Term | Meaning |
| --- | --- |
| Match | One played session: a race, a round set, a skirmish. Holds the map, mode, game version, the server, and the rules it was played with (`settings`, a JSON object). |
| Reporter | Whatever sends a match's events: a game server, a tool watching one, a player's client, or a plugin. A match can have several. |
| Participant | A player, bot or team member, identified by a **player key**. Linked to a user when known. |
| Event | One thing that happened, with a type, game time, who did it (`actorKey`), who it happened to (`subjectKey`) and a JSON `payload`. Events are never edited. |
| Result | A player's or team's place in the final standings, as the game decided it. |
| Statistic | A number counted for a player or team by the end of the match: `kills`, `resources_collected`, `best_lap_ms`. |
| Attachment | A file kept with the match, such as a demo, a replay or a server log. |

### Player keys

Players are referred to by a key made of a kind and a value: `steam:STEAM_0:1:12345`, `name:Pat`, `bot:3`. A key without a kind is treated as a name. Names match regardless of case; other kinds have to match exactly.

## Trust

How far a reporter's data is trusted depends on the account it signed in with, never on what the reporter says about itself:

- **Administrators and plugins** are *authoritative*. They can name the user a participant is, and they can send late events at any time.
- **Everyone else** is a player's own client (*untrusted*). Their events are stored, but they can only report while the match is going, plus 15 minutes after it ends. They can't end a match that a trusted reporter is also recording.

A dedicated game server should report with a personal access token owned by an administrator and limited to **Report matches** (see [Access tokens](#access-tokens)).

## How players are linked to users

The server tries these in order for each participant:

1. **Named by a trusted reporter.** The participant's `userId`, when an administrator or plugin sends one.
2. **A known identity.** A user can register their in-game ids, such as a Steam ID, with `POST /api/Matches/Identities`. Administrators and plugins can register ids for anyone. Registering an id also links participants that were already recorded under it.
3. **Playing on the server under that name.** When the launcher starts a game it tells the server which game server the player is joining and the name they play under. The server learns the game server from a server's Connect or Join action, or from the Interposer when it sees the game connect to one of its servers. If exactly one user was playing the game on the match's server under the participant's name during the match, they're linked. The name is then remembered for that game, so later matches link straight away.

Participants that can't be linked are kept as reported. The server retries recent ones every few minutes.

## Reporting a match

Every endpoint is under `/api/Matches` and needs a bearer token.

### 1. Create the match

```http
POST /api/Matches
{
  "gameId": "…",
  "serverId": "…",
  "externalId": "round-2026-11-07-2215",
  "map": "de_dust2",
  "mode": "competitive",
  "settings": { "mp_roundtime": 2, "mp_maxrounds": 30 },
  "reporterKind": "Server",
  "reporterName": "cs-01"
}
```

The response holds the `matchId` and your `reporterId`. Sending the same `externalId` for the same game again returns the existing match, along with the last sequence number the server wrote for you. A reporter that restarts can therefore pick up where it left off.

### 2. Send events in batches

```http
POST /api/Matches/{matchId}/Events
{
  "reporterId": "…",
  "events": [
    {
      "id": "0192f6a4-6c1e-7b3a-9f10-2a4d5e6f7a8b",
      "sequence": 412,
      "type": "kill",
      "gameTimeMilliseconds": 183245,
      "actorKey": "steam:STEAM_0:1:7731",
      "subjectKey": "steam:STEAM_0:0:9902",
      "payload": { "weapon": "ak47", "headshot": true }
    }
  ]
}
```

- `id` is generated when the event happens and reused on every retry. The server drops repeats, so resending is always safe.
- `sequence` starts at 1 for each reporter and goes up by 1. Gaps are counted on the reporter.
- `gameTimeMilliseconds` is game time since the match started, taken from the game's clock.
- Send up to 500 events per request, every few seconds.
- Use lowercase event types with underscores. Give durations as whole milliseconds with a `_ms` suffix, and leave out derived values such as rates.

The server answers `202` once the events are queued. It lists any events it will never accept, which shouldn't be retried. If the server is behind on writing it answers `429` with `Retry-After`; send the same events again after that.

### 3. End the match

```http
POST /api/Matches/{matchId}/End
{
  "reason": "completed",
  "results": [
    { "playerKey": "steam:STEAM_0:1:7731", "rank": 1, "score": 24 },
    { "playerKey": "steam:STEAM_0:0:9902", "rank": 2, "score": 19 }
  ],
  "statistics": [
    { "playerKey": "steam:STEAM_0:1:7731", "key": "kills", "value": 24 },
    { "playerKey": "steam:STEAM_0:1:7731", "key": "deaths", "value": 11 }
  ]
}
```

Sending results again replaces them. Statistics replace the same key for the same player. A reason of `aborted`, or `"abandoned": true`, marks the match **Abandoned**, so leaderboards leave it out. An open match that goes 30 minutes without events is abandoned automatically.

### Attachments

Large files are sent in chunks:

1. `POST /api/Matches/{matchId}/Attachments` with the `kind`, `fileName`, `size`, `sha256` and, optionally, the game time range the file covers.
2. `PUT /api/Matches/{matchId}/Attachments/{attachmentId}/Content?offset=…` with each chunk as the raw body, in any order.
3. `POST /api/Matches/{matchId}/Attachments/{attachmentId}/Complete`. The server checks the size and hash.

Download with `GET /api/Matches/{matchId}/Attachments/{attachmentId}/Download`.

### From .NET

The SDK's `MatchClient` does all of this. `StartRecordingAsync` returns a `MatchRecorder` that numbers events, sends them in batches and waits out a busy server:

```csharp
await using var recorder = await matchClient.StartRecordingAsync(new MatchCreateRequest { GameId = gameId, Map = "Monza" });

recorder.Record("lap_completed", gameTimeMilliseconds: 81234, actorKey: "name:Pat", payload: new { lap = 2, lap_time_ms = 81234 });

await recorder.EndAsync(new MatchEndRequest { Reason = "completed", Results = { … } });
```

## Reading matches

| Endpoint | Returns |
| --- | --- |
| `GET /api/Matches?gameId=&serverId=&userId=&status=&map=&mode=&from=&to=&page=&pageSize=` | Matches, newest first |
| `GET /api/Matches/{matchId}` | A match with its reporters, participants, results, statistics and attachments |
| `GET /api/Matches/{matchId}/Events?type=&actorKey=&subjectKey=&afterGameTimeMilliseconds=` | A match's events in game-time order |
| `GET /api/Matches/Statistics?gameId=&key=&aggregation=Sum&groupBy=User&descending=true&limit=` | One statistic aggregated across matches: a leaderboard |

`aggregation` is one of `Sum`, `Average`, `Minimum`, `Maximum` or `Count`. `groupBy` is `User` or `PlayerKey`. For statistics where lower is better, such as lap times, set `descending=false`.

### Grafana

Use the [Infinity](https://grafana.com/grafana/plugins/yesoreyeram-infinity-datasource/) data source:

1. Create a personal access token with **Read matches** access.
2. Add an Infinity data source with the server's address as an allowed host, and the token as a bearer token.
3. Add a panel with type **JSON** and a URL such as `https://lancommander.local/api/Matches/Statistics?gameId=…&key=kills`. Use `userName` and `value` as the columns.

For match lists, point the panel at `/api/Matches` and use `items` as the root selector.

## Access tokens

Under **Profile → Access Tokens**, two kinds of token are limited to match statistics:

- **Report matches** works only on `/api/Matches`, for a game server or tool that reports matches.
- **Read matches** can only read `/api/Matches`, for dashboards.

## Plugins

Plugins don't need HTTP:

- `IMatchStatisticsSink` records matches as a trusted reporter. A plugin that parses a game server's log would write through it.
- `IMatchStatisticsProvider` reads matches and aggregates statistics.
- `MatchCreatedEvent`, `MatchEventsRecordedEvent`, `MatchEndedEvent` and `MatchParticipantLinkedEvent` are raised on the plugin event bus. `MatchEndedEvent` carries the final standings, which is what a tournament bracket needs.

## Settings

Under `MatchStatistics` in the server's settings:

| Setting | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `true` | Accept match reports |
| `QueueCapacity` | `50000` | Events held in memory waiting to be written; beyond this reporters get `429` |
| `BatchSize` | `500` | Events written per transaction |
| `FlushInterval` | `250` | Milliseconds the writer waits to fill a batch |
| `MaxEventsPerRequest` | `500` | Events accepted per request |
| `MaxPayloadSize` | `16` | Largest event payload, in KB |
| `AbandonAfter` | `30` | Minutes without events before an open match is abandoned; `0` never abandons |
| `MaxAttachmentSize` | `2048` | Largest attachment, in MB |
| `AttachmentRetention` | `0` | Days attachments are kept; `0` keeps them |
| `PendingAttachmentTimeout` | `24` | Hours an upload may stay incomplete |

Events are written by a single background writer, so reporting never waits on the database. Events that are queued but not yet written are lost if the server stops abruptly. A reporter covers this by resending everything after its reporter's `lastSequence` (`GET /api/Matches/{matchId}/Reporters/{reporterId}`).
