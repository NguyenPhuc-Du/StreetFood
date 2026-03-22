(function () {
    const base = window.STREETFOOD_API || 'http://localhost:5288';
    const key = window.STREETFOOD_ADMIN_KEY || '';

    async function adminFetch(path, options = {}) {
        const headers = new Headers(options.headers || {});
        if (key) headers.set('X-Admin-Key', key);
        if (!headers.has('Content-Type') && options.body && typeof options.body === 'string')
            headers.set('Content-Type', 'application/json');
        const res = await fetch(base + path, { ...options, headers });
        const text = await res.text();
        let data;
        try { data = text ? JSON.parse(text) : null; } catch { data = text; }
        if (!res.ok) {
            const msg = typeof data === 'string' ? data : (data?.message || JSON.stringify(data));
            throw new Error(msg || res.statusText);
        }
        return data;
    }

    window.StreetFoodAdmin = {
        baseUrl: base,
        getHeatmap: () => adminFetch('/api/admin/analytics/heatmap'),
        getPaths: () => adminFetch('/api/admin/analytics/paths'),
        createPoiWithOwner: (body) => adminFetch('/api/admin/poi-with-owner', { method: 'POST', body: JSON.stringify(body) }),
        getAwaitingPois: () => adminFetch('/api/admin/pois/awaiting-script'),
        getPendingScripts: () => adminFetch('/api/admin/script-requests/pending'),
        approveScript: (id) => adminFetch('/api/admin/script-requests/' + id + '/approve', { method: 'POST' }),
        listOwners: (includeHidden) => adminFetch('/api/admin/owners?includeHidden=' + !!includeHidden),
        hideOwner: (userId) => adminFetch('/api/admin/owners/' + userId + '/hide', { method: 'POST' }),
        getDashboardSummary: () => adminFetch('/api/admin/dashboard/summary'),
        regenerateAudio: (poiId) => adminFetch('/api/admin/poi/' + poiId + '/regenerate-audio', { method: 'POST' })
    };
})();
