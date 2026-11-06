using System;
using CommunityToolkit.Mvvm.ComponentModel;
using LANCommander.SDK.Models;

namespace LANCommander.Launcher.ViewModels.Components;

/// <summary>
/// One player's presence as the views show it: whether they are playing, and the card that opens
/// over their name. <see cref="PresenceDirectory"/> keeps it current, on the UI thread.
/// </summary>
public partial class PlayerPresenceViewModel : ViewModelBase
{
    public PlayerPresenceViewModel(Guid userId)
    {
        UserId = userId;
    }

    public Guid UserId { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPlaying))]
    private UserPresence? _presence;

    [ObservableProperty]
    private string? _iconUrl;

    [ObservableProperty]
    private string? _elapsedText;

    public bool IsPlaying => Presence != null;

    public string UserName => Presence?.UserName ?? string.Empty;

    public string GameTitle => Presence?.GameTitle ?? string.Empty;

    public Guid GameId => Presence?.GameId ?? Guid.Empty;

    public bool HasIcon => !string.IsNullOrEmpty(IconUrl);

    public string? Details => Presence?.Activity?.Details;

    public string? State => Presence?.Activity?.State;

    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);

    public bool HasState => !string.IsNullOrWhiteSpace(State);

    /// <summary>"3 of 8", when the game reports a party.</summary>
    public string? PartyText => Presence?.Activity is { PartyMax: > 0 } activity
        ? $"{activity.PartySize} of {activity.PartyMax}"
        : null;

    public bool HasParty => PartyText != null;

    /// <summary>An image the game set by URL, shown beside its icon.</summary>
    public string? ImageUrl => Presence?.Activity?.LargeImageUrl;

    public bool HasImage => !string.IsNullOrEmpty(ImageUrl);

    public string? ImageText => Presence?.Activity?.LargeImageText;

    public bool HasElapsed => !string.IsNullOrEmpty(ElapsedText);

    partial void OnPresenceChanged(UserPresence? value)
    {
        OnPropertyChanged(nameof(UserName));
        OnPropertyChanged(nameof(GameTitle));
        OnPropertyChanged(nameof(GameId));
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(HasDetails));
        OnPropertyChanged(nameof(HasState));
        OnPropertyChanged(nameof(PartyText));
        OnPropertyChanged(nameof(HasParty));
        OnPropertyChanged(nameof(ImageUrl));
        OnPropertyChanged(nameof(HasImage));
        OnPropertyChanged(nameof(ImageText));

        Tick(DateTimeOffset.UtcNow);
    }

    partial void OnIconUrlChanged(string? value) => OnPropertyChanged(nameof(HasIcon));

    partial void OnElapsedTextChanged(string? value) => OnPropertyChanged(nameof(HasElapsed));

    /// <summary>Brings the timer up to <paramref name="now"/>.</summary>
    public void Tick(DateTimeOffset now) => ElapsedText = FormatTimer(Presence, now);

    /// <summary>
    /// "12:34 left" when the game counts down to an end, "12:34 elapsed" from when its activity
    /// started, or else from when the player started the game.
    /// </summary>
    internal static string? FormatTimer(UserPresence? presence, DateTimeOffset now)
    {
        if (presence == null)
            return null;

        if (presence.Activity?.EndsOn is { } endsOn && endsOn > now)
            return $"{Format(endsOn - now)} left";

        var startedOn = presence.Activity?.StartsOn
            ?? new DateTimeOffset(DateTime.SpecifyKind(presence.StartedOn, DateTimeKind.Utc));

        var elapsed = now - startedOn;

        return elapsed < TimeSpan.Zero ? null : $"{Format(elapsed)} elapsed";

        static string Format(TimeSpan span) => span.TotalHours >= 1
            ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"
            : $"{span.Minutes}:{span.Seconds:00}";
    }
}
