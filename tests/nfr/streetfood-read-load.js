/**
 * Read-heavy load profile for StreetFood API.
 * Usage:
 *   k6 run -e BASE_URL=https://<api-url> -e LOAD_LEVEL=50 tests/nfr/streetfood-read-load.js
 */
import { sleep } from 'k6';
import {
  clientHeadersForVu,
  defaultThresholds,
  getBaseUrl,
  getLoadProfile,
  check2xx,
  getPoiList,
  logVuPlatformOnce,
} from './k6-common.js';
import http from 'k6/http';

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

  sleep(0.2);
}
