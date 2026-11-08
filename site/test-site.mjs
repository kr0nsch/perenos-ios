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
const report = { page: PAGE, viewports: [], noJs: [], screenshots: [] };
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
    if (result.status !== 0 && !`${result.stdout}${result.stderr}`.includes('not found')) {
      console.error(`${result.stdout}${result.stderr}`.trim());
    }
  } else {
    try { process.kill(-server.pid, 'SIGTERM'); } catch { /* Already stopped. */ }
  }
}

async function waitForServer() {
  const deadline = Date.now() + 30000;
  while (Date.now() < deadline) {
    if (server.exitCode !== null) throw new Error(`Preview server exited:\n${serverOutput}`);
    try {
      const response = await fetch(PAGE);
      if (response.ok) return;
    } catch { /* Vite is still starting. */ }
    await new Promise(resolve => setTimeout(resolve, 200));
  }
  throw new Error(`Preview server did not start:\n${serverOutput}`);
}

async function checkLayout(page, width, height, theme) {
  const overflow = await page.evaluate(() => ({
    viewport: innerWidth,
    document: document.documentElement.scrollWidth,
    body: document.body.scrollWidth,
  }));
  assert.ok(overflow.document <= overflow.viewport + 1, `Horizontal overflow at ${width}x${height}: ${JSON.stringify(overflow)}`);
  assert.ok(overflow.body <= overflow.viewport + 1, `Body overflow at ${width}x${height}: ${JSON.stringify(overflow)}`);
  assert.equal(await page.locator('html').getAttribute('data-theme'), theme);
  assert.equal(await page.locator('#hero-title').isVisible(), true);
  await page.evaluate(() => document.fonts.ready);
  await page.locator('.hero-window img').evaluate(img => img.decode());
}

async function capture(page, name, selector) {
  const path = resolve(screenshots, `${name}.png`);
  if (selector) {
    const locator = page.locator(selector);
    await locator.scrollIntoViewIfNeeded();
    await locator.screenshot({ path });
  } else {
    await page.evaluate(() => scrollTo(0, 0));
    await page.screenshot({ path });
  }
  report.screenshots.push(`artifacts/site-check/${name}.png`);
}

