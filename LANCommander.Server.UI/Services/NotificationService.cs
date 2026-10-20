using Microsoft.AspNetCore.Components.Web;
using LANCommander.Server.UI.Controls;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Radzen;

namespace LANCommander.Server.UI.Services;

public enum NotificationLevel
{
    Success,
    Info,
    Warning,
    Error,
}

/// <summary>
/// A link under a notification's message, e.g. "Open media editor". Give it an <paramref name="Href"/>
/// to navigate, or an <paramref name="OnClick"/> to run; either way the notification closes.
/// </summary>
public sealed record NotificationAction(string Text, string? Href = null, Func<Task>? OnClick = null);

/// <summary>
/// Toast notifications in the corner of the screen: short confirmations ("Game saved") and errors
/// that don't warrant interrupting the user with a dialog.
/// </summary>
public sealed class NotificationService(Radzen.NotificationService notifications, ILogger<NotificationService> logger)
{
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan ErrorDuration = TimeSpan.FromSeconds(8);

    public void Success(string message, string? detail = null, NotificationAction? action = null, string? meta = null) =>
        Show(NotificationLevel.Success, message, detail, action, meta);

    public void Info(string message, string? detail = null, NotificationAction? action = null, string? meta = null) =>
        Show(NotificationLevel.Info, message, detail, action, meta);

    public void Warning(string message, string? detail = null, NotificationAction? action = null, string? meta = null) =>
        Show(NotificationLevel.Warning, message, detail, action, meta);

    public void Error(string message, string? detail = null, NotificationAction? action = null, string? meta = null) =>
        Show(NotificationLevel.Error, message, detail, action, meta);

    /// <summary>
    /// Logs <paramref name="exception"/> and tells the user <paramref name="message"/>, with the
    /// exception's own message as the detail.
    /// </summary>
    public void Error(Exception exception, string message)
    {
        logger.LogError(exception, "{Message}", message);

        Show(NotificationLevel.Error, message, exception.Message);
    }

    /// <summary>
    /// Shows a notification: <paramref name="message"/> as its title, <paramref name="detail"/> under
    /// it, then an optional <paramref name="action"/> link and a <paramref name="meta"/> line in mono
    /// (a job id, a time). One with an action stays twice as long, so there is time to use it.
    /// </summary>
    public void Show(NotificationLevel level, string message, string? detail = null, NotificationAction? action = null, string? meta = null)
    {
        var duration = level == NotificationLevel.Error ? ErrorDuration : DefaultDuration;

        if (action != null)
            duration *= 2;

        var notification = Create(level, message, detail, duration);

        if (action != null || meta != null)
        {
            // Clicking the body would close it before the action's own click lands
            notification.CloseOnClick = action == null;
            notification.DetailContent = _ => Detail(detail, action, meta, () => notifications.Messages.Remove(notification));
        }

        notifications.Notify(notification);
    }

    /// <summary>
    /// Shows a notification that stays until closed through the returned handle, for work in
    /// progress such as uploads. The handle can update the text as the work progresses.
    /// </summary>
    public NotificationHandle ShowPersistent(NotificationLevel level, string message, string? detail = null)
    {
        var handle = new NotificationHandle(notifications, level, message, detail);

        notifications.Notify(handle.Message);

        return handle;
    }

