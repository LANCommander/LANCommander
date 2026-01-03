import { SplitPane } from "./Components/SplitPane/SplitPane"
export { CreateUploader } from "./Components/ChunkUploader/UploaderFactory";
export { CreateTimeProvider } from "./Components/LocalTime/TimeProviderFactory";
import { Readline } from "xterm-readline"
import { FitAddon } from "@xterm/addon-fit"

(<any>window).SplitPane = new SplitPane();
(<any>window).XtermAddons = {
    Fit: FitAddon,
    Readline: Readline
}