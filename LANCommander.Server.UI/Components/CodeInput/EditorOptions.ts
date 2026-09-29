declare const monaco: any;

/**
 * Updates options on the editor inside the element with this id, for values BlazorMonaco's typed
 * options can't carry (a fractional font size).
 */
export function setEditorOptions(id: string, options: any): void {
    if (typeof monaco === "undefined")
        return;

    const editor = monaco.editor.getEditors().find((e: any) => {
        const container = e.getContainerDomNode();

        return container?.id === id || container?.closest?.(`[id="${id}"]`) != null;
    });

    editor?.updateOptions(options);
}
