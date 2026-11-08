using System;
using System.Collections.Generic;
using System.Text.Json;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Models
{
    /// <summary>
    /// Starts recording a match, or returns the match already recorded under the same
    /// <see cref="ExternalId"/> for the game. Whoever sends it becomes one of the match's reporters.
    /// </summary>
    public class MatchCreateRequest
    {
        public Guid GameId { get; set; }

        /// <summary>The game server the match is played on, when LANCommander manages it.</summary>
        public Guid? ServerId { get; set; }

        /// <summary>
        /// The reporter's own id for the match, such as the game's round or session id. Sending the same
        /// id again for the same game returns the existing match, so a reporter that restarts can carry on.
        /// </summary>
        public string ExternalId { get; set; }

        public string Map { get; set; }
        public string Mode { get; set; }
        public string GameVersion { get; set; }

        /// <summary>For a server LANCommander doesn't manage: its name.</summary>
        public string ServerName { get; set; }

        /// <summary>For a server LANCommander doesn't manage: the address players connect to.</summary>
        public string ServerAddress { get; set; }

        /// <summary>When the match started, UTC. Defaults to when the request arrives.</summary>
        public DateTime? StartedOn { get; set; }

        /// <summary>The rules the match is played with (time limit, score limit, cvars), as a JSON object.</summary>
        public JsonElement? Settings { get; set; }

        /// <summary>Anything else to keep with the match, as a JSON object.</summary>
        public JsonElement? Metadata { get; set; }

        /// <summary>What kind of reporter is sending. Trust is decided by the server, not by this.</summary>
        public MatchReporterKind ReporterKind { get; set; } = MatchReporterKind.Server;

        /// <summary>A name to tell reporters apart, such as the server's host name.</summary>
        public string ReporterName { get; set; }

        /// <summary>Players known when the match starts.</summary>
        public List<MatchParticipantSubmission> Participants { get; set; } = new();
    }

    /// <summary>The match a reporter is recording into and its own id within it.</summary>
    public class MatchRegistration
    {
        public Guid MatchId { get; set; }
        public Guid ReporterId { get; set; }

        /// <summary>False when the match already existed under the same external id.</summary>
        public bool Created { get; set; }

        /// <summary>
        /// The highest sequence number the server has written for this reporter. A reporter resuming a
        /// match resends everything after it.
        /// </summary>
        public long LastSequence { get; set; }

        public MatchStatus Status { get; set; }
    }

    /// <summary>A player, bot or team member taking part in a match.</summary>
    public class MatchParticipantSubmission
    {
        /// <summary>
        /// The game's id for the player, prefixed by its kind: <c>steam:STEAM_0:1:123</c>,
        /// <c>name:Pat</c>, <c>bot:3</c>. Events and results refer to the player by this key.
        /// </summary>
        public string PlayerKey { get; set; }

        public string DisplayName { get; set; }
        public string Team { get; set; }

        /// <summary>
        /// The LANCommander user the player is, when the reporter knows. Only used from trusted
        /// reporters; otherwise the server works it out.
        /// </summary>
        public Guid? UserId { get; set; }

        public long? JoinedAtMilliseconds { get; set; }
        public long? LeftAtMilliseconds { get; set; }
    }

    /// <summary>A batch of events from one reporter.</summary>
    public class MatchEventBatch
    {
        /// <summary>The reporter the events are from, as returned by <see cref="MatchRegistration"/>.</summary>
        public Guid ReporterId { get; set; }

        public List<MatchEventSubmission> Events { get; set; } = new();
    }

    /// <summary>
    /// One thing that happened in a match. Every event has the same envelope; only
    /// <see cref="Payload"/> is specific to the game.
    /// </summary>
    public class MatchEventSubmission
    {
        /// <summary>
        /// Generated when the event happens and reused on every retry. Sending an id again is ignored.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>Starts at 1 for each reporter in each match and increases by 1. Never reused.</summary>
        public long Sequence { get; set; }

        /// <summary>
        /// What happened, in lowercase with underscores: <c>kill</c>, <c>lap_completed</c>,
        /// <c>player_defeated</c>.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Milliseconds of game time since the reporter saw the match start. Use the game's clock, so
        /// pauses don't count.
        /// </summary>
        public long GameTimeMilliseconds { get; set; }

        /// <summary>The game's simulation tick, when it has one.</summary>
        public long? Tick { get; set; }

        /// <summary>The player key of who caused the event, if anyone.</summary>
        public string ActorKey { get; set; }

        /// <summary>The player key of who it happened to, such as a kill's victim.</summary>
        public string SubjectKey { get; set; }

        /// <summary>
        /// The detail of the event, as a JSON object. Refer to other players by player key, give
        /// durations in whole milliseconds with a <c>_ms</c> suffix, and leave out derived values such
        /// as rates.
        /// </summary>
        public JsonElement? Payload { get; set; }

        /// <summary>The reporter's clock when the event happened, UTC. Informational only.</summary>
        public DateTime? ClientTime { get; set; }
    }

    /// <summary>How the server took a batch of events.</summary>
    public class MatchEventBatchResult
    {
        /// <summary>
        /// Events queued to be written. Repeats of events already written are dropped when written, so
        /// they are counted here too.
        /// </summary>
        public int Queued { get; set; }

        /// <summary>Events that will never be accepted. Don't retry them.</summary>
        public List<MatchEventRejection> Rejected { get; set; } = new();
    }

    public class MatchEventRejection
    {
        public Guid Id { get; set; }

        /// <summary>One of <see cref="MatchEventRejectionCodes"/>.</summary>
        public string Code { get; set; }

        public string Detail { get; set; }
    }

    public static class MatchEventRejectionCodes
    {
        /// <summary>A required field is missing or out of range.</summary>
        public const string Invalid = "invalid";

        /// <summary>The payload is larger than the server accepts.</summary>
        public const string PayloadTooLarge = "payload_too_large";

        /// <summary>The same id or sequence number appears twice in the batch.</summary>
        public const string Duplicate = "duplicate";
    }

    /// <summary>Ends a match with the game's final standings and totals.</summary>
    public class MatchEndRequest
    {
        /// <summary>When the match ended, UTC. Defaults to when the request arrives.</summary>
        public DateTime? EndedOn { get; set; }

        /// <summary>Why it ended, such as <c>completed</c>, <c>time_limit</c> or <c>aborted</c>.</summary>
        public string Reason { get; set; }

        /// <summary>The match was cut short and its results shouldn't count.</summary>
        public bool Abandoned { get; set; }

        /// <summary>
        /// The standings as the game decided them, including tie-breaks and penalties. Sending results
        /// again replaces them.
        /// </summary>
        public List<MatchResultSubmission> Results { get; set; } = new();

        /// <summary>End-of-match totals. Sending a key again for the same player or team replaces it.</summary>
        public List<MatchStatisticSubmission> Statistics { get; set; } = new();

        /// <summary>Players to add or update along with the results.</summary>
        public List<MatchParticipantSubmission> Participants { get; set; } = new();
    }

    /// <summary>One player's or team's place in the final standings. Give a player key, a team, or both.</summary>
    public class MatchResultSubmission
    {
        public string PlayerKey { get; set; }
        public string Team { get; set; }

        /// <summary>1 for the winner. Tied entries share a rank.</summary>
        public int Rank { get; set; }

        public double? Score { get; set; }
        public MatchResultStatus Status { get; set; } = MatchResultStatus.Finished;

        /// <summary>A finishing time in milliseconds, for races.</summary>
        public long? TimeMilliseconds { get; set; }

        /// <summary>Anything else the game reports for the entry, as a JSON object.</summary>
        public JsonElement? Payload { get; set; }
    }

    /// <summary>
    /// A number the game counted for a player or team: kills, resources collected, best lap. Give a
    /// player key or a team.
    /// </summary>
    public class MatchStatisticSubmission
    {
        public string PlayerKey { get; set; }
        public string Team { get; set; }

        /// <summary>What was counted, in lowercase with underscores: <c>kills</c>, <c>best_lap_ms</c>.</summary>
        public string Key { get; set; }

        public double Value { get; set; }
    }

    /// <summary>Declares a file to attach to a match before its bytes are sent.</summary>
    public class MatchAttachmentCreateRequest
    {
        public Guid? ReporterId { get; set; }
        public MatchAttachmentKind Kind { get; set; } = MatchAttachmentKind.Other;
        public string FileName { get; set; }
        public string ContentType { get; set; }

        /// <summary>The file's size in bytes.</summary>
        public long Size { get; set; }

        /// <summary>The file's hex-encoded SHA-256. Checked when the upload completes, if given.</summary>
        public string Sha256 { get; set; }

        /// <summary>Milliseconds of game time where the file starts.</summary>
        public long? StartMilliseconds { get; set; }

        /// <summary>Milliseconds of game time where the file ends.</summary>
        public long? EndMilliseconds { get; set; }
    }

    /// <summary>Links an in-game id to a LANCommander user.</summary>
    public class PlayerIdentitySubmission
    {
        public Guid UserId { get; set; }

        /// <summary>The game the id is used in. Leave empty when it holds for every game, as a Steam ID does.</summary>
        public Guid? GameId { get; set; }

        /// <summary>The player key, such as <c>steam:STEAM_0:1:123</c> or <c>name:Pat</c>.</summary>
        public string PlayerKey { get; set; }
    }
}
