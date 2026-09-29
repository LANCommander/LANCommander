export class DomHelper
{
    public static Create(): DomHelper
    {
        return new DomHelper();
    }

    public ClickElement(id: string): void
    {
        document.getElementById(id)?.click();
    }

    /** Starts a download of url, leaving the page where it is. */
    public Download(url: string): void
    {
        const link = document.createElement("a");

        link.href = url;
        link.download = "";
        link.style.display = "none";

        document.body.appendChild(link);
        link.click();
        link.remove();
    }

    /**
     * Focuses the input inside box when "/" is pressed anywhere but a text field, like the search
     * on most sites. With several boxes on a page the first visible one takes it. The listener
     * removes itself once box has left the page, so nothing needs unbinding.
     */
    public BindSearchShortcut(box: HTMLElement): void
    {
        if (!box)
            return;

        const handler = (event: KeyboardEvent) =>
        {
            if (!box.isConnected)
            {
                document.removeEventListener("keydown", handler);
                return;
            }

            if (event.key !== "/" || event.ctrlKey || event.metaKey || event.altKey || event.defaultPrevented)
                return;

            const target = event.target as HTMLElement | null;

            if (target && (target.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName)))
                return;

            // Hidden, or behind an open dialog
            if (box.offsetParent === null || document.querySelector(".rz-dialog-wrapper"))
                return;

            const input = box.querySelector("input");

            if (!input)
                return;

            event.preventDefault();
            input.focus();
            input.select();
        };

        document.addEventListener("keydown", handler);
    }

    /**
     * Keeps scroller at its end while output is appended to it, as long as the reader is there:
     * scrolling up pauses it and scrolling back to the end resumes it. Each change is reported to
     * owner's [JSInvokable] FollowChanged(bool). The returned handle's Resume() jumps back to the
     * end; Dispose() unbinds.
     */
    public FollowOutput(scroller: HTMLElement, owner: any): { Resume: () => void, Dispose: () => void }
    {
        let following = true;

        const atEnd = () => scroller.scrollHeight - scroller.scrollTop - scroller.clientHeight < 8;
        const toEnd = () => { scroller.scrollTop = scroller.scrollHeight; };

        const setFollowing = (value: boolean) =>
        {
            if (value === following)
                return;

            following = value;
            owner?.invokeMethodAsync("FollowChanged", value);
        };

        // Mutation callbacks run before the next scroll event, so an append never reads as the
        // reader scrolling away
        const observer = new MutationObserver(() =>
        {
            if (following)
                toEnd();
        });

        const onScroll = () => setFollowing(atEnd());

        observer.observe(scroller, { childList: true, subtree: true, characterData: true });
        scroller.addEventListener("scroll", onScroll, { passive: true });

        toEnd();

        return {
            Resume: () =>
            {
                toEnd();
                setFollowing(true);
            },
            Dispose: () =>
            {
                observer.disconnect();
                scroller.removeEventListener("scroll", onScroll);
            }
        };
    }
}
