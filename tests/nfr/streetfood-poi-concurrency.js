/**
 * Concurrent POI session flow load test.
 * Simulates many users around the same POI:
 * - /api/Poi/log
 * - /api/Poi/visit/start
 * - /api/Poi/movement
 * - /api/Poi/visit/end
 *
 * Example:
 *   k6 run -e BASE_URL=https://localhost:7236 -e VUS=20 -e HOLD=90s -e POI_ID=1 tests/nfr/streetfood-poi-concurrency.js
 */
import http from 'k6/http';
import { sleep } from 'k6';
import { check2xx, getBaseUrl, getHeaders } from './k6-common.js';

const base = getBaseUrl();
const headers = getHeaders();

const vus = Number(__ENV.VUS || 20);
const hold = String(__ENV.HOLD || '90s');
const poiId = Number(__ENV.POI_ID || 1);
const nextPoiId = Number(__ENV.NEXT_POI_ID || poiId + 1);

export const options = {
  scenarios: {
    default: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '20s', target: vus },
        { duration: hold, target: vus },
        { duration: '20s', target: 0 },
      ],
      gracefulRampDown: '30s',
      gracefulStop: '30s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.02'],
    'http_req_duration{expected_response:true}': ['p(95)<1000'],
  },
};

function jitterCoord(baseValue, scale = 0.00015) {
  return baseValue + (Math.random() - 0.5) * scale;
}

export default function () {
  const now = Date.now();
  const deviceId = `k6-poi-${__VU}`;

  const lat = Number(__ENV.BASE_LAT || 10.776889);
  const lng = Number(__ENV.BASE_LNG || 106.700806);

  const logRes = http.post(
    `${base}/api/Poi/log`,
    JSON.stringify({
      deviceId,
      latitude: jitterCoord(lat),
      longitude: jitterCoord(lng),
    }),
    { headers }
  );
  check2xx(logRes, 'poi log 2xx');

  const startRes = http.post(
    `${base}/api/Poi/visit/start`,
    JSON.stringify({
      deviceId,
      poiId,
      atUtc: new Date(now).toISOString(),
    }),
    { headers }
  );
  check2xx(startRes, 'visit start 2xx');

  const moveRes = http.post(
    `${base}/api/Poi/movement`,
    JSON.stringify({
      deviceId,
      fromPoiId: poiId,
      toPoiId: nextPoiId,
      atUtc: new Date(now + 1000).toISOString(),
    }),
    { headers }
  );
  check2xx(moveRes, 'movement 2xx');

  const endRes = http.post(
    `${base}/api/Poi/visit/end`,
    JSON.stringify({
      deviceId,
      poiId,
      atUtc: new Date(now + 3000).toISOString(),
    }),
    { headers }
  );
  check2xx(endRes, 'visit end 2xx');

  sleep(0.5);
}
