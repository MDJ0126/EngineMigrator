const { chromium } = require('playwright');
const fs = require('node:fs');
const path = require('node:path');
const {pathToFileURL, fileURLToPath} = require('node:url');
(async () => {
  const browser = await chromium.launchPersistentContext(path.resolve('artifacts/mapping-browser'), {channel:'chrome',headless:true});
  try {
    const page = await browser.newPage();
    const errors = []; page.on('pageerror', e => errors.push(e.message));
    await page.goto(pathToFileURL(path.resolve('artifacts/mapping-sample/porting-map.html')).href);
    const total = await page.locator('tbody tr').count();
    if (total < 5) throw new Error('Missing code/resource/meta/settings rows');
    for (const link of await page.locator('a[href]').evaluateAll(a => a.map(x => x.href)))
      if (!fs.existsSync(fileURLToPath(link))) throw new Error('Generated link missing: ' + link);
    await page.locator('#search').fill('Material.mat');
    if (await page.locator('tbody tr:visible').count() !== 2) throw new Error('Resource/meta search mismatch');
    await page.locator('#search').fill('no-match-123');
    if (await page.locator('tbody tr:visible').count() !== 0 || await page.locator('article:visible').count() !== 0) throw new Error('Search filter failure');
    await page.locator('#search').fill('');
    for (const width of [1280,390]) {
      await page.setViewportSize({width,height:900});
      if (await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)) throw new Error('Horizontal page overflow: ' + width);
      await page.screenshot({path:`artifacts/mapping-${width}.png`, fullPage:true});
      console.log(`PASS mapping ${width}px: ${total} mapped rows, output links exist, resource/meta search works, no overflow`);
    }
    const method = page.locator('details').filter({has:page.locator('summary', {hasText:'Damage'})}).first();
    await method.locator('summary').click();
    if (await method.locator('pre:visible').count() !== 2) throw new Error('Source/target snippets did not expand');
    console.log('PASS source/target code disclosure');
    if (errors.length) throw new Error(errors.join('\n'));
  } finally { await browser.close(); }
})().catch(e => {console.error(e.message);process.exitCode=1;});
