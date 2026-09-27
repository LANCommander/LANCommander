import { InfiniteScrollAnchor } from "./InfiniteScrollAnchor";

export class InfiniteScroll {
    public scrollHost: Element;
    public sentinel: Element;
    public dotnet: any;
    
    public static Create(dotnet: any, scrollHost: Element, sentinel: Element): InfiniteScroll {
        return new InfiniteScroll(dotnet, scrollHost, sentinel);
    }
    
    constructor(dotnet: any, scrollHost: Element, sentinel: Element) {
        this.dotnet = dotnet;
        this.scrollHost = scrollHost;
        this.sentinel = sentinel;
    }
    
    ObserveSentinel() {
        const observer = new IntersectionObserver((entries) => {
            for (const entry of entries) {
                if (entry.isIntersecting) {
                    this.dotnet.invokeMethodAsync("OnSentinelVisible");
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
    
    IsSentinelVisible(): boolean {
        const rect = this.sentinel.getBoundingClientRect();
        const hostRect = this.scrollHost.getBoundingClientRect();
        
        // Check if sentinel is within the visible area of the scroll host
        return rect.top >= hostRect.top && rect.top <= hostRect.bottom;
    }
    
    ScrollToBottom() {
        this.scrollHost.scrollTop = this.scrollHost.scrollHeight;
    }
}