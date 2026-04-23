(function () {
    const base = window.STREETFOOD_API || 'http://localhost:5288';
    const key = window.STREETFOOD_ADMIN_KEY || '';

    async function adminFetch(path, options = {}) {
        const headers = new Headers(options.headers || {});
        headers.set('ngrok-skip-browser-warning', 'true');
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
        getPoiListenStats: (days) => adminFetch('/api/admin/analytics/poi-audio-listen?days=' + encodeURIComponent(days == null ? 365 : days)),
        getHourlyActiveUsers: (days) => adminFetch('/api/admin/analytics/hourly-active-users?days=' + encodeURIComponent(days == null ? 30 : days)),
        getUserAnalysisByVisitHour: (days) => adminFetch('/api/admin/analytics/user-analysis/hourly-visits?days=' + encodeURIComponent(days == null ? 30 : days)),
        getPaths: () => adminFetch('/api/admin/analytics/paths'),
        getPopularPaths: (top) => adminFetch('/api/admin/analytics/popular-paths?top=' + encodeURIComponent(top || 5)),
        getPopularRouteChains: (topRoutes, maxPoisPerRoute) => {
            const tr = encodeURIComponent(topRoutes ?? 5);
            const mp = encodeURIComponent(maxPoisPerRoute ?? 5);
            return adminFetch('/api/admin/analytics/popular-route-chains?topRoutes=' + tr + '&maxPoisPerRoute=' + mp);
        },
        createPoiWithOwner: (body) => adminFetch('/api/admin/poi-with-owner', { method: 'POST', body: JSON.stringify(body) }),
        getAwaitingPois: () => adminFetch('/api/admin/pois/awaiting-script'),
        getPendingScripts: () => adminFetch('/api/admin/script-requests/pending'),
        approveScript: (id) => adminFetch('/api/admin/script-requests/' + id + '/approve', { method: 'POST' }),
        listOwners: (includeHidden) => adminFetch('/api/admin/owners?includeHidden=' + !!includeHidden),
        hideOwner: (userId) => adminFetch('/api/admin/owners/' + userId + '/hide', { method: 'POST' }),
        unhideOwner: (userId) => adminFetch('/api/admin/owners/' + userId + '/unhide', { method: 'POST' }),
        getDashboardSummary: () => adminFetch('/api/admin/dashboard/summary'),
        getOnlineNow: (seconds) => adminFetch('/api/admin/analytics/online-now?seconds=' + encodeURIComponent(seconds == null ? 5 : seconds)),
        getOpsMetrics: () => adminFetch('/api/admin/ops/metrics'),
        getAudioJobQueue: () => adminFetch('/api/admin/ops/jobs/queue'),
        getRecentAudioJobs: (limit) => adminFetch('/api/admin/ops/jobs/recent?limit=' + encodeURIComponent(limit == null ? 20 : limit)),
        getPoiIngressQueue: () => adminFetch('/api/admin/ops/ingress-queue'),
        updatePoiIngressQueue: (body) => adminFetch('/api/admin/ops/ingress-queue', { method: 'POST', body: JSON.stringify(body || {}) }),
        regenerateAudio: (poiId) => adminFetch('/api/admin/poi/' + poiId + '/regenerate-audio', { method: 'POST' })
    };
})();

/** Ghi đè sidebar StreetFood Admin — luôn đủ mục (tránh HTML tĩnh bị cache cũ). */
(function () {
    function injectStreetFoodAdminNav() {
        var ul = document.querySelector('.app-shell .sidebar .nav-menu ul');
        if (!ul) return;
        var path = (window.location.pathname || '').toLowerCase();
        var active = '';
        if (path.indexOf('dashboardpage') !== -1) active = 'dashboard';
        else if (path.indexOf('poilistenstatspage') !== -1) active = 'listen';
        else if (path.indexOf('routeheatmappage') !== -1) active = 'route';
        else if (path.indexOf('createpoiownerpage') !== -1) active = 'create';
        else if (path.indexOf('pendingscriptspage') !== -1) active = 'pending';
        else if (path.indexOf('restaurantownerspage') !== -1) active = 'owners';

        var items = [
            { id: 'dashboard', href: 'dashboardPage.html', icon: 'fa-border-all', label: 'Bảng điều khiển' },
            { id: 'listen', href: 'poiListenStatsPage.html', icon: 'fa-headphones', label: 'Thời lượng nghe' },
            { id: 'route', href: 'routeHeatmapPage.html', icon: 'fa-route', label: 'Tuyến & Heatmap' },
            { id: 'create', href: 'createPoiOwnerPage.html', icon: 'fa-user-plus', label: 'Tạo POI & chủ quán' },
            { id: 'pending', href: 'pendingScriptsPage.html', icon: 'fa-file-signature', label: 'Phê duyệt script' },
            { id: 'owners', href: 'restaurantOwnersPage.html', icon: 'fa-user-slash', label: 'Chủ cửa hàng' }
        ];

        ul.innerHTML = items.map(function (it) {
            var cls = it.id === active ? ' class="active"' : '';
            return '<li' + cls + '><a href="' + it.href + '"><i class="fa-solid ' + it.icon + '"></i> ' + it.label + '</a></li>';
        }).join('');
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', injectStreetFoodAdminNav);
    } else {
        injectStreetFoodAdminNav();
    }
})();