try {
  startServer();
  await waitForServer();
  const edgePaths = [
    'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
    'C:/Program Files/Microsoft/Edge/Application/msedge.exe',
  ];
  const executablePath = edgePaths.find(existsSync);
  if (!executablePath) throw new Error('Microsoft Edge was not found');
  browser = await chromium.launch({ executablePath, headless: true });

  for (const [width, height] of [[375, 812], [390, 844], [768, 1024], [1024, 768], [1440, 900]]) {
    for (const theme of ['light', 'dark']) {
      const context = await browser.newContext({ viewport: { width, height }, colorScheme: theme });
      const page = await context.newPage();
      page.setDefaultTimeout(15000);
      const requests = [];
      const errors = [];
      page.on('request', request => requests.push(request.url()));
      page.on('pageerror', error => errors.push(error.message));
      page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
      await page.route('https://api.github.com/repos/kr0nsch/perenos-ios/releases/latest', route => route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          tag_name: 'v1.5.0', draft: false, prerelease: false,
          assets: [
            { name: 'Perenos-1.5.0-setup-x64.exe', size: 80 * 1048576, browser_download_url: 'https://github.com/kr0nsch/perenos-ios/releases/download/v1.5.0/Perenos-1.5.0-setup-x64.exe' },
            { name: 'Perenos-1.5.0-portable-x64.zip', size: 75 * 1048576, browser_download_url: 'https://github.com/kr0nsch/perenos-ios/releases/download/v1.5.0/Perenos-1.5.0-portable-x64.zip' },
          ],
        }),
      }));
      await page.goto(PAGE, { waitUntil: 'domcontentloaded' });
      await checkLayout(page, width, height, theme);
      assert.equal(requests.filter(url => url.endsWith('.gif')).length, 0, 'GIF loaded before user action');

      await page.locator('#tab-later').click();
      assert.equal(await page.locator('#tab-later').getAttribute('aria-selected'), 'true');
      assert.equal(await page.locator('#panel-later').isVisible(), true);
      assert.equal(await page.locator('#panel-now').isVisible(), false);
      await page.locator('#tab-later').press('ArrowLeft');
      assert.equal(await page.locator('#tab-now').getAttribute('aria-selected'), 'true');
      await page.locator('#tab-now').press('End');
      assert.equal(await page.locator('#tab-later').getAttribute('aria-selected'), 'true');
      await page.locator('#tab-later').press('Home');
      assert.equal(await page.locator('#tab-now').getAttribute('aria-selected'), 'true');

      await page.locator('#play-demo').click();
      assert.equal(await page.locator('#play-demo').getAttribute('aria-pressed'), 'true');
      assert.ok((await page.locator('#demo-image').getAttribute('src')).endsWith(`demo-${theme}.gif`));
      await page.locator('#play-demo').click();
      assert.equal(await page.locator('#play-demo').getAttribute('aria-pressed'), 'false');
      assert.ok((await page.locator('#demo-image').getAttribute('src')).endsWith(`apps-${theme}.png`));

      await page.locator('#play-demo').click();
      await page.locator('#tab-later').click();
      assert.equal(await page.locator('#play-demo').getAttribute('aria-pressed'), 'false');
      assert.ok((await page.locator('#demo-image').getAttribute('src')).endsWith(`saved-${theme}.png`));
      await page.locator('#play-demo').click();
      assert.ok((await page.locator('#demo-image').getAttribute('src')).endsWith(`later-${theme}.gif`));
      await page.locator('#theme-toggle').click();
      const toggled = theme === 'light' ? 'dark' : 'light';
      assert.equal(await page.locator('#play-demo').getAttribute('aria-pressed'), 'false');
      assert.ok((await page.locator('#demo-image').getAttribute('src')).endsWith(`saved-${toggled}.png`));
      assert.ok((await page.locator('[data-gallery="target"]').getAttribute('href')).endsWith(`target-${toggled}.png`));
      await page.reload({ waitUntil: 'domcontentloaded' });
      await checkLayout(page, width, height, toggled);
      assert.equal(await page.evaluate(() => localStorage.getItem('perenos-site-theme')), toggled);

      const faq = page.locator('.faq-items details').first();
      await faq.locator('summary').click();
      assert.equal(await faq.getAttribute('open'), '');
      await faq.locator('summary').click();
      assert.equal(await faq.getAttribute('open'), null);
      await page.waitForFunction(() => document.querySelector('[data-download="installer"]').href.includes('/releases/download/'));
      assert.ok((await page.locator('[data-download="installer"]').getAttribute('href')).endsWith('Perenos-1.5.0-setup-x64.exe'));
      assert.ok((await page.locator('[data-download="portable"]').getAttribute('href')).endsWith('Perenos-1.5.0-portable-x64.zip'));
      assert.match(await page.locator('#release-label').textContent(), /1\.5\.0/);

      if (width === 390 && theme === 'light') {
        assert.match(await page.title(), /Перенос приложений с iPhone на iPhone/);
        assert.equal(await page.locator('link[rel="canonical"]').getAttribute('href'), 'https://kr0nsch.github.io/perenos-ios/');
        const structuredData = await page.locator('script[type="application/ld+json"]').allTextContents();
        const schemas = structuredData.map(text => JSON.parse(text));
        const software = schemas.find(schema => schema['@type'] === 'SoftwareApplication');
        assert.equal(software.softwareVersion, '1.5.0');
        assert.equal(software.offers.price, '0');
        const faqSchema = schemas.find(schema => schema['@type'] === 'FAQPage');
        assert.equal(faqSchema.mainEntity.length, await page.locator('.faq-items details').count());
        const faqNames = await page.locator('.faq-items summary').allTextContents();
        assert.deepEqual(faqSchema.mainEntity.map(question => question.name), faqNames.map(name => name.replace(/\+$/, '').trim()));
        const sitemap = await (await fetch(`${PAGE}sitemap.xml`)).text();
        const robots = await (await fetch(`${PAGE}robots.txt`)).text();
        assert.match(sitemap, /<loc>https:\/\/kr0nsch\.github\.io\/perenos-ios\/<\/loc>/);
        assert.match(robots, /Sitemap: https:\/\/kr0nsch\.github\.io\/perenos-ios\/sitemap\.xml/);
      }

      for (const selector of ['.hero-window img', '.availability-figure img', '.gallery-grid img']) {
        const images = page.locator(selector);
        for (let i = 0; i < await images.count(); i++) {
          await images.nth(i).scrollIntoViewIfNeeded();
          await images.nth(i).evaluate(img => img.decode());
          assert.ok(await images.nth(i).evaluate(img => img.naturalWidth > 0));
        }
      }
      await checkLayout(page, width, height, toggled);
      if (width === 1440) {
        await capture(page, `desktop-${toggled}-hero`);
        await capture(page, `desktop-${toggled}-demo`, '#demo');
        await capture(page, `desktop-${toggled}-restore`, '#restore');
        await capture(page, `desktop-${toggled}-download`, '#download');
      }
      if (width === 390) {
        await capture(page, `mobile-${toggled}-hero`);
        await capture(page, `mobile-${toggled}-restore`, '#restore');
        await capture(page, `mobile-${toggled}-download`, '#download');
      }
      assert.deepEqual(errors, [], `Browser errors at ${width}x${height}/${theme}`);
      report.viewports.push({ width, height, initialTheme: theme, persistedTheme: toggled, errors: errors.length, gifRequestsBeforePlay: 0 });
      await context.close();
      console.log(`PASS ${width}x${height} ${theme} → ${toggled}`);
    }
  }

  for (const theme of ['light', 'dark']) {
    const context = await browser.newContext({ viewport: { width: 390, height: 844 }, colorScheme: theme, javaScriptEnabled: false });
    const page = await context.newPage();
    await page.goto(PAGE);
    assert.equal(await page.locator('#panel-now').isVisible(), true);
    assert.equal(await page.locator('#panel-later').isVisible(), true);
    assert.equal(await page.locator('#theme-toggle').isVisible(), false);
    assert.equal(await page.locator('#play-demo').isVisible(), false);
    assert.equal(await page.locator('.scenario-tabs').isVisible(), false);
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
    report.noJs.push({ theme, bothScenariosVisible: true, controlsHidden: true });
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
