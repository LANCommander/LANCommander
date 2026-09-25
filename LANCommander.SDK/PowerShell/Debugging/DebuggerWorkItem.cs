#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Management.Automation;
using SmaDebugger = System.Management.Automation.Debugger;

namespace LANCommander.SDK.PowerShell.Debugging;

/// <summary>
/// A unit of work executed by the pump inside the DebuggerStop handler, i.e. on the PS-Pipeline
/// thread while the debugger is stopped.
/// </summary>
/// <remarks>
/// This exists so that the pipeline thread is the ONLY thread that ever touches the engine's
/// <c>Debugger</c>. The UI posts work items and awaits their tasks; it never blocks, and the
/// pipeline never waits on the UI.
/// </remarks>
public abstract class DebuggerWorkItem
{
    internal abstract void Execute(DebugSession session, SmaDebugger debugger);

    /// <summary>Fault the waiting task. Called for items still queued when the debugger resumes.</summary>
    internal abstract void Cancel(Exception exception);
}

/// <summary>A work item that produces a result.</summary>
public abstract class DebuggerWorkItem<TResult> : DebuggerWorkItem
{
    // RunContinuationsAsynchronously is load-bearing, not a micro-optimisation. Without it,
    // SetResult on the pipeline thread runs the awaiting UI continuation inline on the pipeline
    // thread, mutating documents and observable collections off the UI thread.
    private readonly TaskCompletionSource<TResult> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<TResult> Task => _completion.Task;

    protected void SetResult(TResult result) => _completion.TrySetResult(result);

    protected void SetException(Exception exception) => _completion.TrySetException(exception);

    internal override void Cancel(Exception exception) => _completion.TrySetException(exception);
}

/// <summary>Resume the debugger. Servicing this breaks the pump loop.</summary>
public sealed class ResumeWorkItem : DebuggerWorkItem<bool>
{
    public ResumeWorkItem(DebuggerResumeAction action) => Action = action;

    public DebuggerResumeAction Action { get; }

    /// <summary>Called by the pump once the resume action has been applied.</summary>
    internal void Acknowledge() => SetResult(true);

    internal override void Execute(DebugSession session, SmaDebugger debugger)
    {
        // Never reached: the pump recognises this type and breaks out before dispatching.
    }
}

/// <summary>Result of evaluating a watch expression or console command.</summary>
public sealed record EvaluationResult(string Output, bool IsError, DebuggerResumeAction? ResumeAction);

/// <summary>Run an expression in the stopped debugger's context.</summary>
public sealed class ProcessCommandWorkItem : DebuggerWorkItem<EvaluationResult>
{
    private readonly string _command;
    private readonly bool _formatForDisplay;

    /// <param name="command">The expression or console command to run.</param>
    /// <param name="formatForDisplay">
    /// When true the command is piped through Out-String so that Format-Table and friends render
    /// as text. Suppressed for bare debugger commands (c, s, v, o, q, k), which the debugger
    /// interprets itself and which must reach it unwrapped.
    /// </param>
    public ProcessCommandWorkItem(string command, bool formatForDisplay)
    {
        _command = command;
        _formatForDisplay = formatForDisplay;
    }

    internal override void Execute(DebugSession session, SmaDebugger debugger)
    {
        var output = new PSDataCollection<PSObject>();

        try
        {
            var command = new PSCommand().AddScript(_command);
            if (_formatForDisplay)
            {
                command.AddCommand("Out-String")
                       .AddParameter("Stream", true)
                       .AddParameter("Width", 200);
            }

            DebuggerCommandResults results;

            // A breakpoint inside a watch expression would re-enter DebuggerStop on this very
            // thread, inside this very pump. The depth counter turns that nested stop into an
            // immediate Continue.
            session.EnterNestedEvaluation();
            try
            {
                results = debugger.ProcessCommand(command, output);
            }
            finally
            {
                session.ExitNestedEvaluation();
            }

            var text = string.Join(Environment.NewLine,
                output.Where(static o => o is not null).Select(static o => o.ToString()));

            // "c", "s", "o" and friends are interpreted by the debugger itself, which reports the
            // resume here rather than through our ResumeWorkItem. Without this, typing "c" in the
            // console appears to do nothing at all.
            if (results?.ResumeAction is { } action)
                session.RequestResumeFromPump(action);

            SetResult(new EvaluationResult(text, IsError: false, results?.ResumeAction));
        }
        catch (Exception ex)
        {
            SetResult(new EvaluationResult(ex.GetBaseException().Message, IsError: true, ResumeAction: null));
        }
    }
}

/// <summary>Fetch the children of a variable node.</summary>
public sealed class InspectWorkItem : DebuggerWorkItem<IReadOnlyList<VariableInfo>>
{
    private readonly int _handle;

    public InspectWorkItem(int handle) => _handle = handle;

    internal override void Execute(DebugSession session, SmaDebugger debugger)
    {
        try
        {
            // Runs here rather than on the UI thread because a ScriptProperty getter is user code.
            session.EnterNestedEvaluation();
            try
            {
                SetResult(session.Expander.Expand(_handle));
            }
            finally
            {
                session.ExitNestedEvaluation();
            }
        }
        catch (Exception ex)
        {
            SetException(ex);
        }
    }
}

