/**
 * Mixed load profile (read + write) for StreetFood API.
 * Usage:
 *   k6 run -e BASE_URL=https://<api-url> -e LOAD_LEVEL=100 tests/nfr/streetfood-mixed-load.js
 */
import http from 'k6/http';
import { sleep } from 'k6';
import {
  defaultThresholds,
  getBaseUrl,
  getHeaders,
  getLoadProfile,
  check2xx,
  getPoiList,
  postListenEvent,
} from './k6-common.js';

const base = getBaseUrl();
const headers = getHeaders();
const profile = getLoadProfile();

export const options = {
  vus: profile.vus,
  stages: profile.stages,
  thresholds: defaultThresholds(),
};

export default function () {
  const health = http.get(`${base}/api/health`, { headers });
  check2xx(health, 'health 2xx');

  const poiList = getPoiList(base, headers);
  check2xx(poiList, 'poi list 2xx');

  const listen = postListenEvent(base, headers, {
    poiId: Number(__ENV.POI_ID || 1),
    durationSeconds: Number(__ENV.DURATION_SECONDS || 10),
    deviceId: `k6-mixed-${__VU}-${Date.now()}`,
  });
  check2xx(listen, 'listen event 2xx');

  sleep(0.3);
}
