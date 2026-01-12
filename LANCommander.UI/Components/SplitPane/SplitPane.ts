import Split from 'split.js'

export class SplitPane {    
    constructor(paneId: string) {
        this.Init(paneId);
    }
    
    public static Create(paneId: string): SplitPane {
        return new SplitPane(paneId);
    }

    Init(paneId: string) {
        let splitPane = document.querySelector<HTMLElement>(`#split-pane-${paneId}`);

        if (splitPane != null) {
            let panes = Array.from(splitPane.querySelectorAll<HTMLElement>('.pane'));
            let opts = new SplitPaneOptions();

            for (let pane of panes) {
                let size = parseInt(pane.getAttribute('data-size'));

                if (isNaN(size))
                    opts.sizes.push(0);
                else
                    opts.sizes.push(size);
            }

            Split(panes, opts);
        }
    }
}

class SplitPaneOptions implements Split.Options {
    sizes: number[] = [];
}