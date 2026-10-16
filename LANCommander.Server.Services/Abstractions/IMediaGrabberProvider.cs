namespace LANCommander.Server.Services.Abstractions
{
    /// <summary>
    /// Supplies media grabbers from outside this assembly, such as those plugins add, for the
    /// composite media search to ask alongside the built-in ones.
    /// </summary>
    public interface IMediaGrabberProvider
    {
        IEnumerable<IMediaGrabberService> GetGrabbers();
    }
}
