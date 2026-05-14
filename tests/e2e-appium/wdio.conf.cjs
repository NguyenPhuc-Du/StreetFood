const path = require('path');

const platform = (process.env.PLATFORM || 'android').toLowerCase();

/** Đường tới APK / .app — ưu tiên biến môi trường (xem README). */
const androidApp =
  process.env.ANDROID_APP ||
  path.resolve(__dirname, '../../App/bin/Debug/net10.0-android/com.companyname.StreetFood-Signed.apk');

const iosApp =
  process.env.IOS_APP ||
  path.resolve(
    __dirname,
    '../../App/bin/Debug/net10.0-ios/iossimulator-x64/App.app'
  );

const androidCaps = {
  platformName: 'Android',
  'appium:automationName': 'UiAutomator2',
  'appium:app': androidApp,
  'appium:noReset': true,
  'appium:autoGrantPermissions': true,
};

const iosCaps = {
  platformName: 'iOS',
  'appium:automationName': 'XCUITest',
  'appium:deviceName': process.env.IOS_DEVICE_NAME || 'iPhone 16',
  'appium:platformVersion': process.env.IOS_PLATFORM_VERSION || '18.0',
  'appium:app': iosApp,
  'appium:noReset': true,
};

const capabilities = [platform === 'ios' ? iosCaps : androidCaps];

exports.config = {
  runner: 'local',
  specs: ['./specs/**/*.spec.js'],
  maxInstances: 1,
  capabilities,
  logLevel: 'info',
  waitforTimeout: 20000,
  connectionRetryTimeout: 120000,
  connectionRetryCount: 2,
  framework: 'mocha',
  reporters: [['spec', { showPreface: false }]],
  mochaOpts: { ui: 'bdd', timeout: 120000 },

  services: [
    [
      'appium',
      {
        args: {
          relaxedSecurity: true,
        },
        logPath: './logs-appium',
      },
    ],
  ],

  /**
   * Chạy trước suite: in rõ đang Android hay iOS (đây chính là “nhận diện” theo session Appium).
   */
  onPrepare: function () {
    const cap = capabilities[0];
    const name = cap.platformName;
    const label = name === 'iOS' ? 'iOS' : 'Android';
    console.log('\n========================================');
    console.log(`[E2E] PLATFORM=${platform}  →  session: ${label}`);
    console.log(`[E2E] platformName (capabilities)=${name}`);
    if (name === 'Android') {
      console.log(`[E2E] app (APK)=${cap['appium:app']}`);
    } else {
      console.log(`[E2E] app (.app)=${cap['appium:app']}`);
      console.log(
        `[E2E] simulator: ${cap['appium:deviceName']} iOS ${cap['appium:platformVersion']}`
      );
    }
    console.log('========================================\n');
  },
};
