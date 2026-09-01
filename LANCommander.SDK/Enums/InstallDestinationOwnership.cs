namespace LANCommander.SDK.Enums
{
    /// <summary>
    /// Declares who owns the destination directory of a download/extract, and therefore what may
    /// be deleted if that download/extract is canceled or fails. Extraction failure cleanup is
    /// destructive and recursive, so this intent is threaded explicitly from whoever resolved the
    /// destination rather than inferred at extraction time — a destination can legitimately be
    /// pre-created (empty) before extraction starts, so an on-disk probe alone cannot tell a fresh
    /// install apart from an in-place update of an existing installation.
    /// </summary>
    public enum InstallDestinationOwnership
    {
        /// <summary>
        /// The destination is an existing installation directory, or a directory deliberately
        /// shared with one (an overlay add-on/mod extracting into its base game's folder, an
        /// in-place version change, a legacy pre-migration install being updated in its existing
        /// folder). Nothing in it belongs to this extraction, so it must never be recursively
        /// deleted on cancel/failure — doing so destroys the user's whole installation.
        ///
        /// This is the deliberate default: forgetting to declare ownership leaves partial files
        /// behind, which is recoverable, instead of deleting an installation, which is not.
        /// </summary>
        ExistingInstallation = 0,

        /// <summary>
        /// This extraction is the only thing populating the destination — a brand-new install into
        /// a directory that does not exist yet (or was pre-created empty for it). Only in this case
        /// may cleanup remove the destination directory, because everything in it was put there by
        /// this extraction.
        /// </summary>
        Fresh = 1,
    }
}
