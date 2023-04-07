var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
class Chunk {
    constructor(start, end, index) {
        this.Start = start;
        this.End = end;
        this.Index = index;
    }
}
class Uploader {
    constructor() {
        this.InitRoute = "/Upload/Init";
        this.ChunkRoute = "/Upload/Chunk";
        this.MaxChunkSize = 1024 * 1024 * 25;
    }
    Init(fileInputId, uploadButtonId, objectKeyInputId) {
        this.FileInput = document.getElementById(fileInputId);
        this.UploadButton = document.getElementById(uploadButtonId);
        this.ObjectKeyInput = document.getElementById(objectKeyInputId);
        this.Chunks = [];
        this.UploadButton.onclick = (e) => __awaiter(this, void 0, void 0, function* () {
            yield this.OnUploadButtonClicked(e);
        });
    }
    Upload(fileInputId) {
        return __awaiter(this, void 0, void 0, function* () {
            this.FileInput = document.getElementById(fileInputId);
            this.Chunks = [];
            this.File = this.FileInput.files.item(0);
            this.TotalChunks = Math.ceil(this.File.size / this.MaxChunkSize);
            var response = yield fetch(this.InitRoute, {
                method: "POST"
            });
            const data = yield response.json();
            if (response.ok) {
                this.Key = data.key;
                this.GetChunks();
                try {
                    for (let chunk of this.Chunks) {
                        yield this.UploadChunk(chunk);
                    }
                }
                catch (ex) {
                    this.OnError();
                }
                return this.Key;
            }
            return null;
        });
    }
    OnUploadButtonClicked(e) {
        return __awaiter(this, void 0, void 0, function* () {
            e.preventDefault();
            this.OnStart();
            this.File = this.FileInput.files.item(0);
            this.TotalChunks = Math.ceil(this.File.size / this.MaxChunkSize);
            var response = yield fetch(this.InitRoute, {
                method: "POST"
            });
            const data = yield response.json();
            if (response.ok) {
                this.Key = data.key;
                this.GetChunks();
                try {
                    for (let chunk of this.Chunks) {
                        yield this.UploadChunk(chunk);
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
        });
    }
    UploadChunk(chunk) {
        return __awaiter(this, void 0, void 0, function* () {
            let formData = new FormData();
            formData.append('file', this.File.slice(chunk.Start, chunk.End + 1));
            formData.append('start', chunk.Start.toString());
            formData.append('end', chunk.End.toString());
            formData.append('key', this.Key);
            formData.append('total', this.File.size.toString());
            console.info(`Uploading chunk ${chunk.Index}/${this.TotalChunks}...`);
            let chunkResponse = yield fetch(this.ChunkRoute, {
                method: "POST",
                body: formData
            });
            if (!chunkResponse)
                throw `Error uploading chunk ${chunk.Index}/${this.TotalChunks}`;
            //this.OnProgress(chunk.Index / this.TotalChunks);
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
//# sourceMappingURL=Upload.js.map