(() => {
  'use strict';
  const root = document.documentElement;
  const english = root.lang === 'en';
  const assetPrefix = document.querySelector('meta[name="asset-prefix"]')?.content || '';
  const systemTheme = window.matchMedia('(prefers-color-scheme: dark)');
  const motionPreference = window.matchMedia('(prefers-reduced-motion: reduce)');
  const themeButton = document.querySelector('#theme-toggle');
  const demoImage = document.querySelector('#demo-image');
  const demoPicture = document.querySelector('#demo-picture');
  const demoAnimation = document.querySelector('#demo-animation');
  const motionToggle = document.querySelector('#motion-toggle');
  const motionIcon = document.querySelector('#motion-icon');
  const fullVideo = document.querySelector('#full-video');
  const tabs = Array.from(document.querySelectorAll('[data-scenario]'));
  const scenarios = english ? {
    now: { image: 'apps', animation: 'demo', caption: 'Choosing apps', alt: 'Choosing apps in Perenos', animationAlt: 'Animated app selection and installation in Perenos' },
    restore: { image: 'restore', animation: 'restore', caption: 'Apple Account purchases', alt: 'Restoring purchases in Perenos', animationAlt: 'Animated restoration of App Store purchases in Perenos' },
    later: { image: 'saved', animation: 'save', caption: 'Your library', alt: 'Saving apps in the library', animationAlt: 'Animated installation of saved apps in Perenos' },
  } : {
    now: { image: 'apps', animation: 'demo', caption: 'Выбор приложений', alt: 'Выбор приложений в Переносе', animationAlt: 'Анимация выбора и установки приложений в Переносе' },
    restore: { image: 'restore', animation: 'restore', caption: 'Покупки Apple Account', alt: 'Восстановление покупок в Переносе', animationAlt: 'Анимация восстановления покупок App Store в Переносе' },
    later: { image: 'saved', animation: 'save', caption: 'Ваша библиотека', alt: 'Сохранение приложений в библиотеку', animationAlt: 'Анимация установки сохранённых приложений в Переносе' },
  };
  let selected = 'now';
  let explicitTheme;
  let motionEnabled = !motionPreference.matches;
  let motionTouched = false;
  try { explicitTheme = localStorage.getItem('perenos-site-theme'); } catch { /* Storage is optional. */ }
  if (explicitTheme !== 'light' && explicitTheme !== 'dark') explicitTheme = null;
  try {
    const savedMotion = localStorage.getItem('perenos-site-motion');
    if (savedMotion === 'true' || savedMotion === 'false') {
      motionEnabled = savedMotion === 'true';
      motionTouched = true;
    }
  } catch { /* Motion follows the system when storage is unavailable. */ }
  const activeTheme = () => explicitTheme || (systemTheme.matches ? 'dark' : 'light');
  const onMediaChange = (query, listener) => {
    if (typeof query.addEventListener === 'function') query.addEventListener('change', listener);
    else query.addListener(listener);
  };

  function applyTheme() {
    const theme = activeTheme();
    root.dataset.theme = theme;
    document.querySelectorAll('meta[name="theme-color"]').forEach(meta => {
      meta.content = theme === 'dark' ? '#101923' : '#eaf1f8';
    });
    document.querySelectorAll('.theme-image').forEach(img => {
      const src = `${assetPrefix}assets/${img.dataset.asset}${english ? '-en' : ''}-${theme}.png`;
      img.src = src;
      img.closest('picture')?.querySelector('source')?.setAttribute('srcset', src);
    });
    if (fullVideo) fullVideo.poster = `${assetPrefix}assets/apps${english ? '-en' : ''}-${theme}.png`;
    const dark = theme === 'dark';
    const label = dark
      ? (english ? 'Switch to light theme' : 'Включить светлую тему')
      : (english ? 'Switch to dark theme' : 'Включить тёмную тему');
    themeButton.setAttribute('aria-label', label);
    themeButton.title = label;
  }

  function updateMotion() {
    const animate = motionTouched ? motionEnabled : !motionPreference.matches;
    const scenario = scenarios[selected];
    demoAnimation.hidden = !animate;
    demoPicture.hidden = animate;
    motionToggle.setAttribute('aria-pressed', String(animate));
    motionToggle.querySelector('span').textContent = animate
      ? (english ? 'Hide animation' : 'Скрыть анимацию')
      : (english ? 'Enable animation' : 'Включить анимацию');
    motionIcon.setAttribute('href', animate ? '#pause' : '#play');
    demoAnimation.alt = scenario.animationAlt;
    if (animate) {
      const src = `${assetPrefix}assets/${scenario.animation}${english ? '-en' : ''}${activeTheme() === 'dark' ? '-dark' : ''}.gif`;
      if (demoAnimation.getAttribute('src') !== src) demoAnimation.src = src;
    } else {
      demoAnimation.removeAttribute('src');
    }
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
    updateMotion();
    if (moveFocus) tab.focus();
  }

  themeButton.addEventListener('click', () => {
    explicitTheme = activeTheme() === 'dark' ? 'light' : 'dark';
    try { localStorage.setItem('perenos-site-theme', explicitTheme); } catch { /* The visit still works. */ }
    applyTheme();
  });
  onMediaChange(systemTheme, () => {
    if (!explicitTheme) {
      applyTheme();
      updateMotion();
    }
  });
  motionToggle.addEventListener('click', () => {
    motionEnabled = !motionEnabled;
    motionTouched = true;
    try { localStorage.setItem('perenos-site-motion', String(motionEnabled)); } catch { /* The selection still works. */ }
    updateMotion();
  });
  onMediaChange(motionPreference, () => {
    if (!motionTouched) {
      motionEnabled = !motionPreference.matches;
      updateMotion();
    }
  });
  demoAnimation.addEventListener('error', () => {
    motionEnabled = false;
    updateMotion();
  });
  document.querySelector('.full-demo').addEventListener('toggle', event => {
    if (!event.target.open) fullVideo.pause();
  });
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
        'macos-arm64': /^Perenos-\d+\.\d+\.\d+-macos-arm64(?:-test)?\.dmg$/,
        'macos-x64': /^Perenos-\d+\.\d+\.\d+-macos-x64(?:-test)?\.dmg$/,
      };
      for (const [kind, pattern] of Object.entries(patterns)) {
        const asset = release.assets.find(item => pattern.test(item.name) && !item.name.endsWith('-test.dmg'))
          ?? release.assets.find(item => pattern.test(item.name));
        if (!asset || typeof asset.browser_download_url !== 'string') continue;
        const url = new URL(asset.browser_download_url);
        if (url.origin !== 'https://github.com' || !url.pathname.startsWith('/kr0nsch/perenos-ios/releases/download/') || url.username || url.password) continue;
        const link = document.querySelector(`[data-download="${kind}"]`);
        link.href = url.href;
        link.removeAttribute('aria-disabled');
        if (kind.startsWith('macos-')) {
          const testPackage = asset.name.endsWith('-test.dmg');
          const size = Number(asset.size);
          const sizeText = Number.isFinite(size) && size > 0 ? ` · ${Math.round(size / 1048576)} MB` : '';
          link.querySelector('[data-status]').textContent = testPackage
            ? (english ? `Test DMG · not notarized${sizeText}` : `Тестовая DMG · без нотаризации${sizeText}`)
            : `macOS 15+ · DMG${sizeText}`;
        }
      }
      document.querySelector('#release-label').textContent = english
        ? `Version ${release.tag_name.slice(1)} · Windows and macOS`
        : `Версия ${release.tag_name.slice(1)} · Windows и macOS`;
    } catch { /* Static links and explicit Mac test labels remain available. */ }
    finally { clearTimeout(timeout); }
  }

  applyTheme();
  themeButton.hidden = false;
  motionToggle.hidden = false;
  root.classList.add('enhanced');
  updateMotion();
  updateDownloads();
})();
