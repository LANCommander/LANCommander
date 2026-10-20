namespace LANCommander.SDK.Models
{
    /// <summary>
    /// Body of <c>POST /api/Scripts/{id}/Contents</c>. Deliberately narrow: only the script text can be
    /// changed this way, never its type, name, admin flag or what it belongs to.
    /// </summary>
    public class UpdateScriptContentsRequest
    {
        public string Contents { get; set; }

        /// <summary>
        /// <see cref="Helpers.ScriptHelper.HashContents"/> of the contents the edit started from. When set,
        /// the server refuses the update with 409 Conflict if the script has changed since.
        /// </summary>
        public string BaseContentsHash { get; set; }
    }
}
