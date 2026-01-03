export { CreateUploader } from "./Components/ChunkUploader/UploaderFactory";
export { CreateSplitPane } from "./Components/SplitPane/SplitPaneFactory";
export { CreateTimeProvider } from "./Components/LocalTime/TimeProviderFactory";
import { Readline } from "xterm-readline"
import { FitAddon } from "@xterm/addon-fit"

(<any>window).XtermAddons = {
    Fit: FitAddon,
    Readline: Readline
}