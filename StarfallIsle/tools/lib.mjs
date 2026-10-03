// Araclarin ortak parcalari: `vite preview` sunucusu + Playwright tarayicisi.
import { spawn } from 'node:child_process';
import { chromium } from 'playwright';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
export const OUT = path.join(ROOT, 'tools', 'out');

export async function startPreview(port = 4173) {
  const proc = spawn(process.execPath, [path.join(ROOT, 'node_modules/vite/bin/vite.js'), 'preview', '--port', String(port), '--strictPort'], {
    cwd: ROOT, stdio: ['ignore', 'pipe', 'pipe'],
  });
  await new Promise((resolve, reject) => {
    const t = setTimeout(() => reject(new Error('vite preview baslamadi')), 20000);
    proc.stdout.on('data', (d) => {
      if (String(d).includes('localhost')) { clearTimeout(t); resolve(); }
    });
    proc.on('exit', (c) => reject(new Error(`vite preview cikti: ${c}`)));
  });
  return { url: `http://localhost:${port}/`, stop: () => proc.kill() };
}

// Yazilimsal WebGL (SwiftShader): GPU'suz ortamda da gercek render.
export async function launch(width = 1280, height = 720) {
  const browser = await chromium.launch({
    args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--autoplay-policy=no-user-gesture-required'],
  });
  const page = await browser.newPage({ viewport: { width, height } });
  const errors = [];
  page.on('console', (m) => {
    if (m.type() === 'error') errors.push(m.text());
  });
  page.on('pageerror', (e) => errors.push(String(e.stack || e)));
  return { browser, page, errors };
}

export async function waitForGame(page, timeout = 120000) {
  await page.waitForFunction(() => window.__game && window.__game.state === 'menu', null, { timeout });
}

export const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// Oyun saati ve kamera sabitken birkac kare isle (SwiftShader yavas).
export async function settle(page, frames = 6) {
  await page.evaluate((n) => new Promise((res) => {
    let k = 0;
    const tick = () => (++k >= n ? res() : requestAnimationFrame(tick));
    requestAnimationFrame(tick);
  }), frames);
}
