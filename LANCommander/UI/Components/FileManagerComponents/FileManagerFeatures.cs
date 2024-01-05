namespace LANCommander.UI.Components.FileManagerComponents
{
    [Flags]
    public enum FileManagerFeatures
    {
        NavigationBack = 0,
        NavigationForward = 1,
        UpALevel = 2,
        Refresh = 4,
        Breadcrumbs = 8,
        NewFolder = 16,
        UploadFile = 32,
        Delete = 64,
    }
}
