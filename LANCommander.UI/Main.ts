import { Uploader } from "./Components/ChunkUploader/Uploader";
import { SplitPane } from "./Components/SplitPane/SplitPane";
import { TimeProvider } from "./Components/LocalTime/TimeProvider";
import { Readline } from "xterm-readline";
import { FitAddon } from "@xterm/addon-fit";
import "./_Imports.razor.scss";

(<any>window).Uploader = new Uploader();
(<any>window).SplitPane = new SplitPane();
(<any>window).TimeProvider = new TimeProvider();
(<any>window).XtermAddons = {
  Fit: FitAddon,
  Readline: Readline,
};
