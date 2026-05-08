import { cmdlets, CmdletDefinition } from "./powershell-completions.generated";

declare const monaco: any;

interface VariableDefinition {
    name: string;
    description: string;
}

const variables: VariableDefinition[] = [
    { name: "$InstallDirectory", description: "Root install directory for the game" },
    { name: "$WorkingDirectory", description: "Current working directory for the script" },
    { name: "$ServerAddress", description: "Address of the LANCommander server" },
    { name: "$DefaultInstallDirectory", description: "Default installation directory" },
    { name: "$GameManifest", description: "The game's manifest object" },
    { name: "$PlayerAlias", description: "Current player's alias/display name" },
    { name: "$NewPlayerAlias", description: "New player alias (name change scripts)" },
    { name: "$OldPlayerAlias", description: "Previous player alias (name change scripts)" },
    { name: "$ScriptType", description: "Type of the script being executed (Install, Uninstall, BeforeStart, AfterStop, etc.)" },
    { name: "$Server", description: "Server object (server scripts)" },
    { name: "$Game", description: "Game object (server scripts)" },
    { name: "$User", description: "User object (server scripts)" },
    { name: "$ToolManifest", description: "Tool manifest object (tool scripts)" },
    { name: "$Tool", description: "Tool object (package scripts)" },
    { name: "$RedistributableManifest", description: "Redistributable manifest object (redistributable scripts)" },
    { name: "$Redistributable", description: "Redistributable object (package scripts)" },
    { name: "$ServerId", description: "Server GUID identifier (server process variables)" },
    { name: "$ServerName", description: "Server display name (server process variables)" },
    { name: "$ServerHost", description: "Server hostname (server process variables)" },
    { name: "$ServerPort", description: "Server port number (server process variables)" },
    { name: "$GameTitle", description: "Game title (server process variables)" },
    { name: "$GameId", description: "Game GUID identifier (server process variables)" },
];

let registered = false;

export function registerPowerShellCompletions(): void {
    if (registered || typeof monaco === "undefined") return;
    registered = true;

    registerCompletionProvider();
    registerHoverProvider();
}

function registerCompletionProvider(): void {
    monaco.languages.registerCompletionItemProvider("powershell", {
        triggerCharacters: ["-", "$"],
        provideCompletionItems(model: any, position: any) {
            const word = model.getWordUntilPosition(position);
            const range = {
                startLineNumber: position.lineNumber,
                endLineNumber: position.lineNumber,
                startColumn: word.startColumn,
                endColumn: word.endColumn,
            };

            const lineContent: string = model.getLineContent(position.lineNumber);
            const textBefore = lineContent.substring(0, position.column - 1);

            const suggestions: any[] = [];

            // Variable completions: triggered by $
            if (/\$\w*$/.test(textBefore)) {
                const varRange = {
                    ...range,
                    startColumn: textBefore.lastIndexOf("$") + 1,
                };

                for (const v of variables) {
                    suggestions.push({
                        label: v.name,
                        kind: monaco.languages.CompletionItemKind.Variable,
                        insertText: v.name.substring(1),
                        detail: "Variable",
                        documentation: v.description,
                        range: varRange,
                    });
                }
            }

            // Parameter completions: triggered by - after a cmdlet name
            const paramMatch = textBefore.match(/-(\w*)$/);
            if (paramMatch) {
                const cmdlet = findCmdletOnLine(textBefore);
                if (cmdlet) {
                    const dashCol = textBefore.lastIndexOf("-") + 1;
                    const paramRange = {
                        ...range,
                        startColumn: dashCol,
                    };

                    for (const p of cmdlet.parameters) {
                        const detail = `[${p.type}]${p.mandatory ? " (Required)" : ""}`;
                        suggestions.push({
                            label: `-${p.name}`,
                            kind: monaco.languages.CompletionItemKind.Property,
                            insertText: `-${p.name} `,
                            detail,
                            documentation: p.helpMessage || undefined,
                            range: paramRange,
                            sortText: p.mandatory ? `0_${p.name}` : `1_${p.name}`,
                        });

                        for (const alias of p.aliases) {
                            suggestions.push({
                                label: `-${alias}`,
                                kind: monaco.languages.CompletionItemKind.Property,
                                insertText: `-${alias} `,
                                detail: `${detail} (alias for -${p.name})`,
                                documentation: p.helpMessage || undefined,
                                range: paramRange,
                                sortText: `2_${alias}`,
                            });
                        }
                    }
                }
            }

            // Cmdlet name completions
            if (!paramMatch) {
                for (const c of cmdlets) {
                    suggestions.push({
                        label: c.name,
                        kind: monaco.languages.CompletionItemKind.Function,
                        insertText: c.name,
                        detail: c.outputType ? `-> ${c.outputType}` : "Cmdlet",
                        documentation: buildCmdletDocumentation(c),
                        range,
                    });
                }
            }

            return { suggestions };
        },
    });
}

