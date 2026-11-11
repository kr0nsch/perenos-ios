import assert from 'node:assert/strict';
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { spawn, spawnSync } from 'node:child_process';
import { chromium } from 'playwright';

const ROOT = resolve(import.meta.dirname, '..');
const PORT = 7100;
const ORIGIN = `http://127.0.0.1:${PORT}`;
const PAGE = `${ORIGIN}/perenos-ios/`;
const screenshots = resolve(ROOT, 'artifacts/site-check');
mkdirSync(screenshots, { recursive: true });
const report = { pages: [PAGE, `${PAGE}en/`], viewports: [], noJs: [], screenshots: [] };
let server;
let serverOutput = '';
let browser;

function startServer() {
  const command = process.platform === 'win32' ? (process.env.ComSpec || 'cmd.exe') : 'npm';
  const args = process.platform === 'win32'
    ? ['/d', '/s', '/c', `npm run dev -- --host 127.0.0.1 --port ${PORT} --strictPort`]
    : ['run', 'dev', '--', '--host', '127.0.0.1', '--port', String(PORT), '--strictPort'];
  server = spawn(command, args, { cwd: ROOT, stdio: ['ignore', 'pipe', 'pipe'], detached: process.platform !== 'win32' });
  server.stdout.on('data', chunk => { serverOutput += chunk; });
  server.stderr.on('data', chunk => { serverOutput += chunk; });
}

function stopServer() {
  if (!server?.pid) return;
  if (process.platform === 'win32') {
    const result = spawnSync('taskkill', ['/pid', String(server.pid), '/t', '/f'], { encoding: 'utf8' });
    if (result.status !== 0 && !`${result.stdout}${result.stderr}`.includes('not found')) console.error(`${result.stdout}${result.stderr}`.trim());
  } else {
    try { process.kill(-server.pid, 'SIGTERM'); } catch { /* Already stopped. */ }
  }
}

async function waitForServer() {
  const deadline = Date.now() + 30000;
  while (Date.now() < deadline) {
    if (server.exitCode !== null) throw new Error(`Preview server exited:\n${serverOutput}`);
    try { if ((await fetch(PAGE)).ok) return; } catch { /* Vite is starting. */ }
    await new Promise(resolve => setTimeout(resolve, 200));
  }
  throw new Error(`Preview server did not start:\n${serverOutput}`);
}

async function checkLayout(page, width, theme) {
  const overflow = await page.evaluate(() => ({ viewport: innerWidth, document: document.documentElement.scrollWidth, body: document.body.scrollWidth }));
  assert.ok(overflow.document <= overflow.viewport + 1, `Horizontal overflow at ${width}: ${JSON.stringify(overflow)}`);
  assert.ok(overflow.body <= overflow.viewport + 1, `Body overflow at ${width}: ${JSON.stringify(overflow)}`);
  assert.equal(await page.locator('html').getAttribute('data-theme'), theme);
  assert.equal(await page.locator('#hero-title').isVisible(), true);
  await page.evaluate(() => document.fonts.ready);
  await page.locator('.hero-visual img').evaluate(img => img.decode());
}

async function capture(page, name, selector) {
  const path = resolve(screenshots, `${name}.png`);
  if (selector) {
    const locator = page.locator(selector);
    await locator.scrollIntoViewIfNeeded();
    await locator.screenshot({ path });
  } else {
    await page.evaluate(() => scrollTo(0, 0));
    await page.screenshot({ path, fullPage: true });
  }
  report.screenshots.push(`artifacts/site-check/${name}.png`);
}

