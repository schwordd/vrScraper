function addPageKeyListener(dotNetRef) {
    window._pageKeyHandler = function (e) {
        if (document.activeElement &&
            (document.activeElement.tagName === 'INPUT' ||
             document.activeElement.tagName === 'SELECT' ||
             document.activeElement.tagName === 'TEXTAREA')) {
            return;
        }
        if (e.key === 'ArrowLeft') {
            dotNetRef.invokeMethodAsync('HandleKeyNavigation', -1);
        }
        if (e.key === 'ArrowRight') {
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
