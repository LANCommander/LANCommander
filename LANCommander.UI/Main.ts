import { SplitPane } from "./Components/SplitPane/SplitPane"
import { TimeProvider } from "./Components/LocalTime/TimeProvider";
export { CreateUploader } from "./Components/ChunkUploader/UploaderFactory";
import { Readline } from "xterm-readline"
import { FitAddon } from "@xterm/addon-fit"

(<any>window).SplitPane = new SplitPane();
(<any>window).TimeProvider = new TimeProvider();
(<any>window).XtermAddons = {
    Fit: FitAddon,
    Readline: Readline
}