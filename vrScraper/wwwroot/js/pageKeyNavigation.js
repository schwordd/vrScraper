function addPageKeyListener(dotNetRef) {
    window._pageKeyHandler = function (e) {
        if (window._vrPlayerOpen) return;
        if (document.activeElement &&
            (document.activeElement.tagName === 'INPUT' ||
             document.activeElement.tagName === 'SELECT' ||
             document.activeElement.tagName === 'TEXTAREA')) {
            return;
        }
        if (e.key === 'ArrowLeft' || e.key === 'a' || e.key === 'A') {
            dotNetRef.invokeMethodAsync('HandleKeyNavigation', -1);
        }
        if (e.key === 'ArrowRight' || e.key === 'd' || e.key === 'D') {
            dotNetRef.invokeMethodAsync('HandleKeyNavigation', 1);
        }
    };
    document.addEventListener('keydown', window._pageKeyHandler);
}

function removePageKeyListener() {
    if (window._pageKeyHandler) {
        document.removeEventListener('keydown', window._pageKeyHandler);
        window._pageKeyHandler = null;
    }
}
