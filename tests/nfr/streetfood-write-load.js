/**
 * Write-heavy load profile for telemetry endpoint.
 * Usage:
 *   k6 run -e BASE_URL=https://<api-url> -e LOAD_LEVEL=50 tests/nfr/streetfood-write-load.js
 */
import { sleep } from 'k6';
import { defaultThresholds, getBaseUrl, getHeaders, getLoadProfile, check2xx, postListenEvent } from './k6-common.js';

const base = getBaseUrl();
const headers = getHeaders();
const profile = getLoadProfile();

export const options = {
  vus: profile.vus,
  stages: profile.stages,
  thresholds: defaultThresholds(),
};

export default function () {
  const t = Date.now();
  const payload = {
    poiId: Number(__ENV.POI_ID || 1),
    durationSeconds: Number(__ENV.DURATION_SECONDS || 15),
    deviceId: `k6-device-${__VU}-${t}`,
  };

  const listen = postListenEvent(base, headers, payload);
  check2xx(listen, 'listen event 2xx');

  sleep(0.2);
}
