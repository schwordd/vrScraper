window.uiStorage = (function () {
    function safeGet(key) {
        try { return localStorage.getItem(key); }
        catch (e) { return null; }
    }
    function safeSet(key, value) {
        try {
            if (value === null || value === undefined || value === "") {
                localStorage.removeItem(key);
            } else {
                localStorage.setItem(key, String(value));
            }
        } catch (e) { /* quota / private mode */ }
    }
    return {
        get: function (key) {
            return safeGet(key) ?? "";
        },
        set: function (key, value) {
            safeSet(key, value);
        },
        getInt: function (key, fallback) {
            var raw = safeGet(key);
            if (raw === null || raw === "") return fallback;
            var n = parseInt(raw, 10);
            return isNaN(n) ? fallback : n;
        },
        getLong: function (key, fallback) {
            var raw = safeGet(key);
            if (raw === null || raw === "") return fallback;
            var n = parseInt(raw, 10);
            return isNaN(n) ? fallback : n;
        },
        remove: function (key) {
            try { localStorage.removeItem(key); } catch (e) { }
        }
    };
})();
