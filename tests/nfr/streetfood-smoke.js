/**
 * k6: smoke + tải nhẹ theo PRD §10.1 (NFR).
 * Cài: https://k6.io/docs/get-started/installation/
 * Chạy:  k6 run -e BASE_URL=https://ngrok-url/ tests/nfr/streetfood-smoke.js
 * (Có thể tăng VUs/ duration ngoài dòng lệnh.)
 */
import http from 'k6/http';
import { check, sleep } from 'k6';

const base = __ENV.BASE_URL || __ENV.K6_BASE_URL || 'http://127.0.0.1:5288';

export const options = {
  vus: 20,
  duration: '30s',
  thresholds: {
    http_req_failed: ['rate<0.02'],
    'http_req_duration{expected_response:true}': ['p(95)<800'],
  },
};

const h = { 'ngrok-skip-browser-warning': 'true' };

export default function () {
  const r1 = http.get(`${base}/api/health`, { headers: h });
  check(r1, { 'health 200': (r) => r.status === 200 });
  const r2 = http.get(`${base}/api/Poi`, { headers: h });
  check(r2, { 'poi list 2xx': (r) => r.status >= 200 && r.status < 300 });
  sleep(0.1);
}
