// Helpers for the page editor's Markdown editor (PSC MarkdownEditor, which wraps EasyMDE).
// Its script keeps each editor in the global _instances, keyed by the id it generates for the
// editor's textarea, so the editor is found through an element wrapped around it.
window.pagesEditor = {
    find(hostId) {
        const host = document.getElementById(hostId);

        if (!host || typeof _instances === "undefined") {
            return null;
        }

        for (const textarea of host.querySelectorAll("textarea[id]")) {
            if (_instances[textarea.id]) {
                return _instances[textarea.id].editor;
            }
        }

        return null;
    },

    // Opens the picker of a hidden file input, from a button elsewhere on the page
    openFilePicker(inputId) {
        document.getElementById(inputId)?.click();
    },

    // Inserts text on its own lines at the cursor, replacing any selection. Embeds only work as a
    // whole line, so a line break is added either side unless the cursor is already at a line edge.
    insertBlock(hostId, text) {
        const editor = window.pagesEditor.find(hostId);

        if (!editor) {
            return false;
        }

        const cm = editor.codemirror;
        const from = cm.getCursor("from");
        const to = cm.getCursor("to");
        const before = from.ch > 0 ? "\n\n" : "";
        const after = to.ch < cm.getLine(to.line).length ? "\n\n" : "\n";

        cm.replaceSelection(before + text + after);
        cm.focus();

        return true;
    }
};
