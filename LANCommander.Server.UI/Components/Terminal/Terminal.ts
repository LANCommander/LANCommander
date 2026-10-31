import { Readline } from "xterm-readline"
import { FitAddon } from "@xterm/addon-fit"

export class Terminal {
    public static Create(): Terminal {
        return new Terminal();
    }
    
    constructor() {
        (<any>window).XtermBlazor.registerAddons({
            "readline": new Readline(),
            "addon-fit": new FitAddon(),
        });
    }

    /** Sets an xterm option XtermBlazor's typed options can't carry, e.g. a fractional line height. */
    public SetOption(id: string, name: string, value: any): void {
        const terminal = (<any>window).XtermBlazor?.getTerminalById?.(id);

        if (terminal)
            terminal.options[name] = value;
    }

    /** Tells the component whenever the terminal's element changes size, so it can refit. */
    public ObserveResize(element: HTMLElement, component: any): { Dispose(): void } {
        let pending = 0;

        const observer = new ResizeObserver(() => {
            // One refit per frame however many sizes a drag goes through
            cancelAnimationFrame(pending);
            pending = requestAnimationFrame(() => component.invokeMethodAsync("ElementResized"));
        });

        observer.observe(element);

        return {
            Dispose: () => {
                cancelAnimationFrame(pending);
                observer.disconnect();
            },
        };
    }
}