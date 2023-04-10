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
    FileInput: HTMLInputElement;
    UploadButton: HTMLButtonElement;
    ObjectKeyInput: HTMLInputElement;

    File: File;

    InitRoute: string = "/Upload/Init";
    ChunkRoute: string = "/Upload/Chunk";

    MaxChunkSize: number = 1024 * 1024 * 50;
    TotalChunks: number;
    CurrentChunk: number;
    Chunks: Chunk[];

    Key: string;
    Id: string;

    Init(fileInputId: string, uploadButtonId: string, objectKeyInputId: string) {
        this.FileInput = document.getElementById(fileInputId) as HTMLInputElement;
        this.UploadButton = document.getElementById(uploadButtonId) as HTMLButtonElement;
        this.ObjectKeyInput = document.getElementById(objectKeyInputId) as HTMLInputElement;

        this.Chunks = [];

        this.UploadButton.onclick = async (e) => {
            await this.OnUploadButtonClicked(e);
        }
    }

    async Upload(fileInputId: string) {
        this.FileInput = document.getElementById(fileInputId) as HTMLInputElement;
        this.Chunks = [];

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
            }
            catch (ex) {
                this.OnError();
            }

            return this.Key;
        }

        return null;
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

                this.ObjectKeyInput.value = this.Key;

                var event = document.createEvent('HTMLEvents');
                event.initEvent('change', false, true);
                this.ObjectKeyInput.dispatchEvent(event);
                this.OnComplete(this.Id, this.Key);
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

        var percent = Math.ceil((chunk.Index / this.TotalChunks) * 100);

        let progress: HTMLElement = document.querySelector('.ant-progress-bg');

        progress.style.width = percent + '%';
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