/// <summary>Add or remove a line breakpoint while the debugger is stopped.</summary>
public sealed class BreakpointChangeWorkItem : DebuggerWorkItem<bool>
{
    private readonly BreakpointRequest _request;
    private readonly bool _add;

    public BreakpointChangeWorkItem(BreakpointRequest request, bool add)
    {
        _request = request;
        _add = add;
    }

    internal override void Execute(DebugSession session, SmaDebugger debugger)
    {
        try
        {
            session.ApplySingleBreakpointChange(debugger, _request, _add);
            SetResult(true);
        }
        catch (Exception ex)
        {
            SetException(ex);
        }
    }
}

/// <summary>Fetch the variables visible in one call-stack frame.</summary>
/// <remarks>
/// Goes through <c>Get-Variable</c> rather than <c>CallStackFrame.GetFrameVariables()</c>, which
/// returns only the frame's automatic variables and none of the user's locals. The scope number
/// is the frame's depth from the top of the stack.
/// </remarks>
public sealed class FrameVariablesWorkItem : DebuggerWorkItem<IReadOnlyList<VariableInfo>>
{
    private readonly int _scope;

    public FrameVariablesWorkItem(int scope) => _scope = scope;

    internal override void Execute(DebugSession session, SmaDebugger debugger)
    {
        try
        {
            var output = new PSDataCollection<PSObject>();

            session.EnterNestedEvaluation();
            try
            {
                var command = new PSCommand()
                    .AddCommand("Get-Variable")
                    .AddParameter("Scope", _scope);

                debugger.ProcessCommand(command, output);
            }
            catch (Exception)
            {
                // A scope number past the end of the chain throws; fall back to the innermost one
                // rather than showing the user an empty panel.
                output = new PSDataCollection<PSObject>();
                debugger.ProcessCommand(
                    new PSCommand().AddCommand("Get-Variable").AddParameter("Scope", 0), output);
            }
            finally
            {
                session.ExitNestedEvaluation();
            }

            var variables = new List<VariableInfo>();

            foreach (var item in output)
            {
                if (item?.BaseObject is not PSVariable variable)
                    continue;

                if (VariableExpander.IsHidden(variable.Name))
                    continue;

                try
                {
                    variables.Add(session.Expander.Describe(variable.Name, variable.Value));
                }
                catch (Exception ex)
                {
                    variables.Add(new VariableInfo
                    {
                        Name = variable.Name,
                        TypeName = string.Empty,
                        Value = "<error: " + ex.GetBaseException().Message + ">",
                        HasChildren = false,
                    });
                }
            }

            variables.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            SetResult(variables);
        }
        catch (Exception ex)
        {
            SetException(ex);
        }
    }
}

/// <summary>Enumerate the stopped session's commands, including modules the script imported.</summary>
/// <remarks>
/// The list the command pane shows while idle comes from <see cref="Commands.CommandCatalog"/> and
/// therefore knows nothing about what the running script has imported. This is how that gap is
/// closed: the same query, run against the live session.
/// </remarks>
public sealed class CommandListWorkItem : DebuggerWorkItem<IReadOnlyList<Commands.CommandInfoSnapshot>>
{
    internal override void Execute(DebugSession session, SmaDebugger debugger)
    {
        try
        {
            session.EnterNestedEvaluation();
            try
            {
                SetResult(Commands.CommandQuery.List((command, output) => debugger.ProcessCommand(command, output)));
            }
            finally
            {
                session.ExitNestedEvaluation();
            }
        }
        catch (Exception ex)
        {
            SetException(ex);
        }
    }
}

/// <summary>Describe one command in the stopped session's context.</summary>
public sealed class CommandDetailWorkItem : DebuggerWorkItem<Commands.CommandDetail?>
{
    private readonly string _name;

    public CommandDetailWorkItem(string name) => _name = name;

    internal override void Execute(DebugSession session, SmaDebugger debugger)
    {
        try
        {
            session.EnterNestedEvaluation();
            try
            {
                SetResult(Commands.CommandQuery.Describe((command, output) => debugger.ProcessCommand(command, output), _name));
            }
            finally
            {
                session.ExitNestedEvaluation();
            }
        }
        catch (Exception ex)
        {
            SetException(ex);
        }
    }
}

/// <summary>
/// Drop every breakpoint and step mode, then continue. Serviced when the debugger UI goes away while the
/// script is stopped, so the script runs to completion unobserved instead of being killed.
/// </summary>
public sealed class DetachWorkItem : DebuggerWorkItem<bool>
{
    internal override void Execute(DebugSession session, SmaDebugger debugger)
    {
        try
        {
            session.ClearDebuggingState(debugger);
            session.RequestResumeFromPump(DebuggerResumeAction.Continue);
            SetResult(true);
        }
        catch (Exception ex)
        {
            SetException(ex);
        }
    }
}
