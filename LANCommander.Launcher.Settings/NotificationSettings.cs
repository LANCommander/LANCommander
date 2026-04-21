namespace LANCommander.Launcher.Settings
{
    public class NotificationSettings
    {
        public bool NotifyOnInstallComplete { get; set; } = true;
        public bool NotifyOnInstallFailed { get; set; } = true;
        public bool NotifyOnChatMessage { get; set; } = true;
        public NotificationSoundTheme SoundTheme { get; set; } = NotificationSoundTheme.SystemDefault;
    }

    public enum NotificationSoundTheme
    {
        SystemDefault,
        Silent
    }
}
