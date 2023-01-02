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
    Init(elementId) {
        this.FileInput = document.getElementById(elementId);
        this.ParentForm = this.FileInput.closest("form");
        this.Chunks = [];
        this.ParentForm.onsubmit = (e) => __awaiter(this, void 0, void 0, function* () {
            yield this.HandleFormSubmit(e);
        });
    }
    HandleFormSubmit(e) {
        return __awaiter(this, void 0, void 0, function* () {
            e.preventDefault();
            this.File = this.FileInput.files.item(0);
            this.TotalChunks = Math.ceil(this.File.size / this.MaxChunkSize);
            var response = yield fetch(this.InitRoute, {
                method: "POST"
            });
            const data = yield response.json();
            if (response.ok) {
                this.Key = data.key;
                this.GetChunks();
                for (let chunk of this.Chunks) {
                    let dataChunk = yield this.ReadChunkFromFile(this.File, chunk);
                    let formData = new FormData();
                    formData.append('file', this.File.slice(chunk.Start, chunk.End));
                    formData.append('start', chunk.Start.toString());
                    formData.append('end', chunk.End.toString());
                    formData.append('key', this.Key);
                    formData.append('total', this.File.size.toString());
                    console.info(`Uploading chunk ${chunk.Index}/${this.TotalChunks}...`);
                    let response = yield fetch(this.ChunkRoute, {
                        method: "POST",
                        body: formData
                    });
                    debugger;
                }
            }
        });
    }
    ReadChunkFromFile(file, chunk) {
        return new Promise((resolve, reject) => {
            let reader = new FileReader();
            let blob = this.File.slice(chunk.Start, chunk.End);
            reader.onload = () => {
                resolve(reader.result);
            };
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
//# sourceMappingURL=Upload.js.map