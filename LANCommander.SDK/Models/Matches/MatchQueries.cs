using System;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Models
{
    /// <summary>Filters for listing matches. Empty filters match everything.</summary>
    public class MatchQuery
    {
        public Guid? GameId { get; set; }
        public Guid? ServerId { get; set; }

        /// <summary>Only matches the user took part in.</summary>
        public Guid? UserId { get; set; }

        public MatchStatus? Status { get; set; }
        public string Map { get; set; }
        public string Mode { get; set; }

        /// <summary>Only matches that started at or after this time, UTC.</summary>
        public DateTime? From { get; set; }

        /// <summary>Only matches that started before this time, UTC.</summary>
        public DateTime? To { get; set; }

        /// <summary>1-based.</summary>
        public int Page { get; set; } = 1;

        /// <summary>At most 500.</summary>
        public int PageSize { get; set; } = 50;
    }

    /// <summary>Filters for a match's events, which are returned in game-time order.</summary>
    public class MatchEventQuery
    {
        public string Type { get; set; }
        public string ActorKey { get; set; }
        public string SubjectKey { get; set; }

        /// <summary>Only events after this many milliseconds of game time.</summary>
        public long? AfterGameTimeMilliseconds { get; set; }

        public int Page { get; set; } = 1;

        /// <summary>At most 5000.</summary>
        public int PageSize { get; set; } = 500;
    }

    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public enum MatchStatisticAggregation
    {
        Sum,
        Average,
        Minimum,
        Maximum,
        Count,
    }

    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public enum MatchStatisticGrouping
    {
        /// <summary>One row per LANCommander user. Participants not linked to a user are left out.</summary>
        User,

        /// <summary>One row per player key, linked to a user or not.</summary>
        PlayerKey,
    }

    /// <summary>Aggregates one statistic of a game across its matches, such as total kills per user.</summary>
    public class MatchStatisticQuery
    {
        public Guid GameId { get; set; }

        /// <summary>The statistic, such as <c>kills</c> or <c>best_lap_ms</c>.</summary>
        public string Key { get; set; }

        public MatchStatisticAggregation Aggregation { get; set; } = MatchStatisticAggregation.Sum;
        public MatchStatisticGrouping GroupBy { get; set; } = MatchStatisticGrouping.User;

        public Guid? ServerId { get; set; }
        public string Map { get; set; }
        public string Mode { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        /// <summary>Leave out abandoned matches. On by default.</summary>
        public bool ExcludeAbandoned { get; set; } = true;

        /// <summary>Highest value first. Turn off for statistics where lower is better, such as lap times.</summary>
        public bool Descending { get; set; } = true;

        /// <summary>At most 1000.</summary>
        public int Limit { get; set; } = 100;
    }
}
