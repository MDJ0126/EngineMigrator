const { chromium } = require('playwright');
const { pathToFileURL } = require('node:url');
const path = require('node:path');
(async () => {
  const browser = await chromium.launchPersistentContext(path.resolve('artifacts/guide-browser'), { channel: 'chrome', headless: true });
  try {
    const page = await browser.newPage();
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    await page.goto(pathToFileURL(path.resolve('docs/guide.html')).href);
    for (const width of [1280, 390]) {
      await page.setViewportSize({ width, height: 900 });
      const check = await page.evaluate(() => ({
        overflow: document.documentElement.scrollWidth > window.innerWidth,
        brokenLinks: [...document.querySelectorAll('nav a')].filter(a => !document.querySelector(a.getAttribute('href'))).length,
        sections: document.querySelectorAll('main section').length
      }));
      if (check.overflow || check.brokenLinks || check.sections !== 7) throw new Error(JSON.stringify(check));
      await page.screenshot({ path: `artifacts/guide-${width}.png`, fullPage: true });
      console.log(`PASS guide ${width}px: 7 sections, anchors valid, no horizontal overflow`);
    }
    if (errors.length) throw new Error(errors.join('\n'));
  } finally { await browser.close(); }
})().catch(e => { console.error(e.message); process.exitCode = 1; });
