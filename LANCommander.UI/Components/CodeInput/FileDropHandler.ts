declare const monaco: any;

export function SetupFileDropHandler(): void {
    if (typeof monaco === "undefined")
        return;

    const editors = monaco.editor.getEditors();
    const editor = editors[editors.length - 1];
    
    if (!editor)
        return;

    const domNode = editor.getDomNode();
    
    if (!domNode)
        return;

    domNode.addEventListener("dragover", (e: DragEvent) => {
        if (e.dataTransfer?.types?.includes("Files")) {
            e.preventDefault();
            e.stopPropagation();
        }
    });

    domNode.addEventListener("drop", (e: DragEvent) => {
        const files = e.dataTransfer?.files;
        
        if (files && files.length > 0) {
            e.preventDefault();
            e.stopPropagation();
            handleFile(editor, files[0]);
        }
    });
    
    document.addEventListener("paste", (e: ClipboardEvent) => {
        if (!editor.hasWidgetFocus())
            return;
        
        if (!e.clipboardData?.files.length)
            return;

        e.preventDefault();
        e.stopPropagation();
        
        handleFile(editor, e.clipboardData.files[0]);
    }, true);
}

const MAX_FILE_SIZE = 1024 * 1024; // 1 MB

function IsTextFile(file: File): boolean {
    if (file.type.startsWith("text/")) return true;

    const textExtensions = [
        ".reg", ".ps1", ".txt", ".cfg", ".ini", ".conf",
        ".json", ".xml", ".yaml", ".yml", ".csv", ".log",
        ".bat", ".cmd", ".sh", ".py", ".lua",
    ];

    return textExtensions.includes(getFileExtension(file.name));
}

function handleFile(editor: any, file: File): void {
    if (file.size > MAX_FILE_SIZE)
        return;
    
    if (!IsTextFile(file))
        return;
    
    const reader = new FileReader();
    
    reader.onload = () => {
        const content = reader.result as string;
        const ext = getFileExtension(file.name);

        if (ext === ".reg") {
            const psCommands = ParseRegToPowerShell(content);
            insertTextAtCursor(editor, psCommands);
        } else if (ext === ".ps1") {
            editor.setValue(content);
        } else {
            const hereString = `@'\n${content}\n'@`;
            
            insertTextAtCursor(editor, hereString);
        }
    };
    
    reader.readAsText(file);
}

function getFileExtension(filename: string): string {
    const idx = filename.lastIndexOf(".");
    
    return idx >= 0 ? filename.substring(idx).toLowerCase() : "";
}

function insertTextAtCursor(editor: any, text: string): void {
    const selection = editor.getSelection();
    
    editor.executeEdits("fileImport", [{
        range: selection,
        text: text,
        forceMoveMarkers: true,
    }]);
}

interface RegEntry {
    type: "key" | "sz" | "dword" | "binary";
    path: string;
    property?: string;
    value?: any;
}

function ParseRegToPowerShell(regContent: string): string {
    const entries = ParseRegFile(regContent);
    const lines: string[] = [];

    for (const entry of entries) {
        switch (entry.type) {
            case "key":
                if (lines.length > 0)
                    lines.push("");
                
                lines.push(`New-Item -Path "registry::\\${entry.path}"`);
                break;
            case "sz": {
                const escaped = (entry.value as string).replace(/"/g, '""""');
                
                lines.push(`New-ItemProperty -Path "registry::\\${entry.path}" -Name "${entry.property}" -Value "${escaped}" -Force`);
                break;
            }
            case "dword":
                lines.push(`New-ItemProperty -Path "registry::\\${entry.path}" -Name "${entry.property}" -Value ${entry.value} -Force`);
                break;
            case "binary": {
                const bytes = entry.value as number[];
                const chunks: string[] = [];
                
                for (let i = 0; i < bytes.length; i += 32) {
                    const chunk = bytes.slice(i, i + 32);
                    chunks.push(chunk.map(b => "0x" + b.toString(16).toUpperCase().padStart(2, "0")).join(", "));
                }
                
                const formatted = chunks.join("\n\t");
                
                lines.push(`New-ItemProperty -Path "registry::\\${entry.path}" -Name "${entry.property}" -PropertyType Binary -Value ([byte[]]@(\n\t${formatted}\n)) -Force`);
                break;
            }
        }
    }

    return lines.join("\n");
}

function ParseRegFile(content: string): RegEntry[] {
    const entries: RegEntry[] = [];
    const normalized = content.replace(/\r\n/g, "\n").replace(/\r/g, "\n");
    const joined = normalized.replace(/\\\n\s*/g, "");
    const rawLines = joined.split("\n");

    let currentPath = "";

    for (const line of rawLines) {
        const trimmed = line.trim();

        // Skip empty lines, comments, and headers
        if (!trimmed || trimmed.startsWith(";") ||
            trimmed === "Windows Registry Editor Version 5.00" ||
            trimmed === "REGEDIT4") {
            
            continue;
        }

        // Key section: [HKEY_...]
        const keyMatch = trimmed.match(/^\[(.+)\]$/);
        
        if (keyMatch) {
            const path = keyMatch[1];
            
            if (path.startsWith("-"))
                continue;
            currentPath = path;
            entries.push({ type: "key", path: currentPath });
            continue;
        }

        if (!currentPath)
            continue;

        // Parse value lines
        let name: string;
        let valueStr: string;

        if (trimmed.startsWith("@=")) {
            name = "@";
            valueStr = trimmed.substring(2);
        } else {
            const valueMatch = trimmed.match(/^"(.+?)"=(.*)$/);
            
            if (!valueMatch)
                continue;
            
            name = valueMatch[1].replace(/\\\\/g, "\\").replace(/\\"/g, '"');
            valueStr = valueMatch[2];
        }

        // Skip delete values
        if (valueStr === "-")
            continue;

        // String value: "..."
        if (valueStr.startsWith('"') && valueStr.endsWith('"')) {
            const str = valueStr.slice(1, -1).replace(/\\\\/g, "\\").replace(/\\"/g, '"');
            
            entries.push({ type: "sz", path: currentPath, property: name, value: str });
            
            continue;
        }

        // DWORD: dword:XXXXXXXX
        const dwordMatch = valueStr.match(/^dword:([0-9a-fA-F]{1,8})$/);
        
        if (dwordMatch) {
            const val = parseInt(dwordMatch[1], 16);
            
            entries.push({ type: "dword", path: currentPath, property: name, value: val });
            continue;
        }

        // Binary/hex types: hex:XX,XX,... or hex(N):XX,XX,...
        const hexMatch = valueStr.match(/^hex(?:\([0-9a-fA-F]+\))?:(.*)$/);
        
        if (hexMatch) {
            const hexStr = hexMatch[1].replace(/\s/g, "");
            const bytes = hexStr.split(",")
                .filter(s => s.length > 0)
                .map(s => parseInt(s, 16));
            
            entries.push({ type: "binary", path: currentPath, property: name, value: bytes });
        }
    }

    return entries;
}
