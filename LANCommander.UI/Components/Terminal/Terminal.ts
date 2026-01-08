import { Readline } from "xterm-readline"
import { FitAddon } from "@xterm/addon-fit"

export class Terminal {
    public static Create(): Terminal {
        return new Terminal();
    }
    
    constructor() {
        console.log("Terminal Constructor");
        (<any>window).XtermBlazor.registerAddons({
            "readline": new Readline(),
            "addon-fit": new FitAddon(),
        });
    }
}