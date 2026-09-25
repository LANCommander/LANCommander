#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Security;
using System.Threading;

namespace LANCommander.SDK.PowerShell.Debugging.Hosting;

/// <summary>
/// Routes everything the engine writes through the host into <see cref="IConsoleSink"/>, and blocks the
/// pipeline thread for Read-Host while the UI stays live.
/// </summary>
/// <remarks>
/// Cmdlet-generated Error/Warning/Verbose/Debug records land in the PowerShell streams, not here. These
/// methods exist for code paths that call the host directly, e.g. <c>$Host.UI.WriteWarningLine(...)</c>
/// or the SDK's <c>PowerShellHostLogger</c>. Write-Host is the opposite case: it only comes through here.
/// </remarks>
public sealed class DebugPSHostUserInterface : PSHostUserInterface
{
    private readonly IConsoleSink _sink;
    private readonly DebugPSHostRawUserInterface _rawUi = new();

    public DebugPSHostUserInterface(IConsoleSink sink) => _sink = sink;

    public override PSHostRawUserInterface RawUI => _rawUi;

    /// <summary>False so the engine formats with plain text rather than ANSI escapes.</summary>
    public override bool SupportsVirtualTerminal => false;

    /// <summary>
    /// Set by <see cref="DebugSession"/> at the start of each run. Cancelling it unblocks a pending
    /// Read-Host, which is what makes Stop work while a script is waiting for input.
    /// </summary>
    public CancellationToken InputCancellation { get; set; } = CancellationToken.None;

    public override void Write(string? value) =>
        _sink.Write(ConsoleOutputKind.Host, value ?? string.Empty);

    public override void Write(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string? value) =>
        _sink.Write(ConsoleOutputKind.Host, value ?? string.Empty, foregroundColor, backgroundColor);

    public override void WriteLine() =>
        _sink.WriteLine(ConsoleOutputKind.Host, string.Empty);

    public override void WriteLine(string? value) =>
        _sink.WriteLine(ConsoleOutputKind.Host, value ?? string.Empty);

    public override void WriteLine(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string? value) =>
        _sink.WriteLine(ConsoleOutputKind.Host, value ?? string.Empty, foregroundColor, backgroundColor);

    public override void WriteErrorLine(string? value) =>
        _sink.WriteLine(ConsoleOutputKind.Error, value ?? string.Empty);

    public override void WriteWarningLine(string? value) =>
        _sink.WriteLine(ConsoleOutputKind.Warning, value ?? string.Empty);

    public override void WriteVerboseLine(string? value) =>
        _sink.WriteLine(ConsoleOutputKind.Verbose, value ?? string.Empty);

    public override void WriteDebugLine(string? value) =>
        _sink.WriteLine(ConsoleOutputKind.Debug, value ?? string.Empty);

    public override void WriteInformation(InformationRecord record)
    {
        // Write-Host already arrived through Write/WriteLine above; the engine additionally publishes it
        // here tagged PSHOST. Rendering it again would double every line.
        if (record.Tags?.Contains("PSHOST") == true)
            return;

        _sink.WriteLine(ConsoleOutputKind.Information, record.MessageData?.ToString() ?? string.Empty);
    }

    public override void WriteProgress(long sourceId, ProgressRecord record) =>
        _sink.ReportProgress(sourceId, ScriptProgress.From(record));

    /// <summary>
    /// Called on the pipeline thread and must block; the engine expects a synchronous string. This is the
    /// mirror image of the debugger pump: the pipeline blocks, the UI stays live.
    /// </summary>
    public override string ReadLine() => ReadBlocking(prompt: null, secure: false) ?? string.Empty;

    public override SecureString ReadLineAsSecureString()
    {
        var text = ReadBlocking(prompt: null, secure: true);

        var secure = new SecureString();

        foreach (var c in text ?? string.Empty)
            secure.AppendChar(c);

        secure.MakeReadOnly();

        return secure;
    }

    public override Dictionary<string, PSObject> Prompt(string caption, string message, Collection<FieldDescription> descriptions)
    {
        var results = new Dictionary<string, PSObject>(StringComparer.OrdinalIgnoreCase);

        if (descriptions is null)
            return results;

        if (!string.IsNullOrEmpty(caption))
            _sink.WriteLine(ConsoleOutputKind.Prompt, caption);

        if (!string.IsNullOrEmpty(message))
            _sink.WriteLine(ConsoleOutputKind.Prompt, message);

        foreach (var description in descriptions)
        {
            var label = string.IsNullOrEmpty(description.Label) ? description.Name : description.Label;
            var answer = ReadBlocking(label, secure: false);

            results[description.Name] = PSObject.AsPSObject(answer ?? string.Empty);
        }

        return results;
    }

    /// <summary>
    /// Auto-selects the default and says so. Throwing here would kill any script using -Confirm, and
    /// choosing silently would be worse.
    /// </summary>
    public override int PromptForChoice(string caption, string message, Collection<ChoiceDescription> choices, int defaultChoice)
    {
        if (!string.IsNullOrEmpty(caption))
            _sink.WriteLine(ConsoleOutputKind.Prompt, caption);

        if (!string.IsNullOrEmpty(message))
            _sink.WriteLine(ConsoleOutputKind.Prompt, message);

        var index = defaultChoice >= 0 && choices is not null && defaultChoice < choices.Count ? defaultChoice : 0;
        var label = choices is not null && index < choices.Count ? choices[index].Label : "<default>";

        _sink.WriteLine(ConsoleOutputKind.System, $"[host] Interactive choice is not supported; auto-selected the default: {label}");

        return index;
    }

    public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName) =>
        throw new PSNotImplementedException("Credential prompts are not supported by the script debugger. Build the PSCredential in the script instead.");

    public override PSCredential PromptForCredential(
        string caption, string message, string userName, string targetName,
        PSCredentialTypes allowedCredentialTypes, PSCredentialUIOptions options) =>
        throw new PSNotImplementedException("Credential prompts are not supported by the script debugger. Build the PSCredential in the script instead.");

    private string? ReadBlocking(string? prompt, bool secure)
    {
        try
        {
            return _sink.ReadLineAsync(prompt, secure, InputCancellation).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // The engine understands this as "the pipeline was stopped" and unwinds cleanly.
            throw new PipelineStoppedException();
        }
    }
}
