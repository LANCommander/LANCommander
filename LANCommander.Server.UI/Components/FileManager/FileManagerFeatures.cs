namespace LANCommander.Server.UI.Components
{
    [Flags]
    public enum FileManagerFeatures
    {
        None = 0,
        NavigationBack = 1,
        NavigationForward = 2,
        UpALevel = 4,
        Refresh = 8,
        Breadcrumbs = 16,
        NewFolder = 32,
        UploadFile = 64,
        Delete = 128,
        ColumnPicker = 256,
        /// <summary>A field in the nav row that narrows the current folder's entries by name.</summary>
        Filter = 512,
    }
}
