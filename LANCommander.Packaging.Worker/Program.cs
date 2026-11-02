using LANCommander.Interposer;
using LANCommander.Packaging.Worker;

// Deployed under Data\Libraries\Packaging\{arch}\, beside the launcher's copy of the Interposer in
// Data\Libraries\Interposer\{arch}\.
InterposerService.LibraryDirectory ??= Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "Interposer"));

return await WorkerHost.RunAsync(args);
