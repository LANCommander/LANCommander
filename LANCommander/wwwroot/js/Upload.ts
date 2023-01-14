class Chunk {
    Index: number;
    Start: number;
    End: number;

    constructor(start: number, end: number, index: number) {
        this.Start = start;
        this.End = end;
        this.Index = index;
    }
}

class Uploader {
    ParentForm: HTMLFormElement;
    FileInput: HTMLInputElement;
    UploadButton: HTMLButtonElement;
    VersionInput: HTMLInputElement;
    LastVersionIdInput: HTMLInputElement;
    GameIdInput: HTMLInputElement;
    ChangelogTextArea: HTMLTextAreaElement;
    ObjectKeyInput: HTMLInputElement;
    IdInput: HTMLInputElement;

    File: File;

    InitRoute: string = "/Upload/Init";
    ChunkRoute: string = "/Upload/Chunk";
    ValidateRoute: string = "/Archives/Validate";

    MaxChunkSize: number = 1024 * 1024 * 25;
    TotalChunks: number;
    CurrentChunk: number;
    Chunks: Chunk[];

    Key: string;
    Id: string;

    Init(fileInputId: string, uploadButtonId: string) {
        this.FileInput = document.getElementById("File") as HTMLInputElement;
        this.UploadButton = document.getElementById("UploadButton") as HTMLButtonElement;
        this.VersionInput = document.getElementById("Version") as HTMLInputElement;
        this.ChangelogTextArea = document.getElementById("Changelog") as HTMLTextAreaElement;
        this.LastVersionIdInput = document.getElementById("LastVersion_Id") as HTMLInputElement;
        this.GameIdInput = document.getElementById("GameId") as HTMLInputElement;
        this.ParentForm = this.FileInput.closest("form");

        this.Chunks = [];

        this.UploadButton.onclick = async (e) => {
            await this.OnUploadButtonClicked(e);
        }
    }

    async OnUploadButtonClicked(e: MouseEvent) {
        e.preventDefault();

        this.OnStart();

        this.File = this.FileInput.files.item(0);
        this.TotalChunks = Math.ceil(this.File.size / this.MaxChunkSize);

        var response = await fetch(this.InitRoute, {
            method: "POST"
        });

        const data = await response.json();

        if (response.ok) {
            this.Key = data.key;

            this.GetChunks();

            try {
                for (let chunk of this.Chunks) {
                    await this.UploadChunk(chunk);
                }

                var isValid = await this.Validate();

                if (isValid)
                    this.OnComplete(this.Id, this.Key);
                else
                    this.OnError();
            }
            catch (ex) {
                this.OnError();
            }
        }
    }

    async UploadChunk(chunk: Chunk) {
        let formData = new FormData();

        formData.append('file', this.File.slice(chunk.Start, chunk.End + 1));
        formData.append('start', chunk.Start.toString());
        formData.append('end', chunk.End.toString());
        formData.append('key', this.Key);
        formData.append('total', this.File.size.toString());

        console.info(`Uploading chunk ${chunk.Index}/${this.TotalChunks}...`);

        let chunkResponse = await fetch(this.ChunkRoute, {
            method: "POST",
            body: formData
        });

        if (!chunkResponse)
            throw `Error uploading chunk ${chunk.Index}/${this.TotalChunks}`;

        this.OnProgress(chunk.Index / this.TotalChunks);
    }

    async Validate(): Promise<boolean> {
        let formData = new FormData();

        formData.append('Version', this.VersionInput.value);
        formData.append('Changelog', this.ChangelogTextArea.value);
        formData.append('GameId', this.GameIdInput.value);
        formData.append('ObjectKey', this.Key);

        let validationResponse = await fetch(`${this.ValidateRoute}/${this.Key}`, {
            method: "POST",
            body: formData
        });

        if (!validationResponse.ok) {
            ErrorModal.Show("Archive Invalid", await validationResponse.text())

            return false;
        }

        let data = await validationResponse.json();

        if (data == null || data.Id === "") {
            ErrorModal.Show("Upload Error", "Something interfered with the upload. Try again.");

            return false;
        }

        this.Id = data.Id;

        return true;
    }

    GetChunks() {
        for (let currentChunk = 1; currentChunk <= this.TotalChunks; currentChunk++) {
            let start = (currentChunk - 1) * this.MaxChunkSize;
            let end = (currentChunk * this.MaxChunkSize) - 1;

            if (currentChunk == this.TotalChunks)
                end = this.File.size;

            this.Chunks.push(new Chunk(start, end, currentChunk));
        }
    }

    OnStart: () => void;
    OnComplete: (id: string, key: string) => void;
    OnProgress: (percent: number) => void;
    OnError: () => void;
}