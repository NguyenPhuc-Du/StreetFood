/**
 * Mixed load profile (read + write) for StreetFood API.
 * Usage:
 *   k6 run -e BASE_URL=https://<api-url> -e LOAD_LEVEL=100 tests/nfr/streetfood-mixed-load.js
 */
import http from 'k6/http';
import { sleep } from 'k6';
import {
  clientHeadersForVu,
  defaultThresholds,
  getBaseUrl,
  getLoadProfile,
  check2xx,
  getPoiList,
  postListenEvent,
  k6DeviceId,
  logVuPlatformOnce,
} from './k6-common.js';

const base = getBaseUrl();
const profile = getLoadProfile();

export const options = {
  vus: profile.vus,
  stages: profile.stages,
  thresholds: defaultThresholds(),
};

export default function () {
  logVuPlatformOnce(__VU);
  const h = clientHeadersForVu(__VU);
  const health = http.get(`${base}/api/health`, { headers: h });
  check2xx(health, 'health 2xx');

  const poiList = getPoiList(base, h);
  check2xx(poiList, 'poi list 2xx');

  const listen = postListenEvent(base, h, {
    poiId: Number(__ENV.POI_ID || 1),
    durationSeconds: Number(__ENV.DURATION_SECONDS || 10),
    deviceId: k6DeviceId('mixed', __VU, Date.now()),
  });
  check2xx(listen, 'listen event 2xx');

  sleep(0.3);
}
