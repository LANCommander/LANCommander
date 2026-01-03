import {InfiniteScrollAnchor} from "./InfiniteScrollAnchor";
declare const DotNet: typeof import("@microsoft/dotnet-js-interop").DotNet;

export function ObserveSentinel(scrollHost: Element, sentinel: Element) {
    const observer = new IntersectionObserver((entries) => {
        for (const entry of entries) {
            if (entry.isIntersecting) {
                DotNet.invokeMethodAsync("LANCommander.UI", "OnSentinelVisible");
            }
        }
    }, {
        root: scrollHost,
        threshold: 0.01
    });
    
    observer.observe(sentinel);
}

export function CaptureAnchor(scrollHost: Element): InfiniteScrollAnchor {
    const first = scrollHost.querySelector("[data-id]");
    
    if (!first)
        return null;
    
    const rect = first.getBoundingClientRect();
    const hostRect = scrollHost.getBoundingClientRect();
    
    const anchor = new InfiniteScrollAnchor();
    
    anchor.Id = first.getAttribute("data-id");
    anchor.OffsetTop = rect.top - hostRect.top;
    
    return anchor;
}

export function RestoreAfterPrepend(scrollHost: Element, anchor: InfiniteScrollAnchor) {
    if (!anchor)
        return;
    
    const el = scrollHost.querySelector(`[data-id="${anchor.Id}"]`);
    
    if (!el)
        return;
    
    const rect = el.getBoundingClientRect();
    const hostRect = scrollHost.getBoundingClientRect();
    const newOffset = rect.top - hostRect.top;
    
    scrollHost.scrollTop += (newOffset - anchor.OffsetTop);
}