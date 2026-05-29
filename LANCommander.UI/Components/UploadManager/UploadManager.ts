import { Chunk } from '../ChunkUploader/Chunk';
import { UploadInitRequest } from '../ChunkUploader/UploadInitRequest';
import axios, { AxiosProgressEvent, CancelTokenSource } from 'axios';

interface ActiveUpload {
    uploadId: string;
    file: File;
    key: string;
    storageLocationId: string;
    totalChunks: number;
    chunks: Chunk[];
    cancelSource: CancelTokenSource;
}

export class UploadManager {
    private static _instance: UploadManager;
    private _dotNetRef: any;
    private _uploads: Map<string, ActiveUpload> = new Map();

    private readonly InitRoute: string = "/api/Upload/Init";
    private readonly ChunkRoute: string = "/api/Upload/Chunk";
    private readonly MaxChunkSize: number = 1024 * 1024 * 50;

    public static Create(): UploadManager {
        if (!UploadManager._instance)
            UploadManager._instance = new UploadManager();

        return UploadManager._instance;
    }

    Initialize(dotNetRef: any) {
        this._dotNetRef = dotNetRef;
    }

    async StartUpload(uploadId: string, fileInputId: string, storageLocationId: string, objectKey: string) {
        const fileInput = document.getElementById(fileInputId) as HTMLInputElement;

        if (!fileInput?.files?.length)
            return;

        const file = fileInput.files.item(0)!;

        let key = objectKey;

        if (!key || key === "") {
            const request = new UploadInitRequest();
            request.storageLocationId = storageLocationId;
            request.key = "";

            const response = await axios.post<string>(this.InitRoute, request);
            key = response.data;
        }

        const totalChunks = Math.ceil(file.size / this.MaxChunkSize);
        const chunks: Chunk[] = [];

        for (let i = 1; i <= totalChunks; i++) {
            const start = (i - 1) * this.MaxChunkSize;
            let end = (i * this.MaxChunkSize) - 1;
            if (i === totalChunks) end = file.size;
            chunks.push(new Chunk(key, start, end, i));
        }

        const cancelSource = axios.CancelToken.source();

        const upload: ActiveUpload = {
            uploadId,
            file,
            key,
            storageLocationId,
            totalChunks,
            chunks,
            cancelSource,
        };

        this._uploads.set(uploadId, upload);

        this.processUpload(upload);
    }

    CancelUpload(uploadId: string) {
        const upload = this._uploads.get(uploadId);
        if (upload) {
            upload.cancelSource.cancel('Upload cancelled by user');
            this._uploads.delete(uploadId);
        }
    }

    GetActiveUploadIds(): string[] {
        return Array.from(this._uploads.keys());
    }

    private async processUpload(upload: ActiveUpload) {
        try {
            for (const chunk of upload.chunks) {
                await this.uploadChunk(upload, chunk);
            }

            this._uploads.delete(upload.uploadId);

            if (this._dotNetRef) {
                this._dotNetRef.invokeMethodAsync('JSOnUploadComplete', upload.uploadId, upload.key);
            }
        } catch (ex) {
            this._uploads.delete(upload.uploadId);

            if (axios.isCancel(ex))
                return;

            const message = ex instanceof Error ? ex.message : String(ex);

            if (this._dotNetRef) {
                this._dotNetRef.invokeMethodAsync('JSOnUploadError', upload.uploadId, message);
            }

            console.error(`Upload ${upload.uploadId} failed: ${ex}`);
        }
    }

    private async uploadChunk(upload: ActiveUpload, chunk: Chunk) {
        const formData = new FormData();

        formData.append('file', upload.file.slice(chunk.Start, chunk.End + 1));
        formData.append('start', chunk.Start.toString());
        formData.append('end', chunk.End.toString());
        formData.append('key', upload.key);
        formData.append('total', upload.file.size.toString());

        await axios({
            method: "post",
            url: this.ChunkRoute,
            data: formData,
            headers: { "Content-Type": "multipart/form-data" },
            cancelToken: upload.cancelSource.token,
            onUploadProgress: (progressEvent: AxiosProgressEvent) => {
                const percent = ((1 / upload.totalChunks) * (progressEvent.progress ?? 0)) + ((chunk.Index - 1) / upload.totalChunks);
                const rate = progressEvent.rate ?? 0;

                if (this._dotNetRef) {
                    // Only send rate when it's a meaningful value; the C# side
                    // preserves the last known speed so it doesn't flicker to 0
                    // between chunks.
                    this._dotNetRef.invokeMethodAsync('JSOnUploadProgress', upload.uploadId, Math.ceil(percent * 100), rate);
                }
            }
        });
    }
}