try {
  startServer();
  await waitForServer();
  const executablePath = [
    'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
    'C:/Program Files/Microsoft/Edge/Application/msedge.exe',
  ].find(existsSync);
  if (!executablePath) throw new Error('Microsoft Edge was not found');
  browser = await chromium.launch({ executablePath, headless: true });

  for (const [width, height] of [[375, 812], [390, 844], [768, 1024], [1024, 768], [1440, 900]]) {
    for (const theme of ['light', 'dark']) {
      const context = await browser.newContext({ viewport: { width, height }, colorScheme: theme });
      const page = await context.newPage();
      page.setDefaultTimeout(15000);
      const errors = [];
      page.on('pageerror', error => errors.push(error.message));
      page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
      await page.route('https://api.github.com/repos/kr0nsch/perenos-ios/releases/latest', route => route.fulfill({
        status: 200, contentType: 'application/json', body: JSON.stringify({
          tag_name: 'v1.7.0', draft: false, prerelease: false, assets: [
            { name: 'Perenos-1.7.0-setup-x64.exe', size: 50 * 1048576, browser_download_url: 'https://github.com/kr0nsch/perenos-ios/releases/download/v1.7.0/Perenos-1.7.0-setup-x64.exe' },
            { name: 'Perenos-1.7.0-portable-x64.zip', size: 45 * 1048576, browser_download_url: 'https://github.com/kr0nsch/perenos-ios/releases/download/v1.7.0/Perenos-1.7.0-portable-x64.zip' },
            { name: 'Perenos-1.7.0-macos-arm64.dmg', size: 60 * 1048576, browser_download_url: 'https://github.com/kr0nsch/perenos-ios/releases/download/v1.7.0/Perenos-1.7.0-macos-arm64.dmg' },
            { name: 'Perenos-1.7.0-macos-x64.dmg', size: 61 * 1048576, browser_download_url: 'https://github.com/kr0nsch/perenos-ios/releases/download/v1.7.0/Perenos-1.7.0-macos-x64.dmg' },
          ],
        }),
      }));
      await page.goto(PAGE, { waitUntil: 'domcontentloaded' });
      await checkLayout(page, width, theme);
      assert.equal(await page.locator('#tab-restore').count(), 1);
      await page.locator('#tab-restore').click();
      assert.equal(await page.locator('#panel-restore').isVisible(), true);
      assert.equal(await page.locator('#panel-now').isVisible(), false);
      assert.ok((await page.locator('#demo-image').getAttribute('src')).endsWith(`restore-${theme}.png`));
      await page.locator('#tab-restore').press('ArrowRight');
      assert.equal(await page.locator('#tab-later').getAttribute('aria-selected'), 'true');
      assert.ok((await page.locator('#demo-image').getAttribute('src')).endsWith(`saved-${theme}.png`));
      await page.locator('#tab-later').press('Home');
      assert.equal(await page.locator('#tab-now').getAttribute('aria-selected'), 'true');
      await page.locator('#tab-now').press('End');
      assert.equal(await page.locator('#tab-later').getAttribute('aria-selected'), 'true');
      await page.locator('#theme-toggle').click();
      const toggled = theme === 'light' ? 'dark' : 'light';
      assert.ok((await page.locator('#demo-image').getAttribute('src')).endsWith(`saved-${toggled}.png`));
      await page.reload({ waitUntil: 'domcontentloaded' });
      await checkLayout(page, width, toggled);
      assert.equal(await page.evaluate(() => localStorage.getItem('perenos-site-theme')), toggled);

      const faq = page.locator('.faq-items details').first();
      await faq.locator('summary').click();
      assert.equal(await faq.getAttribute('open'), '');
      await faq.locator('summary').click();
      assert.equal(await faq.getAttribute('open'), null);
      await page.waitForFunction(() => document.querySelector('[data-download="macos-arm64"]').href.includes('/releases/download/'));
      assert.ok((await page.locator('[data-download="installer"]').getAttribute('href')).endsWith('Perenos-1.7.0-setup-x64.exe'));
      assert.ok((await page.locator('[data-download="portable"]').getAttribute('href')).endsWith('Perenos-1.7.0-portable-x64.zip'));
      assert.ok((await page.locator('[data-download="macos-arm64"]').getAttribute('href')).endsWith('Perenos-1.7.0-macos-arm64.dmg'));
      assert.ok((await page.locator('[data-download="macos-x64"]').getAttribute('href')).endsWith('Perenos-1.7.0-macos-x64.dmg'));
      assert.equal(await page.locator('[data-download="macos-arm64"]').getAttribute('aria-disabled'), null);
      assert.match(await page.locator('#release-label').textContent(), /1\.7\.0/);
      for (const image of await page.locator('img.theme-image').all()) {
        await image.scrollIntoViewIfNeeded();
        await image.evaluate(img => img.decode());
        assert.ok(await image.evaluate(img => img.naturalWidth > 0));
      }
      await checkLayout(page, width, toggled);
      if (width === 1440) {
        await capture(page, `desktop-${toggled}-hero`);
        await capture(page, `desktop-${toggled}-showcase`, '.showcase');
        await capture(page, `desktop-${toggled}-restore`, '#restore');
        await capture(page, `desktop-${toggled}-download`, '#download');
      }
      if (width === 390) {
        await capture(page, `mobile-${toggled}-hero`);
        await capture(page, `mobile-${toggled}-restore`, '#restore');
        await capture(page, `mobile-${toggled}-download`, '#download');
      }
      assert.deepEqual(errors, [], `Browser errors at ${width}x${height}/${theme}`);
      report.viewports.push({ width, height, initialTheme: theme, persistedTheme: toggled, errors: errors.length });
      await context.close();
      console.log(`PASS ${width}x${height} ${theme} → ${toggled}`);
    }
  }

  for (const language of ['en', 'ru']) {
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, colorScheme: 'light' });
    const page = await context.newPage();
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(language === 'en' ? PAGE : `${PAGE}en/`);
    await page.locator('.language').click();
    assert.equal(page.url(), language === 'en' ? `${PAGE}en/` : PAGE);
    const expectedLang = language === 'en' ? 'en' : 'ru';
    assert.equal(await page.locator('html').getAttribute('lang'), expectedLang);
    const canonical = await page.locator('link[rel="canonical"]').getAttribute('href');
    assert.equal(canonical, expectedLang === 'en' ? 'https://kr0nsch.github.io/perenos-ios/en/' : 'https://kr0nsch.github.io/perenos-ios/');
    await page.locator('#tab-restore').click();
    assert.ok((await page.locator('#demo-image').getAttribute('src')).endsWith(expectedLang === 'en' ? 'restore-en-light.png' : 'restore-light.png'));
    assert.match(await page.locator('#demo-caption').textContent(), expectedLang === 'en' ? /Apple Account purchases/ : /Покупки Apple Account/);
    for (const image of await page.locator('img.theme-image').all()) {
      await image.scrollIntoViewIfNeeded();
      await image.evaluate(img => img.decode());
      assert.ok(await image.evaluate(img => img.naturalWidth > 0));
    }
    const schemas = (await page.locator('script[type="application/ld+json"]').allTextContents()).map(text => JSON.parse(text));
    const software = schemas.find(schema => schema['@type'] === 'SoftwareApplication');
    assert.equal(software.softwareVersion, '1.7.0');
    assert.equal(software.offers.price, '0');
    const faqSchema = schemas.find(schema => schema['@type'] === 'FAQPage');
    assert.equal(faqSchema.mainEntity.length, await page.locator('.faq-items details').count());
    const names = await page.locator('.faq-items summary').allTextContents();
    assert.deepEqual(faqSchema.mainEntity.map(question => question.name), names.map(name => name.replace(/\+$/, '').trim()));
    assert.ok((await (await fetch(`${PAGE}sitemap.xml`)).text()).includes('https://kr0nsch.github.io/perenos-ios/en/</loc>'));
    assert.match(await (await fetch(`${PAGE}robots.txt`)).text(), /Sitemap: https:\/\/kr0nsch\.github\.io\/perenos-ios\/sitemap\.xml/);
    assert.deepEqual(errors, []);
    if (expectedLang === 'en') await capture(page, 'desktop-en-hero');
    await context.close();
    console.log(`PASS language ${expectedLang} and localized images`);
  }

  for (const theme of ['light', 'dark']) {
    const context = await browser.newContext({ viewport: { width: 390, height: 844 }, colorScheme: theme, javaScriptEnabled: false });
    const page = await context.newPage();
    await page.goto(PAGE);
    assert.equal(await page.locator('#panel-now').isVisible(), true);
    assert.equal(await page.locator('#panel-restore').isVisible(), true);
    assert.equal(await page.locator('#panel-later').isVisible(), true);
    assert.equal(await page.locator('#theme-toggle').isVisible(), false);
    assert.equal(await page.locator('.scenario-tabs').isVisible(), false);
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
    report.noJs.push({ theme, allScenariosVisible: true, controlsHidden: true });
    await context.close();
    console.log(`PASS no-JS ${theme}`);
  }
  report.status = 'passed';
} catch (error) {
  report.status = 'failed';
  report.error = error.stack;
  console.error(error);
  process.exitCode = 1;
} finally {
  if (browser) await browser.close();
  stopServer();
  writeFileSync(resolve(ROOT, 'artifacts/site-test-results.json'), JSON.stringify(report, null, 2) + '\n');
}
