﻿import { Chunk } from './Chunk';
import { UploadInitResponse } from './UploadInitResponse';
import { UploadInitRequest } from './UploadInitRequest';
import axios, { AxiosProgressEvent } from 'axios';

export class Uploader {
    FileInput: HTMLInputElement | undefined;
    UploadButton: HTMLButtonElement | undefined;
    ObjectKeyInput: HTMLInputElement | undefined;
    ProgressBar: HTMLElement | undefined;
    ProgressText: HTMLElement | undefined;
    ProgressRate: HTMLElement | undefined;

    File: File | undefined;

    InitRoute: string = "/api/Upload/Init";
    ChunkRoute: string = "/api/Upload/Chunk";

    MaxChunkSize: number = 1024 * 1024 * 50;
    TotalChunks: number = 0;
    CurrentChunk: number = 0;
    Chunks: Chunk[] = [];

    Key: string = "";
    StorageLocationId: string = "";
    Id: string = "";

    async Init(fileInputId: string, storageLocationId: string, objectKey: string)
    {
        debugger;
        this.FileInput = document.getElementById(fileInputId) as HTMLInputElement;
        this.ProgressBar = document.querySelector('.uploader-progress .ant-progress-circle-path');
        this.ProgressText = document.querySelector('.uploader-progress .ant-progress-text');
        this.ProgressRate = document.querySelector('.uploader-progress-rate');

        if (objectKey == undefined || objectKey == "") {
            try {
                var request = new UploadInitRequest();

                request.storageLocationId = storageLocationId;
                request.key = objectKey;

                var response = await axios.post<string>(this.InitRoute, request);

                this.Key = response.data;
            }
            catch (ex) {
                this.Key = null;
                console.error(`Could not init upload: ${ex}`);
            }
        }
        else
            this.Key = objectKey;

        this.Chunks = [];
    }

    async Upload(dotNetObject: any) {
        this.File = this.FileInput.files.item(0);
        this.TotalChunks = Math.ceil(this.File.size / this.MaxChunkSize);

        try {
            this.GetChunks();

            for (let chunk of this.Chunks) {
                await this.UploadChunk(chunk);
            }

            dotNetObject.invokeMethodAsync('JSOnUploadComplete', this.Key);
        } catch (ex) {
            dotNetObject.invokeMethodAsync('JSOnUploadError', ex.message);
            console.error(`Could not chunk upload: ${ex}`);
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
                headers: { "Content-Type": "multipart/form-data" },
                onUploadProgress: (progressEvent: AxiosProgressEvent) => {
                    console.log(progressEvent);

                    this.UpdateProgressBar(chunk.Index, progressEvent);
                }
            });
        } catch (ex) {
            console.error(ex);
            throw `Error uploading chunk ${chunk.Index}/${this.TotalChunks}`;
        }
    }

    GetChunks() {
        for (let currentChunk = 1; currentChunk <= this.TotalChunks; currentChunk++) {
            let start = (currentChunk - 1) * this.MaxChunkSize;
            let end = (currentChunk * this.MaxChunkSize) - 1;

            if (currentChunk == this.TotalChunks)
                end = this.File.size;

            this.Chunks.push(new Chunk(this.Key, start, end, currentChunk));
        }
    }

    UpdateProgressBar(chunkIndex: number, progressEvent: AxiosProgressEvent) {
        var percent = ((1 / this.TotalChunks) * progressEvent.progress) + ((chunkIndex - 1) / this.TotalChunks);

        this.ProgressBar.style.strokeDasharray = percent * 295.31 + 'px, 295.31px';
        this.ProgressText.innerText = Math.ceil(percent * 100) + '%';

        if (progressEvent.rate > 0)
            this.ProgressRate.innerText = this.GetHumanFileSize(progressEvent.rate, false, 1) + '/s';
    }

    GetHumanFileSize(bytes: number, si: boolean, dp: number) {
        const thresh = si ? 1000 : 1024;

        if (Math.abs(bytes) < thresh) {
            return bytes + ' B';
        }

        const units = si
            ? ['kB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB']
            : ['KiB', 'MiB', 'GiB', 'TiB', 'PiB', 'EiB', 'ZiB', 'YiB'];
        let u = -1;
        const r = 10 ** dp;

        do {
            bytes /= thresh;
            ++u;
        } while (Math.round(Math.abs(bytes) * r) / r >= thresh && u < units.length - 1);

        return bytes.toFixed(dp) + ' ' + units[u];
    }

    Clear() {
        this.File = undefined;
        
        if (this.FileInput != null)
            this.FileInput.value = "";
    }

    OnStart: () => void;
    OnComplete: (id: string, key: string) => void;
    OnProgress: (percent: number) => void;
    OnError: () => void;
}