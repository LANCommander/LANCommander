import { Uploader } from "./Scripts/Uploader"
import { SplitPane } from "./Scripts/SplitPane"
import { Readline } from "xterm-readline"
import { FitAddon } from "@xterm/addon-fit"

(<any>window).Uploader = new Uploader();
(<any>window).SplitPane = new SplitPane();
(<any>window).XtermAddons = {
    Fit: FitAddon,
    Readline: Readline
}