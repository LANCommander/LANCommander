// Publishes the height of a docked edge (UI/Shared/PluginDock.razor) as a CSS custom property on
// the root, e.g. --lc-dock-bottom, so the shell and pages that fill the window make room for it.
export function observe(element, property) {
    const root = document.documentElement;
    const update = () => root.style.setProperty(property, `${Math.ceil(element.getBoundingClientRect().height)}px`);
    const observer = new ResizeObserver(update);

    observer.observe(element);
    update();

    return {
        dispose() {
            observer.disconnect();
            root.style.removeProperty(property);
        },
    };
}
