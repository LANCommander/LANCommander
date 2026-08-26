using System;

namespace LANCommander.SDK.Models
{
    /// <summary>
    /// Sets or clears a game's explicit default archive. Passing a null <see cref="ArchiveId"/>
    /// clears the explicit default, so the effective default falls back to the newest archive by
    /// <c>CreatedOn</c>.
    /// </summary>
    public class SetDefaultArchiveRequest
    {
        public Guid? ArchiveId { get; set; }
    }
}
