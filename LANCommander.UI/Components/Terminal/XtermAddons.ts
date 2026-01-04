import { Readline } from "xterm-readline"
import { FitAddon } from "@xterm/addon-fit"

export function RegisterXtermAddons() {
    (<any>window).XtermBlazor.registerAddons({
       "readline": new Readline(),
       "addon-fit": new FitAddon(), 
    });
}