function registerHoverProvider(): void {
    monaco.languages.registerHoverProvider("powershell", {
        provideHover(model: any, position: any) {
            const word = model.getWordAtPosition(position);
            if (!word) return null;

            const lineContent: string = model.getLineContent(position.lineNumber);

            // Check for cmdlet hover (Verb-Noun pattern)
            const cmdletPattern = /[\w]+-[\w]+/g;
            let match;
            while ((match = cmdletPattern.exec(lineContent)) !== null) {
                const start = match.index + 1;
                const end = start + match[0].length;
                if (position.column >= start && position.column <= end) {
                    const cmdlet = cmdlets.find((c) => c.name === match![0]);
                    if (cmdlet) {
                        return {
                            range: {
                                startLineNumber: position.lineNumber,
                                endLineNumber: position.lineNumber,
                                startColumn: start,
                                endColumn: end,
                            },
                            contents: [
                                { value: `**${cmdlet.name}**` },
                                { value: buildCmdletHoverMarkdown(cmdlet) },
                            ],
                        };
                    }
                }
            }

            // Check for variable hover
            const varPattern = /\$[\w]+/g;
            while ((match = varPattern.exec(lineContent)) !== null) {
                const start = match.index + 1;
                const end = start + match[0].length;
                if (position.column >= start && position.column <= end) {
                    const variable = variables.find((v) => v.name === match![0]);
                    if (variable) {
                        return {
                            range: {
                                startLineNumber: position.lineNumber,
                                endLineNumber: position.lineNumber,
                                startColumn: start,
                                endColumn: end,
                            },
                            contents: [
                                { value: `**${variable.name}**` },
                                { value: variable.description },
                            ],
                        };
                    }
                }
            }

            // Check for parameter hover (-ParameterName)
            const paramPattern = /-(\w+)/g;
            while ((match = paramPattern.exec(lineContent)) !== null) {
                const start = match.index + 1;
                const end = start + match[0].length;
                if (position.column >= start && position.column <= end) {
                    const paramName = match[1];
                    const cmdlet = findCmdletOnLine(
                        lineContent.substring(0, match.index)
                    );
                    if (cmdlet) {
                        const param = cmdlet.parameters.find(
                            (p) =>
                                p.name === paramName ||
                                p.aliases.includes(paramName)
                        );
                        if (param) {
                            const parts = [
                                { value: `**-${param.name}** [${param.type}]${param.mandatory ? " *(Required)*" : ""}` },
                            ];
                            if (param.helpMessage) {
                                parts.push({ value: param.helpMessage });
                            }
                            if (param.aliases.length > 0) {
                                parts.push({
                                    value: `Aliases: ${param.aliases.map((a) => `-${a}`).join(", ")}`,
                                });
                            }
                            return {
                                range: {
                                    startLineNumber: position.lineNumber,
                                    endLineNumber: position.lineNumber,
                                    startColumn: start,
                                    endColumn: end,
                                },
                                contents: parts,
                            };
                        }
                    }
                }
            }

            return null;
        },
    });
}

function findCmdletOnLine(text: string): CmdletDefinition | null {
    const matches = text.match(/(\w+-\w+)/g);
    if (!matches) return null;

    // Return the last cmdlet found on the line (closest to cursor)
    for (let i = matches.length - 1; i >= 0; i--) {
        const cmdlet = cmdlets.find((c) => c.name === matches[i]);
        if (cmdlet) return cmdlet;
    }

    return null;
}

function buildCmdletDocumentation(c: CmdletDefinition): string {
    const parts: string[] = [];
    if (c.description) parts.push(c.description);

    const requiredParams = c.parameters.filter((p) => p.mandatory);
    if (requiredParams.length > 0) {
        parts.push(
            "Required: " +
                requiredParams.map((p) => `-${p.name}`).join(", ")
        );
    }

    return parts.join("\n\n");
}

function buildCmdletHoverMarkdown(c: CmdletDefinition): string {
    const lines: string[] = [];

    if (c.description) lines.push(c.description);
    if (c.outputType) lines.push(`**Output:** ${c.outputType}`);

    if (c.parameters.length > 0) {
        lines.push("");
        lines.push("**Parameters:**");
        for (const p of c.parameters) {
            const req = p.mandatory ? " *(Required)*" : "";
            lines.push(`- \`-${p.name}\` [${p.type}]${req}`);
        }
    }

    return lines.join("\n");
}
