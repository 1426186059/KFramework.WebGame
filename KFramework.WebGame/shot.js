const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({
    args: [
      '--use-gl=angle', '--use-angle=swiftshader',
      '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist',
      '--no-sandbox'
    ]
  });
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  const logs = [];
  page.on('console', m => logs.push('[' + m.type() + '] ' + m.text()));
  page.on('pageerror', e => logs.push('[pageerror] ' + e.message));

  try {
    await page.goto('http://localhost:54041', { waitUntil: 'load', timeout: 60000 });
  } catch (e) { logs.push('[goto-error] ' + e.message); }

  // wait for wasm + scene
  await page.waitForTimeout(25000);

  await page.screenshot({ path: 'shot_full.png' });

  // try a few right-half crops to inspect sprite regions
  const canvas = await page.$('canvas');
  let box = null;
  if (canvas) {
    box = await canvas.boundingBox();
    logs.push('[canvas] ' + JSON.stringify(box));
  }
  console.log(logs.join('\n'));
  await browser.close();
})();
