import { check } from 'k6';
import exec from 'k6/execution';
import http from 'k6/http';

/** Chẵn → Android, lẻ → iOS (phân bổ ~50/50 giữa các VU). */
export function platformForVu(vu) {
  const v = Number(vu) || 1;
  return v % 2 === 0 ? 'android' : 'ios';
}

export function clientHeadersForVu(vu) {
  const plat = platformForVu(vu);
  const base = getHeaders();
  const ua =
    plat === 'android'
      ? 'Mozilla/5.0 (Linux; Android 14; StreetFood-k6) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Mobile Safari/537.36'
      : 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Mobile/15E148 Safari/604.1 StreetFood-k6';
  return Object.assign({}, base, {
    'User-Agent': ua,
    'X-StreetFood-Client-Platform': plat,
  });
}

/** In một dòng ra stdout k6: VU nào đang giả lập Android hay iOS (mỗi VU một lần). */
export function logVuPlatformOnce(vu) {
  if (exec.vu.iterationInScenario !== 0) return;
  const plat = platformForVu(vu);
  const label = plat === 'ios' ? 'iOS' : 'Android';
  console.log(
    `[k6] VU ${vu} → ${label} (X-StreetFood-Client-Platform=${plat}; deviceId có tiền tố "${plat}-")`
  );
}

/** deviceId có tiền tố android- / ios- để log phía API/DB dễ lọc. */
export function k6DeviceId(kind, vu, suffix) {
  const plat = platformForVu(vu);
  const tail = suffix != null ? String(suffix) : String(Date.now());
  return `${plat}-k6-${kind}-${vu}-${tail}`.slice(0, 120);
}

export function getBaseUrl() {
  return __ENV.BASE_URL || __ENV.K6_BASE_URL || 'http://127.0.0.1:5191';
}

export function getHeaders() {
  return {
    'Content-Type': 'application/json',
    'ngrok-skip-browser-warning': 'true',
    ...(typeof __ENV.ADMIN_API_KEY === 'string' && __ENV.ADMIN_API_KEY
      ? { 'X-Admin-Key': __ENV.ADMIN_API_KEY }
      : {}),
  };
}

export function getLoadProfile() {
  const level = String(__ENV.LOAD_LEVEL || '50').trim();
  if (level === '100') {
    return {
      vus: 100,
      stages: [
        { duration: '30s', target: 100 },
        { duration: '2m', target: 100 },
        { duration: '20s', target: 0 },
      ],
    };
  }
  if (level === '200') {
    return {
      vus: 200,
      stages: [
        { duration: '45s', target: 200 },
        { duration: '3m', target: 200 },
        { duration: '30s', target: 0 },
      ],
    };
  }
  return {
    vus: 50,
    stages: [
      { duration: '20s', target: 50 },
      { duration: '90s', target: 50 },
      { duration: '20s', target: 0 },
    ],
  };
}

export function defaultThresholds() {
  return {
    http_req_failed: ['rate<0.02'],
    'http_req_duration{expected_response:true}': ['p(95)<800'],
  };
}

export function check2xx(res, name) {
  check(res, { [name]: (r) => r.status >= 200 && r.status < 300 });
}

export function getPoiList(base, headers) {
  return http.get(`${base}/api/Poi`, { headers });
}

export function postListenEvent(base, headers, payload) {
  return http.post(`${base}/api/analytics/poi-audio-listen`, JSON.stringify(payload), { headers });
}
