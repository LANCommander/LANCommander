import Chunk from './Chunk';
import UploadInitResponse from './UploadInitResponse';
import axios from 'axios';

export default class Uploader {
    FileInput: HTMLInputElement | undefined;
    UploadButton: HTMLButtonElement | undefined;
    ObjectKeyInput: HTMLInputElement | undefined;

    File: File | undefined;

    InitRoute: string = "/Upload/Init";
    ChunkRoute: string = "/Upload/Chunk";

    MaxChunkSize: number = 1024 * 1024 * 50;
    TotalChunks: number = 0;
    CurrentChunk: number = 0;
    Chunks: Chunk[] = [];

    Key: string = "";
    Id: string = "";

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

        try {
            var resp = await axios.post<UploadInitResponse>(this.InitRoute);

            this.Key = resp.data.key;

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
        } catch (ex) {
            console.error(`Could not init upload: ${ex}`);

            return null;
        }
    }

    async OnUploadButtonClicked(e: MouseEvent) {
        e.preventDefault();

        this.OnStart();

        this.File = this.FileInput.files.item(0);
        this.TotalChunks = Math.ceil(this.File.size / this.MaxChunkSize);

        try {
            var resp = await axios.post<UploadInitResponse>(this.InitRoute);

            this.Key = resp.data.key;

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
        } catch (ex) {
            console.error(`Could not init upload: ${ex}`);
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

        try {
            let chunkResponse = await axios({
                method: "post",
                url: this.ChunkRoute,
                data: formData,
                headers: { "Content-Type": "multipart/form-data" }
            });
        } catch (ex) {
            throw `Error uploading chunk ${chunk.Index}/${this.TotalChunks}`;
        } finally {
            var percent = Math.ceil((chunk.Index / this.TotalChunks) * 100);

            let progress: HTMLElement = document.querySelector('.ant-progress-bg');

            progress.style.width = percent + '%';
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
    OnComplete: (id: string, key: string) => void;
    OnProgress: (percent: number) => void;
    OnError: () => void;
}