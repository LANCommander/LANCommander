namespace LANCommander.Server.Services.Models;

/// <summary>
/// A point-in-time sample of a running game server's process (or container).
/// Any member may be null when the engine can't supply it.
/// </summary>
/// <param name="Pid">Operating system process id.</param>
/// <param name="StartTime">When the process started, in UTC.</param>
/// <param name="CpuPercent">CPU usage as a percentage of total machine capacity (0–100).</param>
/// <param name="MemoryBytes">Working set / memory usage in bytes.</param>
public sealed record ServerProcessInfo(int? Pid, DateTime? StartTime, double? CpuPercent, long? MemoryBytes);
