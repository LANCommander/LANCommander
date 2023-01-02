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
    File: File;

    InitRoute: string = "/Upload/Init";
    ChunkRoute: string = "/Upload/Chunk";

    MaxChunkSize: number = 1024 * 1024 * 25;
    TotalChunks: number;
    CurrentChunk: number;
    Chunks: Chunk[];

    Key: string;

    Init(elementId: string) {
        this.FileInput = document.getElementById(elementId) as HTMLInputElement;
        this.ParentForm = this.FileInput.closest("form");

        this.Chunks = [];

        this.ParentForm.onsubmit = async (e) => {
            await this.HandleFormSubmit(e);
        };
    }

    async HandleFormSubmit(e: SubmitEvent) {
        e.preventDefault();

        this.File = this.FileInput.files.item(0);
        this.TotalChunks = Math.ceil(this.File.size / this.MaxChunkSize);

        var response = await fetch(this.InitRoute, {
            method: "POST"
        });

        const data = await response.json();

        if (response.ok) {
            this.Key = data.key;

            this.GetChunks();

            for (let chunk of this.Chunks) {
                let dataChunk = await this.ReadChunkFromFile(this.File, chunk);

                let formData = new FormData();

                formData.append('file', this.File.slice(chunk.Start, chunk.End));
                formData.append('start', chunk.Start.toString());
                formData.append('end', chunk.End.toString());
                formData.append('key', this.Key);
                formData.append('total', this.File.size.toString());

                console.info(`Uploading chunk ${chunk.Index}/${this.TotalChunks}...`);

                let response = await fetch(this.ChunkRoute, {
                    method: "POST",
                    body: formData
                });

                debugger;
            }
        }
    }

    ReadChunkFromFile(file: File, chunk: Chunk): Promise<string | ArrayBuffer>  {
        return new Promise((resolve, reject) => {
            let reader = new FileReader();
            let blob = this.File.slice(chunk.Start, chunk.End);

            reader.onload = () => {
                resolve(reader.result);
            }

            reader.onerror = reject;

            reader.readAsArrayBuffer(blob);
        });
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
}