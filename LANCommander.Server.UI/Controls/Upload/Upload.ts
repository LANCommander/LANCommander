// Posts files picked or dropped on an Upload control to its action URL, reporting progress to .NET.
export class Upload {
    private readonly dotNetRef: any;
    private readonly input: HTMLInputElement;
    private readonly dropZone: HTMLElement;
    private action = "";
    private fieldName = "file";
    private data: Record<string, string> = {};
    private nextId = 0;
    private requests = new Map<string, XMLHttpRequest>();

    constructor(dotNetRef: any, inputId: string, dropZoneId: string) {
        this.dotNetRef = dotNetRef;
        this.input = document.getElementById(inputId) as HTMLInputElement;
        this.dropZone = document.getElementById(dropZoneId) as HTMLElement;

        this.input.addEventListener("change", () => {
            this.send(this.input.files);
            this.input.value = "";
        });

        this.dropZone.addEventListener("dragover", e => {
            e.preventDefault();
            this.dropZone.classList.add("lc-upload-drop-active");
        });

        this.dropZone.addEventListener("dragleave", () => this.dropZone.classList.remove("lc-upload-drop-active"));

        this.dropZone.addEventListener("drop", e => {
            e.preventDefault();
            this.dropZone.classList.remove("lc-upload-drop-active");

            if (!this.input.disabled)
                this.send(this.input.multiple ? e.dataTransfer?.files : this.first(e.dataTransfer?.files));
        });
    }

    public static Create(dotNetRef: any, inputId: string, dropZoneId: string): Upload {
        return new Upload(dotNetRef, inputId, dropZoneId);
    }

    Configure(action: string, fieldName: string, data: Record<string, string> | null) {
        this.action = action;
        this.fieldName = fieldName;
        this.data = data ?? {};
    }

    Cancel(id: string) {
        this.requests.get(id)?.abort();
    }

    Dispose() {
        this.requests.forEach(request => request.abort());
        this.requests.clear();
    }

    private first(files: FileList | undefined): File[] {
        return files && files.length > 0 ? [files[0]] : [];
    }

    private send(files: FileList | File[] | null | undefined) {
        for (const file of Array.from(files ?? []))
            this.sendFile(file);
    }

    private sendFile(file: File) {
        const id = `upload-${this.nextId++}`;
        const form = new FormData();

        form.append(this.fieldName, file, file.name);

        for (const [key, value] of Object.entries(this.data))
            form.append(key, value);

        const request = new XMLHttpRequest();

        this.requests.set(id, request);

        request.upload.addEventListener("progress", e => {
            if (e.lengthComputable)
                this.dotNetRef.invokeMethodAsync("OnProgress", id, e.loaded, e.total);
        });

        request.addEventListener("loadend", () => {
            this.requests.delete(id);

            const ok = request.status >= 200 && request.status < 300;
            const message = ok ? null : (request.responseText || request.statusText || "Upload cancelled");

            this.dotNetRef.invokeMethodAsync("OnFinished", id, ok, message);
        });

        this.dotNetRef.invokeMethodAsync("OnStarted", id, file.name, file.size);

        request.open("POST", this.action);
        request.send(form);
    }
}
