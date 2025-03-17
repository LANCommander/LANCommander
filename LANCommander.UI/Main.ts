import { Uploader } from "./Scripts/Uploader"
import { SplitPane } from "./Scripts/SplitPane"
import { TimeProvider } from "./Scripts/TimeProvider";
import { Readline } from "xterm-readline"
import { FitAddon } from "@xterm/addon-fit"

(<any>window).Uploader = new Uploader();
(<any>window).SplitPane = new SplitPane();
(<any>window).TimeProvider = new TimeProvider();
(<any>window).XtermAddons = {
    Fit: FitAddon,
    Readline: Readline
}