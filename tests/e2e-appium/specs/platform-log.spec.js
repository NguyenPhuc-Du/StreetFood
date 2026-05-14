/**
 * Smoke: sau khi Appium tạo session, đọc lại capabilities từ driver
 * và ghi log — khớp với Android hay iOS do bạn chọn lúc chạy npm script.
 */
const assert = require('assert');

describe('StreetFood — nhận diện nền tảng session', () => {
  it('in log Android hoặc iOS từ session thật', async () => {
    const caps = browser.capabilities;
    const raw =
      caps.platformName ||
      caps['appium:platformName'];

    const lower = String(raw || '').toLowerCase();
    const label = lower === 'ios' ? 'iOS' : 'Android';
    console.log(`[E2E][test] Thiết bị / session: ${label} (platformName=${raw})`);

    assert.ok(lower === 'android' || lower === 'ios', 'platformName phải là Android hoặc iOS');
  });
});
