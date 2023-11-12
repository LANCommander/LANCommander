using LANCommander.SDK.Enums;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LANCommander.SDK
{
    public class RedistributableManager
    {
        private readonly ILogger Logger;
        private Client Client { get; set; }

        public delegate void OnArchiveEntryExtractionProgressHandler(object sender, ArchiveEntryExtractionProgressArgs e);
        public event OnArchiveEntryExtractionProgressHandler OnArchiveEntryExtractionProgress;

        public delegate void OnArchiveExtractionProgressHandler(long position, long length);
        public event OnArchiveExtractionProgressHandler OnArchiveExtractionProgress;

        public RedistributableManager(Client client)
        {
            Client = client;
        }

        public RedistributableManager(Client client, ILogger logger)
        {
            Client = client;
            Logger = logger;
        }

        public void Install(Game game)
        {
            foreach (var redistributable in game.Redistributables)
            {
                Install(redistributable);
            }
        }

        public void Install(Redistributable redistributable)
        {
            string installScriptTempFile = null;
            string detectionScriptTempFile = null;
            string extractTempPath = null;

            try
            {
                var installScript = redistributable.Scripts.FirstOrDefault(s => s.Type == ScriptType.Install);
                installScriptTempFile = ScriptHelper.SaveTempScript(installScript);

                var detectionScript = redistributable.Scripts.FirstOrDefault(s => s.Type == ScriptType.DetectInstall);
                detectionScriptTempFile = ScriptHelper.SaveTempScript(detectionScript);

                var detectionResult = PowerShellRuntime.RunScript(detectionScriptTempFile, detectionScript.RequiresAdmin);

                // Redistributable is not installed
                if (detectionResult == 0)
                {
                    if (redistributable.Archives.Count() > 0)
                    {
                        var extractionResult = DownloadAndExtract(redistributable);

                        if (extractionResult.Success)
                        {
                            extractTempPath = extractionResult.Directory;

                            PowerShellRuntime.RunScript(installScriptTempFile, installScript.RequiresAdmin, null, extractTempPath);
                        }
                    }
                    else
                    {
                        PowerShellRuntime.RunScript(installScriptTempFile, installScript.RequiresAdmin, null, extractTempPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Redistributable {Redistributable} failed to install", redistributable.Name);
            }
            finally
            {
                if (File.Exists(installScriptTempFile))
                    File.Delete(installScriptTempFile);

                if (File.Exists(detectionScriptTempFile))
                    File.Delete(detectionScriptTempFile);

                if (Directory.Exists(extractTempPath))
                    Directory.Delete(extractTempPath);
            }
        }

        private ExtractionResult DownloadAndExtract(Redistributable redistributable)
        {
            if (redistributable == null)
            {
                Logger?.LogTrace("Redistributable failed to download! No redistributable was specified");
                throw new ArgumentNullException("No redistributable was specified");
            }

            var destination = Path.Combine(Path.GetTempPath(), redistributable.Name.SanitizeFilename());

            Logger?.LogTrace("Downloading and extracting {Redistributable} to path {Destination}", redistributable.Name, destination);

            try
            {
                Directory.CreateDirectory(destination);

                using (var redistributableStream = Client.StreamRedistributable(redistributable.Id))
                using (var reader = ReaderFactory.Open(redistributableStream))
                {
                    redistributableStream.OnProgress += (pos, len) =>
                    {
                        OnArchiveExtractionProgress?.Invoke(pos, len);
                    };

                    reader.EntryExtractionProgress += (object sender, ReaderExtractionEventArgs<IEntry> e) =>
                    {
                        OnArchiveEntryExtractionProgress?.Invoke(this, new ArchiveEntryExtractionProgressArgs
                        {
                            Entry = e.Item,
                            Progress = e.ReaderProgress,
                        });
                    };

                    reader.WriteAllToDirectory(destination, new ExtractionOptions()
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not extract to path {Destination}", destination);

                if (Directory.Exists(destination))
                {
                    Logger?.LogTrace("Cleaning up orphaned files after bad install");

                    Directory.Delete(destination, true);
                }

                throw new Exception("The redistributable archive could not be extracted, is it corrupted? Please try again");
            }

            var extractionResult = new ExtractionResult
            {
                Canceled = false
            };

            if (!extractionResult.Canceled)
            {
                extractionResult.Success = true;
                extractionResult.Directory = destination;
                Logger?.LogTrace("Redistributable {Redistributable} successfully downloaded and extracted to {Destination}", redistributable.Name, destination);
            }

            return extractionResult;
        }
    }
}
