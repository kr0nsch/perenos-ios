(() => {
  'use strict';
  const root = document.documentElement;
  const systemTheme = window.matchMedia('(prefers-color-scheme: dark)');
  const themeButton = document.querySelector('#theme-toggle');
  const demoImage = document.querySelector('#demo-image');
  const demoPicture = document.querySelector('#demo-picture');
  const playButton = document.querySelector('#play-demo');
  const tabs = Array.from(document.querySelectorAll('[data-scenario]'));
  let scenario = 'now';
  let playing = false;
  let explicitTheme;
  try { explicitTheme = localStorage.getItem('perenos-site-theme'); } catch { /* Storage may be disabled. */ }
  if (explicitTheme !== 'light' && explicitTheme !== 'dark') explicitTheme = null;
  const activeTheme = () => explicitTheme || (systemTheme.matches ? 'dark' : 'light');

  function updateDemo() {
    const theme = activeTheme();
    const asset = scenario === 'now' ? 'apps' : 'saved';
    const source = playing ? `assets/${scenario === 'now' ? 'demo' : 'later'}-${theme}.gif` : `assets/${asset}-${theme}.png`;
    demoImage.dataset.asset = asset;
    demoPicture.querySelector('source').srcset = source;
    demoImage.src = source;
    demoImage.alt = playing
      ? `Демонстрация ${scenario === 'now' ? 'переноса приложений' : 'установки сохранённых приложений'} в Переносе 1.3.0`
      : (scenario === 'now' ? 'Выбор приложений в Переносе 1.3.0' : 'Библиотека сохранённых приложений в Переносе 1.3.0');
    playButton.querySelector('span').textContent = playing ? 'Остановить демо' : 'Воспроизвести демо';
    playButton.setAttribute('aria-pressed', String(playing));
    playButton.querySelector('path').setAttribute('d', playing ? 'M6 5h4v14H6Zm8 0h4v14h-4Z' : 'm9 5 11 7-11 7Z');
    document.querySelector('#demo-caption').textContent = `${scenario === 'now' ? 'Перенос сейчас' : 'Установка сохранённого'} · версия 1.3.0`;
  }

  function applyTheme() {
    playing = false;
    const theme = activeTheme();
    root.dataset.theme = theme;
    document.querySelector('meta[name="theme-color"]').content = theme === 'dark' ? '#111827' : '#f7f9fc';
    document.querySelectorAll('.theme-image').forEach(img => {
      if (img === demoImage) return;
      const src = `assets/${img.dataset.asset}-${theme}.png`;
      img.src = src;
      img.closest('picture')?.querySelector('source')?.setAttribute('srcset', src);
    });
    document.querySelectorAll('[data-gallery]').forEach(link => { link.href = `assets/${link.dataset.gallery}-${theme}.png`; });
    themeButton.setAttribute('aria-label', theme === 'dark' ? 'Включить светлую тему' : 'Включить тёмную тему');
    themeButton.title = theme === 'dark' ? 'Светлая тема' : 'Тёмная тема';
    updateDemo();
  }

  function selectScenario(tab, moveFocus = false) {
    scenario = tab.dataset.scenario;
    playing = false;
    tabs.forEach(item => {
      const selected = item === tab;
      item.classList.toggle('active', selected);
      item.setAttribute('aria-selected', String(selected));
      item.tabIndex = selected ? 0 : -1;
      document.getElementById(item.getAttribute('aria-controls')).hidden = !selected;
    });
    updateDemo();
    if (moveFocus) tab.focus();
  }

  themeButton.addEventListener('click', () => {
    explicitTheme = activeTheme() === 'dark' ? 'light' : 'dark';
    try { localStorage.setItem('perenos-site-theme', explicitTheme); } catch { /* Theme still works for this visit. */ }
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
  playButton.addEventListener('click', () => { playing = !playing; updateDemo(); });
  applyTheme();
  root.classList.add('enhanced');
  themeButton.hidden = false;
  playButton.hidden = false;

  // Keep safe release-page fallbacks when GitHub's API is unavailable.
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
      const patterns = { installer: /^Perenos-\d+\.\d+\.\d+-setup-x64\.exe$/, portable: /^Perenos-\d+\.\d+\.\d+-portable-x64\.zip$/ };
      for (const [kind, pattern] of Object.entries(patterns)) {
        const asset = release.assets.find(item => pattern.test(item.name));
        if (!asset || typeof asset.browser_download_url !== 'string') continue;
        const url = new URL(asset.browser_download_url);
        if (url.origin !== 'https://github.com' || !url.pathname.startsWith('/kr0nsch/perenos-ios/releases/download/') || url.username || url.password) continue;
        const link = document.querySelector(`[data-download="${kind}"]`);
        link.href = url.href;
        const size = Number(asset.size);
        if (Number.isFinite(size) && size > 0) link.querySelector('small').textContent += ` · ${Math.round(size / 1048576)} МБ`;
      }
      document.querySelector('#release-label').textContent = `Актуальная версия: ${release.tag_name.slice(1)} · Windows x64`;
    } catch { /* Static release links remain usable. */ }
    finally { clearTimeout(timeout); }
  }
  updateDownloads();
})();
