window.authHelpers = {
    getStatus: async function () {
        const r = await fetch('/api/auth/status');
        return await r.text();
    },
    postAuth: async function (url, payload) {
        const r = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: payload
        });
        return JSON.stringify({ ok: r.ok, status: r.status, body: await r.text() });
    }
};
