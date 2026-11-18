// Chat alerts for the server UI, matching the launcher's: a chime, a desktop notification with the
// sender's avatar, and the unread count on the tab (its title, and the app badge where installed).

const soundUrl = '/static/sounds/chat-message.mp3';
const fallbackIcon = '/favicon.png';
// The browser trims a title, so a prefix on an empty one loses its space
const titlePrefix = /^\(\d+\+?\)\s*/;

let audio;
let lastPlayed = 0;
let unread = 0;
let titleObserver;

/** Whether the user can be asked, has allowed, or has refused desktop notifications */
export function permission() {
    return 'Notification' in window ? Notification.permission : 'unsupported';
}

/** Asks to show desktop notifications; browsers only allow this from a click */
export async function requestPermission() {
    if (!('Notification' in window))
        return 'unsupported';

    return await Notification.requestPermission();
}

/** Whether the user is looking at this tab */
export function isFocused() {
    return document.visibilityState === 'visible' && document.hasFocus();
}

export function playSound() {
    // A burst of messages plays one chime rather than a pile of them
    if (Date.now() - lastPlayed < 1000)
        return;

    lastPlayed = Date.now();

    audio ??= new Audio(soundUrl);
    audio.currentTime = 0;

    // Browsers refuse sound until the page has been interacted with; nothing to do about that
    audio.play().catch(() => { });
}

/**
 * Shows a desktop notification, if allowed; clicking it brings this tab forward and opens the thread.
 * The avatar is checked first, since a user without one would otherwise show a broken image.
 */
export async function notify(title, body, avatarUrl, url, tag) {
    if (permission() !== 'granted')
        return;

    let icon = fallbackIcon;

    try {
        if (avatarUrl && (await fetch(avatarUrl, { method: 'HEAD' })).ok)
            icon = avatarUrl;
    } catch {
        // The fallback will do
    }

    const notification = new Notification(title, { body, icon, tag, silent: true });

    notification.onclick = () => {
        window.focus();
        notification.close();

        if (url && location.pathname + location.search !== url)
            location.assign(url);
    };
}

/** Puts the unread count in front of the tab's title and on the installed app's badge */
export function setUnread(count) {
    unread = count;

    applyTitle();

    if (titleObserver === undefined && document.querySelector('title')) {
        // Pages set their own titles as they're navigated to; the count goes back in front each time
        titleObserver = new MutationObserver(applyTitle);
        titleObserver.observe(document.querySelector('title'), { childList: true, characterData: true, subtree: true });
    }

    if ('setAppBadge' in navigator) {
        const badge = count > 0 ? navigator.setAppBadge(count) : navigator.clearAppBadge();

        badge?.catch?.(() => { });
    }
}

function applyTitle() {
    const bare = document.title.replace(titlePrefix, '') || 'LANCommander';
    const title = unread > 0 ? `(${unread > 99 ? '99+' : unread}) ${bare}` : bare;

    // Setting it to what it is would wake the observer again
    if (document.title !== title)
        document.title = title;
}
