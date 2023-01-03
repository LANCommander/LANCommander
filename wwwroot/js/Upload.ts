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
    File: File;

    InitRoute: string = "/Upload/Init";
    ChunkRoute: string = "/Upload/Chunk";

    MaxChunkSize: number = 1024 * 1024 * 25;
    TotalChunks: number;
    CurrentChunk: number;
    Chunks: Chunk[];

    Key: string;

    Init(fileInputId: string, uploadButtonId: string) {
        this.FileInput = document.getElementById(fileInputId) as HTMLInputElement;
        this.UploadButton = document.getElementById(uploadButtonId) as HTMLButtonElement;
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

                this.OnComplete(this.Key);
            }
            catch {
                this.OnError();
            }
        }
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
    OnComplete: (key: string) => void;
    OnProgress: (percent: number) => void;
    OnError: () => void;
}