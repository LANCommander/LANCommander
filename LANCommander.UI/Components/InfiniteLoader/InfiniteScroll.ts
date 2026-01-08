import { InfiniteScrollAnchor } from "./InfiniteScrollAnchor";
declare const DotNet: typeof import("@microsoft/dotnet-js-interop").DotNet;

export class InfiniteScroll {
    public scrollHost: Element;
    public sentinel: Element;
    
    public static Create(scrollHostSelector: string, sentinelSelector: string): InfiniteScroll {
        return new InfiniteScroll(document.querySelector(scrollHostSelector), document.querySelector(sentinelSelector));
    }
    
    constructor(scrollHost: Element, sentinel: Element) {
        this.scrollHost = scrollHost;
        this.sentinel = sentinel;
    }
    
    ObserveSentinel() {
        const observer = new IntersectionObserver((entries) => {
            for (const entry of entries) {
                if (entry.isIntersecting) {
                    DotNet.invokeMethodAsync("LANCommander.UI", "OnSentinelVisible");
                }
            }
        }, {
            root: this.scrollHost,
            threshold: 0.01
        });

        observer.observe(this.sentinel);
    }

    CaptureAnchor(): InfiniteScrollAnchor {
        const first = this.scrollHost.querySelector("[data-id]");

        if (!first)
            return null;

        const rect = first.getBoundingClientRect();
        const hostRect = this.scrollHost.getBoundingClientRect();

        const anchor = new InfiniteScrollAnchor();

        anchor.Id = first.getAttribute("data-id");
        anchor.OffsetTop = rect.top - hostRect.top;

        return anchor;
    }

    RestoreAfterPrepend(anchor: InfiniteScrollAnchor) {
        if (!anchor)
            return;

        const el = this.scrollHost.querySelector(`[data-id="${anchor.Id}"]`);

        if (!el)
            return;

        const rect = el.getBoundingClientRect();
        const hostRect = this.scrollHost.getBoundingClientRect();
        const newOffset = rect.top - hostRect.top;

        this.scrollHost.scrollTop += (newOffset - anchor.OffsetTop);
    }
}