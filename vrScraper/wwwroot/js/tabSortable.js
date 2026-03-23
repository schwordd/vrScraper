window.tabSortable = {
    init: function (containerId, dotNetRef) {
        const el = document.getElementById(containerId);
        if (!el) return;
        if (el._sortable) el._sortable.destroy();
        el._sortable = new Sortable(el, {
            handle: '.drag-handle',
            animation: 150,
            ghostClass: 'sortable-ghost',
            onEnd: function (evt) {
                const ids = Array.from(el.children).map(c => parseInt(c.dataset.tabId));
                dotNetRef.invokeMethodAsync('OnTabOrderChanged', ids);
            }
        });
    },
    destroy: function (containerId) {
        const el = document.getElementById(containerId);
        if (el && el._sortable) {
            el._sortable.destroy();
            el._sortable = null;
        }
    }
};
