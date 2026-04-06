(function () {
    function getCreds() {
        function getCookie(name) {
            const prefix = name + '=';
            const parts = document.cookie ? document.cookie.split('; ') : [];
            for (let i = 0; i < parts.length; i++) {
                const p = parts[i];
                if (p.indexOf(prefix) === 0) return decodeURIComponent(p.substring(prefix.length));
            }
            return '';
        }

        const Username =
            sessionStorage.getItem('vendorUsername') ||
            localStorage.getItem('vendorUsername') ||
            getCookie('vendorUsername');

        const Password =
            sessionStorage.getItem('vendorPassword') ||
            localStorage.getItem('vendorPassword') ||
            getCookie('vendorPassword');
        if (!Username || !Password) throw new Error('Chưa đăng nhập. Vui lòng đăng nhập lại.');
        return { Username, Password };
    }

    async function vendorFetch(path, body) {
        if (typeof window.STREETFOOD_API !== 'string' || !window.STREETFOOD_API) {
            throw new Error('Thiếu cấu hình STREETFOOD_API.');
        }

        const controller = typeof AbortController !== 'undefined' ? new AbortController() : null;
        const timeoutMs = 12000;
        const t = controller
            ? setTimeout(() => controller.abort(), timeoutMs)
            : null;

        let res;
        try {
            res = await fetch(window.STREETFOOD_API + path, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(body || {}),
                signal: controller ? controller.signal : undefined
            });
        } catch (e) {
            const msg =
                e && (e.name === 'AbortError' || String(e).includes('AbortError'))
                    ? `Hết thời gian chờ (${Math.round(timeoutMs / 1000)} giây). Vui lòng kiểm tra API có chạy đúng chưa.`
                    : 'Không thể kết nối API. Vui lòng kiểm tra API có chạy đúng chưa.';
            throw new Error(msg);
        } finally {
            if (t) clearTimeout(t);
        }

        const text = await res.text();
        if (!res.ok) {
            // backend đôi khi trả plain text
            throw new Error(text || res.statusText);
        }

        try {
            return text ? JSON.parse(text) : null;
        } catch {
            return text;
        }
    }

    async function getDefaultPoi() {
        const pois = await vendorFetch('/api/vendor/pois/list', getCreds());
        if (!pois || !pois.length) throw new Error('Không tìm thấy POI cho vendor này.');
        // demo: chọn POI đầu tiên
        window.VendorAPI.currentPoiId = pois[0].poiId ?? pois[0].PoiId ?? pois[0].PoiID ?? pois[0].id;
        return pois[0];
    }

    async function listPois() {
        return await vendorFetch('/api/vendor/pois/list', getCreds());
    }

    async function uploadShopImage(file) {
        const creds = getCreds();
        if (typeof window.STREETFOOD_API !== 'string' || !window.STREETFOOD_API) {
            throw new Error('Thiếu cấu hình STREETFOOD_API.');
        }
        const fd = new FormData();
        fd.append('username', creds.Username);
        fd.append('password', creds.Password);
        fd.append('file', file);

        const controller = typeof AbortController !== 'undefined' ? new AbortController() : null;
        const timeoutMs = 60000;
        const t = controller ? setTimeout(() => controller.abort(), timeoutMs) : null;

        let res;
        try {
            res = await fetch(window.STREETFOOD_API + '/api/vendor/media/upload', {
                method: 'POST',
                body: fd,
                signal: controller ? controller.signal : undefined
            });
        } catch (e) {
            throw new Error(e && (e.name === 'AbortError') ? 'Hết thời gian khi tải ảnh lên.' : 'Không thể kết nối API tải ảnh.');
        } finally {
            if (t) clearTimeout(t);
        }

        const text = await res.text();
        if (!res.ok) throw new Error(text || res.statusText);
        const data = text ? JSON.parse(text) : {};
        const url = data.url || data.Url;
        if (!url) throw new Error('API không trả về URL ảnh.');
        return url;
    }

    async function updateShopDetails(poiId, OpeningHours, Phone, Email, ImageUrl) {
        const creds = getCreds();
        return await vendorFetch('/api/vendor/shop/update-details', {
            ...creds,
            PoiId: poiId,
            ImageUrl: ImageUrl || '',
            OpeningHours: OpeningHours,
            Phone: Phone,
            Email: Email || ''
        });
    }

    async function listFoods(poiId) {
        const creds = getCreds();
        const foods = await vendorFetch('/api/vendor/foods/list', {
            ...creds,
            PoiId: poiId
        });
        return foods || [];
    }

    async function createFood(poiId, Name, Description, Price, ImageUrl) {
        const creds = getCreds();
        return await vendorFetch('/api/vendor/foods/create', {
            ...creds,
            PoiId: poiId,
            Name,
            Description,
            Price: Number(Price),
            ImageUrl: ImageUrl || ''
        });
    }

    async function updateFood(foodId, _poiId, Name, Description, Price, ImageUrl) {
        const creds = getCreds();
        return await vendorFetch('/api/vendor/foods/update', {
            ...creds,
            FoodId: foodId,
            Name,
            Description,
            Price: Number(Price),
            ImageUrl: ImageUrl || ''
        });
    }

    async function deleteFood(foodId) {
        const creds = getCreds();
        return await vendorFetch('/api/vendor/foods/delete', {
            ...creds,
            FoodId: foodId
        });
    }

    async function submitScript(poiId, ScriptText, LanguageCode) {
        const creds = getCreds();
        return await vendorFetch('/api/vendor/submit-script', {
            ...creds,
            PoiId: poiId,
            ScriptText,
            LanguageCode: LanguageCode || 'vi'
        });
    }

    async function submitAudioBundle(poiId, files) {
        const creds = getCreds();
        if (typeof window.STREETFOOD_API !== 'string' || !window.STREETFOOD_API) {
            throw new Error('Thiếu cấu hình STREETFOOD_API.');
        }
        const fd = new FormData();
        fd.append('username', creds.Username);
        fd.append('password', creds.Password);
        fd.append('poiId', String(poiId));
        fd.append('audio_vi', files.vi);
        fd.append('audio_en', files.en);
        fd.append('audio_cn', files.cn);
        fd.append('audio_ja', files.ja);
        fd.append('audio_ko', files.ko);

        const controller = typeof AbortController !== 'undefined' ? new AbortController() : null;
        const timeoutMs = 120000;
        const t = controller ? setTimeout(() => controller.abort(), timeoutMs) : null;

        let res;
        try {
            res = await fetch(window.STREETFOOD_API + '/api/vendor/submit-audio-bundle', {
                method: 'POST',
                body: fd,
                signal: controller ? controller.signal : undefined
            });
        } catch (e) {
            throw new Error(e && (e.name === 'AbortError') ? 'Hết thời gian khi tải file lên.' : 'Không thể kết nối API.');
        } finally {
            if (t) clearTimeout(t);
        }

        const text = await res.text();
        if (!res.ok) throw new Error(text || res.statusText);
        try {
            return text ? JSON.parse(text) : null;
        } catch {
            return text;
        }
    }

    window.VendorAPI = {
        currentPoiId: null,
        listPois,
        getDefaultPoi,
        uploadShopImage,
        updateShopDetails,
        listFoods,
        createFood,
        updateFood,
        deleteFood,
        submitScript,
        submitAudioBundle
    };
})();

