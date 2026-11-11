(() => {
  'use strict';
  const root = document.documentElement;
  const english = root.lang === 'en';
  const assetPrefix = document.querySelector('meta[name="asset-prefix"]')?.content || '';
  const systemTheme = window.matchMedia('(prefers-color-scheme: dark)');
  const themeButton = document.querySelector('#theme-toggle');
  const demoImage = document.querySelector('#demo-image');
  const tabs = Array.from(document.querySelectorAll('[data-scenario]'));
  const scenarios = english ? {
    now: { image: 'apps', caption: 'Choosing apps', alt: 'Choosing apps in Perenos' },
    restore: { image: 'restore', caption: 'Apple Account purchases', alt: 'Restoring purchases in Perenos' },
    later: { image: 'saved', caption: 'Your library', alt: 'Saving apps in the library' },
  } : {
    now: { image: 'apps', caption: 'Выбор приложений', alt: 'Выбор приложений в Переносе' },
    restore: { image: 'restore', caption: 'Покупки Apple Account', alt: 'Восстановление покупок в Переносе' },
    later: { image: 'saved', caption: 'Ваша библиотека', alt: 'Сохранение приложений в библиотеку' },
  };
  let selected = 'now';
  let explicitTheme;
  try { explicitTheme = localStorage.getItem('perenos-site-theme'); } catch { /* Storage is optional. */ }
  if (explicitTheme !== 'light' && explicitTheme !== 'dark') explicitTheme = null;
  const activeTheme = () => explicitTheme || (systemTheme.matches ? 'dark' : 'light');

  function applyTheme() {
    const theme = activeTheme();
    root.dataset.theme = theme;
    document.querySelector('meta[name="theme-color"]').content = theme === 'dark' ? '#171821' : '#f6f5f1';
    document.querySelectorAll('.theme-image').forEach(img => {
      const src = `${assetPrefix}assets/${img.dataset.asset}${english ? '-en' : ''}-${theme}.png`;
      img.src = src;
      img.closest('picture')?.querySelector('source')?.setAttribute('srcset', src);
    });
    const dark = theme === 'dark';
    const label = dark
      ? (english ? 'Switch to light theme' : 'Включить светлую тему')
      : (english ? 'Switch to dark theme' : 'Включить тёмную тему');
    themeButton.setAttribute('aria-label', label);
    themeButton.title = label;
  }

  function selectScenario(tab, moveFocus = false) {
    selected = tab.dataset.scenario;
    const scenario = scenarios[selected];
    tabs.forEach(item => {
      const active = item === tab;
      item.classList.toggle('active', active);
      item.setAttribute('aria-selected', String(active));
      item.tabIndex = active ? 0 : -1;
      document.getElementById(item.getAttribute('aria-controls')).hidden = !active;
    });
    demoImage.dataset.asset = scenario.image;
    demoImage.alt = scenario.alt;
    document.querySelector('#demo-caption').textContent = scenario.caption;
    applyTheme();
    if (moveFocus) tab.focus();
  }

  themeButton.addEventListener('click', () => {
    explicitTheme = activeTheme() === 'dark' ? 'light' : 'dark';
    try { localStorage.setItem('perenos-site-theme', explicitTheme); } catch { /* The visit still works. */ }
    applyTheme();
  });
  systemTheme.addEventListener('change', () => { if (!explicitTheme) applyTheme(); });
  tabs.forEach((tab, index) => {
    tab.addEventListener('click', () => selectScenario(tab));
    tab.addEventListener('keydown', event => {
      let target;
      if (event.key === 'ArrowRight') target = tabs[(index + 1) % tabs.length];
      if (event.key === 'ArrowLeft') target = tabs[(index + tabs.length - 1) % tabs.length];
      if (event.key === 'Home') target = tabs[0];
      if (event.key === 'End') target = tabs[tabs.length - 1];
      if (target) { event.preventDefault(); selectScenario(target, true); }
    });
  });

  async function updateDownloads() {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 5000);
    try {
      const response = await fetch('https://api.github.com/repos/kr0nsch/perenos-ios/releases/latest', {
        headers: { Accept: 'application/vnd.github+json' }, signal: controller.signal,
      });
      if (!response.ok) return;
      const release = await response.json();
      if (release.draft || release.prerelease || !/^v\d+\.\d+\.\d+$/.test(release.tag_name) || !Array.isArray(release.assets)) return;
      const patterns = {
        installer: /^Perenos-\d+\.\d+\.\d+-setup-x64\.exe$/,
        portable: /^Perenos-\d+\.\d+\.\d+-portable-x64\.zip$/,
        'macos-arm64': /^Perenos-\d+\.\d+\.\d+-macos-arm64\.dmg$/,
        'macos-x64': /^Perenos-\d+\.\d+\.\d+-macos-x64\.dmg$/,
      };
      for (const [kind, pattern] of Object.entries(patterns)) {
        const asset = release.assets.find(item => pattern.test(item.name));
        if (!asset || typeof asset.browser_download_url !== 'string') continue;
        const url = new URL(asset.browser_download_url);
        if (url.origin !== 'https://github.com' || !url.pathname.startsWith('/kr0nsch/perenos-ios/releases/download/') || url.username || url.password) continue;
        const link = document.querySelector(`[data-download="${kind}"]`);
        link.href = url.href;
        link.removeAttribute('aria-disabled');
        if (kind.startsWith('macos-')) {
          const size = Number(asset.size);
          const sizeText = Number.isFinite(size) && size > 0 ? ` · ${Math.round(size / 1048576)} MB` : '';
          link.querySelector('[data-status]').textContent = `macOS 15+ · DMG${sizeText}`;
        }
      }
      document.querySelector('#release-label').textContent = english
        ? `Version ${release.tag_name.slice(1)} · Windows and macOS`
        : `Версия ${release.tag_name.slice(1)} · Windows и macOS`;
    } catch { /* Static links and honest Mac beta labels remain available. */ }
    finally { clearTimeout(timeout); }
  }

  applyTheme();
  themeButton.hidden = false;
  root.classList.add('enhanced');
  updateDownloads();
})();
