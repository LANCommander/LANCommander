using System;
using System.Collections.Generic;
using System.Text.Json;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Models
{
    /// <summary>A match as listed: what was played, where and when.</summary>
    public class MatchSummary
    {
        public Guid Id { get; set; }
        public Guid GameId { get; set; }
        public string GameTitle { get; set; }

        /// <summary>The LANCommander-managed server it was played on, if any.</summary>
        public Guid? ServerId { get; set; }

        /// <summary>The server's name: the managed server's, or the one the reporter gave.</summary>
        public string ServerName { get; set; }

        public string ServerAddress { get; set; }
        public string ExternalId { get; set; }
        public string Map { get; set; }
        public string Mode { get; set; }
        public string GameVersion { get; set; }
        public MatchStatus Status { get; set; }
        public DateTime? StartedOn { get; set; }
        public DateTime? EndedOn { get; set; }
        public string EndReason { get; set; }
        public int ParticipantCount { get; set; }
        public DateTime CreatedOn { get; set; }
    }

    /// <summary>A match with everyone in it, its standings and totals.</summary>
    public class MatchDetail : MatchSummary
    {
        public JsonElement? Settings { get; set; }
        public JsonElement? Metadata { get; set; }
        public List<MatchReporterInformation> Reporters { get; set; } = new();
        public List<MatchParticipantInformation> Participants { get; set; } = new();
        public List<MatchResultInformation> Results { get; set; } = new();
        public List<MatchStatisticInformation> Statistics { get; set; } = new();
        public List<MatchAttachmentInformation> Attachments { get; set; } = new();
    }

    public class MatchReporterInformation
    {
        public Guid Id { get; set; }
        public MatchReporterKind Kind { get; set; }
        public MatchReporterTrust Trust { get; set; }
        public string Name { get; set; }
        public Guid? UserId { get; set; }
        public string PluginId { get; set; }

        /// <summary>The highest sequence number written. Resend everything after it when resuming.</summary>
        public long LastSequence { get; set; }

        /// <summary>How many sequence numbers were skipped.</summary>
        public long GapCount { get; set; }
    }

    public class MatchParticipantInformation
    {
        public Guid Id { get; set; }
        public string PlayerKey { get; set; }
        public string DisplayName { get; set; }
        public string Team { get; set; }

        /// <summary>The LANCommander user the player is, when known.</summary>
        public Guid? UserId { get; set; }

        /// <summary>The user's name, or alias when they have one.</summary>
        public string UserName { get; set; }

        public MatchParticipantLinkSource LinkSource { get; set; }
        public long? JoinedAtMilliseconds { get; set; }
        public long? LeftAtMilliseconds { get; set; }
    }

    public class MatchEventInformation
    {
        public Guid Id { get; set; }
        public Guid MatchId { get; set; }
        public Guid ReporterId { get; set; }
        public long Sequence { get; set; }
        public string Type { get; set; }
        public long GameTimeMilliseconds { get; set; }
        public long? Tick { get; set; }
        public string ActorKey { get; set; }
        public string SubjectKey { get; set; }
        public JsonElement? Payload { get; set; }
        public DateTime? ClientTime { get; set; }
        public DateTime ReceivedOn { get; set; }
    }

    public class MatchResultInformation
    {
        public Guid? ParticipantId { get; set; }
        public string PlayerKey { get; set; }
        public Guid? UserId { get; set; }
        public string Team { get; set; }
        public int Rank { get; set; }
        public double? Score { get; set; }
        public MatchResultStatus Status { get; set; }
        public long? TimeMilliseconds { get; set; }
        public JsonElement? Payload { get; set; }
    }

    public class MatchStatisticInformation
    {
        public Guid? ParticipantId { get; set; }
        public string PlayerKey { get; set; }
        public Guid? UserId { get; set; }
        public string Team { get; set; }
        public string Key { get; set; }
        public double Value { get; set; }
    }

    public class MatchAttachmentInformation
    {
        public Guid Id { get; set; }
        public Guid MatchId { get; set; }
        public Guid? ReporterId { get; set; }
        public MatchAttachmentKind Kind { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public long Size { get; set; }
        public string Sha256 { get; set; }
        public MatchAttachmentStatus Status { get; set; }
        public long? StartMilliseconds { get; set; }
        public long? EndMilliseconds { get; set; }
        public DateTime? UploadedOn { get; set; }
        public DateTime? ExpiresOn { get; set; }
    }

    public class PlayerIdentityInformation
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid? GameId { get; set; }
        public string PlayerKey { get; set; }
        public PlayerIdentitySource Source { get; set; }
        public DateTime CreatedOn { get; set; }
    }

    /// <summary>One row of a statistic aggregated across matches: a leaderboard entry.</summary>
    public class MatchStatisticAggregate
    {
        /// <summary>The user, when grouped by user, or when the player key is linked to one.</summary>
        public Guid? UserId { get; set; }

        public string UserName { get; set; }

        /// <summary>The player key, when grouped by participant.</summary>
        public string PlayerKey { get; set; }

        /// <summary>The last name the player was seen with, when grouped by participant.</summary>
        public string DisplayName { get; set; }

        public double Value { get; set; }

        /// <summary>How many matches contributed to the value.</summary>
        public int MatchCount { get; set; }
    }

    /// <summary>A page of results and the total across all pages.</summary>
    public class MatchPage<T>
    {
        public List<T> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
    }
}
