declare const monaco: any;

const schemaKeywords = [
    { label: "CommandTemplate", detail: "string", documentation: "Command template with {exe} and {args} placeholders" },
    { label: "Options", detail: "map", documentation: "Dictionary of option definitions" },
    { label: "Type", detail: "string | bool | int | choice", documentation: "Data type of this option" },
    { label: "DisplayName", detail: "string", documentation: "Human-readable display name for this option" },
    { label: "IsEnvironmentVariable", detail: "boolean", documentation: "Whether this option maps to an environment variable" },
    { label: "Default", detail: "string", documentation: "Default value for this option" },
    { label: "Description", detail: "string", documentation: "Human-readable description" },
    { label: "Required", detail: "boolean", documentation: "Whether this option must be specified" },
    { label: "Choices", detail: "list", documentation: "List of allowed values (for choice type)" },
];

const typeValues = ["string", "bool", "int", "choice"];

let _registered = false;

export function registerYamlCompletions(): void {
    if (_registered) return;
    _registered = true;

    monaco.languages.registerCompletionItemProvider("yaml", {
        triggerCharacters: [" ", ":"],
        provideCompletionItems(model: any, position: any) {
            const lineContent = model.getLineContent(position.lineNumber);
            const textUntilPosition = lineContent.substring(0, position.column - 1);

            const word = model.getWordUntilPosition(position);
            const range = {
                startLineNumber: position.lineNumber,
                endLineNumber: position.lineNumber,
                startColumn: word.startColumn,
                endColumn: word.endColumn,
            };

            // After "Type: " suggest type values
            if (/^\s*Type:\s*/.test(textUntilPosition)) {
                return {
                    suggestions: typeValues.map((t) => ({
                        label: t,
                        kind: monaco.languages.CompletionItemKind.Value,
                        insertText: t,
                        range,
                        detail: "Option type",
                    })),
                };
            }

            // Suggest schema keywords
            return {
                suggestions: schemaKeywords.map((kw) => ({
                    label: kw.label,
                    kind: monaco.languages.CompletionItemKind.Property,
                    insertText: kw.label + ": ",
                    range,
                    detail: kw.detail,
                    documentation: kw.documentation,
                })),
            };
        },
    });

    monaco.languages.registerHoverProvider("yaml", {
        provideHover(model: any, position: any) {
            const word = model.getWordAtPosition(position);
            if (!word) return null;

            const keyword = schemaKeywords.find((kw) => kw.label === word.word);
            if (!keyword) return null;

            return {
                range: {
                    startLineNumber: position.lineNumber,
                    endLineNumber: position.lineNumber,
                    startColumn: word.startColumn,
                    endColumn: word.endColumn,
                },
                contents: [
                    { value: `**${keyword.label}** — \`${keyword.detail}\`` },
                    { value: keyword.documentation },
                ],
            };
        },
    });
}
