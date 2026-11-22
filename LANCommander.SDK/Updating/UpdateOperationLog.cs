#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LANCommander.SDK.Updating;

public enum UpdateOperationKind
{
    /// <summary>An installed file is moved aside, to its path plus <see cref="UpdateJournal.BackupSuffix"/>.</summary>
    Backup,

    /// <summary>A staged file is moved into the install directory.</summary>
    Install,
}

public readonly record struct UpdateOperation(UpdateOperationKind Kind, string RelativePath);

/// <summary>
/// An append-only list of the file moves an update makes. Each line is written before its move, so
/// after a crash the log names every file that may have moved; undoing a move that never happened is
/// harmless.
/// </summary>
public sealed class UpdateOperationLog : IDisposable
{
    private readonly StreamWriter _writer;

    private UpdateOperationLog(string path)
    {
        var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);

        _writer = new StreamWriter(stream, new UTF8Encoding(false));
    }

    public static UpdateOperationLog Open(string path) => new(path);

    public void Append(UpdateOperationKind kind, string relativePath)
    {
        _writer.Write(kind);
        _writer.Write('\t');
        _writer.Write(relativePath);
        _writer.Write('\n');
        _writer.Flush();
    }

    public static IReadOnlyList<UpdateOperation> Read(string path)
    {
        var operations = new List<UpdateOperation>();

        if (!File.Exists(path))
            return operations;

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (reader.ReadLine() is { } line)
        {
            var separator = line.IndexOf('\t');

            // A crash can leave the last line half written
            if (separator <= 0 || separator == line.Length - 1)
                continue;

            if (Enum.TryParse<UpdateOperationKind>(line[..separator], out var kind))
                operations.Add(new UpdateOperation(kind, line[(separator + 1)..]));
        }

        return operations;
    }

    public void Dispose() => _writer.Dispose();
}
