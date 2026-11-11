using LANCommander.Server.Models;
using LANCommander.Server.Services;
using LANCommander.Server.UI.Components;
using Microsoft.Extensions.Logging;
using GlobalVariable = LANCommander.SDK.Models.GlobalVariable;

namespace LANCommander.Server.UI.Controls;

/// <summary>
/// What a PowerShell editor offers to insert and complete: the snippets, the public functions of the
/// server's modules, and the global variables. Shared by the script editor dialog and the snippet and
/// module editors.
/// </summary>
public sealed class ScriptEditorReferences
{
    public IReadOnlyList<Snippet> Snippets { get; private init; } = [];
    public IReadOnlyList<ModuleFunctionCompletion> ModuleFunctions { get; private init; } = [];
    public IReadOnlyList<GlobalVariable> GlobalVariables { get; private init; } = [];
    public IReadOnlyList<GlobalVariableCompletion> GlobalVariableCompletions { get; private init; } = [];

    public static ScriptEditorReferences Empty { get; } = new();

    /// <summary>Loads each kind separately, so one that fails leaves the others usable.</summary>
    public static ScriptEditorReferences Load(ScriptService scriptService, ModuleService moduleService, ILogger logger)
    {
        IReadOnlyList<Snippet> snippets = [];
        IReadOnlyList<ModuleFunctionCompletion> moduleFunctions = [];
        IReadOnlyList<GlobalVariable> globalVariables = [];

        try
        {
            snippets = scriptService.GetSnippets().ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load the script snippets");
        }

        try
        {
            moduleFunctions = moduleService.GetPublicFunctionCompletions()
                .Select(f => new ModuleFunctionCompletion(f.Name, String.IsNullOrWhiteSpace(f.Synopsis) ? null : f.Synopsis, f.Module))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load the module functions");
        }

        try
        {
            globalVariables = scriptService.GetGlobalVariables().ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load the global script variables");
        }

        return new ScriptEditorReferences
        {
            Snippets = snippets,
            ModuleFunctions = moduleFunctions,
            GlobalVariables = globalVariables,
            GlobalVariableCompletions = globalVariables
                .Select(v => new GlobalVariableCompletion(v.Name, String.IsNullOrWhiteSpace(v.Description) ? null : v.Description))
                .ToList(),
        };
    }
}
