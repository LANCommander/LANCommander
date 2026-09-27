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
/// Toast notifications in the corner of the screen: short confirmations ("Game saved") and errors
/// that don't warrant interrupting the user with a dialog.
/// </summary>
public sealed class NotificationService(Radzen.NotificationService notifications, ILogger<NotificationService> logger)
{
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan ErrorDuration = TimeSpan.FromSeconds(8);

    public void Success(string message, string? detail = null) => Show(NotificationLevel.Success, message, detail);

    public void Info(string message, string? detail = null) => Show(NotificationLevel.Info, message, detail);

    public void Warning(string message, string? detail = null) => Show(NotificationLevel.Warning, message, detail);

    public void Error(string message, string? detail = null) => Show(NotificationLevel.Error, message, detail);

    /// <summary>
    /// Logs <paramref name="exception"/> and tells the user <paramref name="message"/>, with the
    /// exception's own message as the detail.
    /// </summary>
    public void Error(Exception exception, string message)
    {
        logger.LogError(exception, "{Message}", message);

        Show(NotificationLevel.Error, message, exception.Message);
    }

    public void Show(NotificationLevel level, string message, string? detail = null) =>
        notifications.Notify(Create(level, message, detail, level == NotificationLevel.Error ? ErrorDuration : DefaultDuration));

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

        handle.Message.DetailContent = _ => builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "lc-notification-detail");
            builder.AddContent(2, detail);
            builder.CloseElement();

            builder.OpenComponent<Button>(3);
            builder.AddComponentParameter(4, nameof(Button.Primary), true);
            builder.AddComponentParameter(5, nameof(Button.Small), true);
            builder.AddComponentParameter(6, nameof(Button.Class), "lc-notification-action");
            builder.AddComponentParameter(7, nameof(Button.OnClick), EventCallback.Factory.Create<MouseEventArgs>(handle, async () =>
            {
                handle.Close();

                try
                {
                    await action();
                }
                catch (Exception ex)
                {
                    Error(ex, $"{actionText} failed");
                }
            }));
            builder.AddComponentParameter(8, nameof(Button.ChildContent), (RenderFragment)(b => b.AddContent(0, actionText)));
            builder.CloseComponent();
        };

        notifications.Notify(handle.Message);

        return handle;
    }

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
