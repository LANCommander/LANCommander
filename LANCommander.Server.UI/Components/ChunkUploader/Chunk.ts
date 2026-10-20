export class Chunk {
    Key: string;
    Index: number;
    Start: number;
    End: number;

    constructor(key: string, start: number, end: number, index: number) {
        this.Key = key;
        this.Start = start;
        this.End = end;
        this.Index = index;
    }
}