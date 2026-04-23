import { check } from 'k6';
import http from 'k6/http';

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
