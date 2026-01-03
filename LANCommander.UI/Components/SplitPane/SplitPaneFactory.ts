import { SplitPane } from "./SplitPane";

export function CreateSplitPane(panelId: string): SplitPane {
    return new SplitPane(panelId);
}