    /// <summary>
    /// Shows a notification that stays until dismissed, with a button that runs
    /// <paramref name="action"/> and closes it, e.g. "Continue Import" once an upload finishes.
    /// </summary>
    public NotificationHandle ShowAction(NotificationLevel level, string message, string? detail, string actionText, Func<Task> action)
    {
        var handle = new NotificationHandle(notifications, level, message, detail);

        handle.Message.DetailContent = _ => Detail(detail, new NotificationAction(actionText, OnClick: async () =>
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                Error(ex, $"{actionText} failed");
            }
        }), meta: null, handle.Close);

        notifications.Notify(handle.Message);

        return handle;
    }

    // The body under the title: the detail text, then the action as a 12/500 link, then the meta line
    private static RenderFragment Detail(string? detail, NotificationAction? action, string? meta, Action close) => builder =>
    {
        if (!String.IsNullOrEmpty(detail))
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "lc-notification-detail");
            builder.AddContent(2, detail);
            builder.CloseElement();
        }

        if (action != null)
        {
            if (action.Href != null)
            {
                builder.OpenElement(3, "a");
                builder.AddAttribute(4, "class", "lc-notification-action");
                builder.AddAttribute(5, "href", action.Href);
                builder.AddAttribute(6, "onclick", EventCallback.Factory.Create<MouseEventArgs>(action, close));
            }
            else
            {
                builder.OpenElement(3, "button");
                builder.AddAttribute(4, "class", "lc-notification-action");
                builder.AddAttribute(5, "type", "button");
                builder.AddAttribute(6, "onclick", EventCallback.Factory.Create<MouseEventArgs>(action, async () =>
                {
                    close();

                    if (action.OnClick != null)
                        await action.OnClick();
                }));
            }

            builder.AddEventStopPropagationAttribute(7, "onclick", true);
            builder.AddContent(8, action.Text);
            builder.CloseElement();
        }

        if (!String.IsNullOrEmpty(meta))
        {
            builder.OpenElement(9, "div");
            builder.AddAttribute(10, "class", "lc-notification-meta");
            builder.AddContent(11, meta);
            builder.CloseElement();
        }
    };

    internal static NotificationMessage Create(NotificationLevel level, string message, string? detail, TimeSpan? duration)
    {
        var notification = new NotificationMessage
        {
            Severity = level switch
            {
                NotificationLevel.Success => NotificationSeverity.Success,
                NotificationLevel.Warning => NotificationSeverity.Warning,
                NotificationLevel.Error => NotificationSeverity.Error,
                _ => NotificationSeverity.Info,
            },
            Summary = message,
            Detail = detail,
            CloseOnClick = true,
        };

        if (duration != null)
            notification.Duration = duration.Value.TotalMilliseconds;
        else
            // Radzen has no "never"; a day is long enough for any upload
            notification.Duration = TimeSpan.FromDays(1).TotalMilliseconds;

        notification.SummaryContent = _ => Summary(level, notification.Summary);

        return notification;
    }

    // Swap Radzen's Material Symbols glyph for the matching Phosphor icon (see Notification.scss)
    private static RenderFragment Summary(NotificationLevel level, string? text) => builder =>
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", "lc-notification-summary");
        builder.OpenComponent<Icon>(2);
        builder.AddComponentParameter(3, nameof(Icon.Type), level switch
        {
            NotificationLevel.Success => IconType.CheckCircle,
            NotificationLevel.Warning => IconType.Warning,
            NotificationLevel.Error => IconType.XCircle,
            _ => IconType.Info,
        });
        builder.AddComponentParameter(4, nameof(Icon.Fill), true);
        builder.AddComponentParameter(5, nameof(Icon.Class), "lc-notification-icon");
        builder.CloseComponent();
        builder.AddContent(6, text);
        builder.CloseElement();
    };
}

/// <summary>A notification shown with <see cref="NotificationService.ShowPersistent"/>.</summary>
public sealed class NotificationHandle
{
    private readonly Radzen.NotificationService _notifications;

    internal NotificationMessage Message { get; private set; }

    internal NotificationHandle(Radzen.NotificationService notifications, NotificationLevel level, string message, string? detail)
    {
        _notifications = notifications;
        Message = NotificationService.Create(level, message, detail, duration: null);
        Message.CloseOnClick = false;
    }

    public bool IsOpen => _notifications.Messages.Contains(Message);

    /// <summary>Replaces the notification's text, e.g. with new progress.</summary>
    public void Update(NotificationLevel level, string message, string? detail = null)
    {
        var index = _notifications.Messages.IndexOf(Message);

        var updated = NotificationService.Create(level, message, detail, duration: null);
        updated.CloseOnClick = false;

        if (index >= 0)
            _notifications.Messages[index] = updated;

        Message = updated;
    }

    public void Close() => _notifications.Messages.Remove(Message);
}
