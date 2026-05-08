import { cmdlets, CmdletDefinition, variableTypes, scriptTypeValues } from "./PowerShellCompletions.g";

declare const monaco: any;

// Build a lookup from type name to its properties
const typeMap = new Map(variableTypes.map((t) => [t.name, t]));

interface VariableDefinition {
    name: string;
    type: string;
    description: string;
}

const variables: VariableDefinition[] = [
    { name: "$InstallDirectory", type: "string", description: "Root install directory for the game" },
    { name: "$WorkingDirectory", type: "string", description: "Current working directory for the script" },
    { name: "$ServerAddress", type: "string", description: "Address of the LANCommander server" },
    { name: "$DefaultInstallDirectory", type: "string", description: "Default installation directory" },
    { name: "$GameManifest", type: "GameManifest", description: "The game's manifest object containing metadata such as title, ID, sort title, description, notes, and related collections" },
    { name: "$PlayerAlias", type: "string", description: "Current player's alias/display name" },
    { name: "$NewPlayerAlias", type: "string", description: "New player alias (name change scripts)" },
    { name: "$OldPlayerAlias", type: "string", description: "Previous player alias (name change scripts)" },
    { name: "$AllocatedKey", type: "string", description: "The product/serial key allocated to the current player for this game (key allocation scripts)" },
    { name: "$ScriptType", type: "ScriptType", description: "Type of the script being executed (Install, Uninstall, BeforeStart, AfterStop, NameChange, KeyChange, etc.)" },
    { name: "$Server", type: "Server", description: "Server model object with properties: Id, Name, Host, Port, Game, and WorkingDirectory (server scripts)" },
    { name: "$Game", type: "Game", description: "Game model object with properties: Id, Title, SortTitle, Description, and Notes (server scripts)" },
    { name: "$User", type: "User", description: "User model object with properties: Id, UserName, and Alias (server scripts)" },
    { name: "$ToolManifest", type: "ToolManifest", description: "Tool manifest object containing metadata for the current tool" },
    { name: "$Tool", type: "Tool", description: "Tool model object with properties: Id, Name, and Description (package scripts)" },
    { name: "$RedistributableManifest", type: "RedistributableManifest", description: "Redistributable manifest object containing metadata for the current redistributable" },
    { name: "$Redistributable", type: "Redistributable", description: "Redistributable model object with properties: Id, Name, and Description (package scripts)" },
    { name: "$DisplayWidth", type: "string", description: "Primary display width in pixels" },
    { name: "$DisplayHeight", type: "string", description: "Primary display height in pixels" },
    { name: "$DisplayRefreshRate", type: "string", description: "Primary display refresh rate in Hz" },
    { name: "$DisplayBitDepth", type: "string", description: "Primary display bit depth (bits per pixel)" },
    { name: "$IPXRelayHost", type: "string", description: "IPX relay server hostname (when IPX relay is configured)" },
    { name: "$IPXRelayPort", type: "string", description: "IPX relay server port (when IPX relay is configured)" },
    { name: "$ServerId", type: "string", description: "Server GUID identifier (server process variables)" },
    { name: "$ServerName", type: "string", description: "Server display name (server process variables)" },
    { name: "$ServerHost", type: "string", description: "Server hostname (server process variables)" },
    { name: "$ServerPort", type: "string", description: "Server port number (server process variables)" },
    { name: "$GameTitle", type: "string", description: "Game title (server process variables)" },
    { name: "$GameId", type: "string", description: "Game GUID identifier (server process variables)" },
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
        triggerCharacters: ["-", "$", "."],
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

            // Member access completions: triggered by . after a variable
            const memberMatch = textBefore.match(/\$(\w+)\.(\w*)$/);
            if (memberMatch) {
                const varName = "$" + memberMatch[1];
                const variable = variables.find((v) => v.name === varName);
                if (variable) {
                    const typeDef = typeMap.get(variable.type);
                    if (typeDef) {
                        for (const prop of typeDef.properties) {
                            suggestions.push({
                                label: prop.name,
                                kind: monaco.languages.CompletionItemKind.Field,
                                insertText: prop.name,
                                detail: `[${prop.type}]`,
                                documentation: `Property of ${variable.type}`,
                                range,
                            });
                        }
                        return { suggestions };
                    }
                }
            }

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
                        detail: `[${v.type}]`,
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
            if (!paramMatch && !memberMatch) {
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

            // Check for variable property hover ($Var.Property)
            const varPropPattern = /\$(\w+)\.(\w+)/g;
            while ((match = varPropPattern.exec(lineContent)) !== null) {
                const fullStart = match.index + 1;
                const fullEnd = fullStart + match[0].length;
                if (position.column >= fullStart && position.column <= fullEnd) {
                    const varName = "$" + match[1];
                    const propName = match[2];
                    const variable = variables.find((v) => v.name === varName);
                    if (variable) {
                        const typeDef = typeMap.get(variable.type);
                        if (typeDef) {
                            const prop = typeDef.properties.find((p) => p.name === propName);
                            if (prop) {
                                return {
                                    range: {
                                        startLineNumber: position.lineNumber,
                                        endLineNumber: position.lineNumber,
                                        startColumn: fullStart,
                                        endColumn: fullEnd,
                                    },
                                    contents: [
                                        { value: `**${varName}.${prop.name}** [${prop.type}]` },
                                        { value: `Property of ${variable.type}` },
                                    ],
                                };
                            }
                        }
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
                        const typeDef = typeMap.get(variable.type);
                        const contents: { value: string }[] = [
                            { value: `**${variable.name}** [${variable.type}]` },
                            { value: variable.description },
                        ];
                        if (typeDef) {
                            const propList = typeDef.properties
                                .map((p) => `- \`${p.name}\` [${p.type}]`)
                                .join("\n");
                            contents.push({ value: `**Properties:**\n${propList}` });
                        }
                        return {
                            range: {
                                startLineNumber: position.lineNumber,
                                endLineNumber: position.lineNumber,
                                startColumn: start,
                                endColumn: end,
                            },
                            contents,
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
