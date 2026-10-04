// Steam magaza ve kutuphane gorselleri (istege bagli varlik araci; Node + Playwright).
// Once oyunun kendisi sahneleri ceker (dotnet run -- --capture ... --hideui), sonra
// Chromium bu cekimleri Steam'in istedigi olculere kirpar ve logoyu yazar.
//   ../steam/store_art/screenshots/*.png       1920x1080 (arayuzsuz oyun ici)
//   ../steam/store_art/<dil>/header_capsule.png 920x430, small_capsule.png 462x174,
//     main_capsule.png 1232x706, vertical_capsule.png 748x896,
//     library_capsule.png 600x900, library_header.png 920x430,
//     library_logo.png 1280x720 (seffaf)
//   ../steam/store_art/library_hero.png         3840x1240 (yazisiz, Steam kurali)
// Calistirma: cd Starfall && dotnet build && cd tools && npm install && npm run store-art
// Linux'ta ekran yoksa xvfb-run kullanilir. Not: bunlar kullanilabilir ilk surumlerdir;
// magaza sayfasi icin bir illustratorun elinden cikmis capsule cok daha fazla tiklanir.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { chromium } from 'playwright';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const OUT = path.join(ROOT, 'steam', 'store_art');
const SHOTS = path.join(OUT, 'screenshots');
fs.mkdirSync(SHOTS, { recursive: true });

// Sahneler: oyuncu konumu (x,y,z,yaw) + kamera + bakis + saat. Hepsi oyunun gercek dunyasi.
// y: oyuncu bu yukseklikten yere duser.
const SCENES = {
  '01_vista': { hour: 16.2, pos: [8, 30, 170, Math.PI], cam: [52, 22, 214], look: [4, 18, 40] },
  '02_village': { hour: 10.5, pos: [10, 20, 116, Math.PI * 0.8], cam: [24, 9, 132], look: [4, 3, 100] },
  '03_meadow': { hour: 15.5, pos: [-48, 20, 58, 2.6], cam: [-38, 6, 66], look: [-56, 4, 46] },
  '04_hollow': { hour: 12.5, pos: [-82, 30, -66, -2.6], cam: [-62, 17, -50], look: [-89, 4, -82] },
  '05_lake': { hour: 18.0, pos: [10, 20, -4, 1.3], cam: [-2, 12, -14], look: [32, 4, 4] },
  '06_peak': { hour: 18.8, pos: [8, 60, -96, 0.2], cam: [20, 46, -78], look: [6, 38, -110] },
  '07_night': { hour: 22.6, pos: [8, 20, 108, 0.4], cam: [20, 7, 126], look: [6, 3, 102] },
  '08_snow_harbor': { hour: 10.5, pos: [552, 20, -468, 2.4], cam: [530, 16, -444], look: [566, 9, -486] },
  '09_observatory': { hour: 16.5, pos: [666, 60, -644, 2.4], cam: [590, 70, -610], look: [670, 45, -655] },
  '10_aurora': { hour: 22.5, pos: [700, 15, -508, 2.4], cam: [680, 26, -470], look: [715, 14, -530] },
  '11_ice_cave': { hour: 12, pos: [757.5, 8, -630, 1.57], cam: [761.8, 9.4, -628.8], look: [756.5, 8.2, -630.6] },
  '12_house': { hour: 15, house: true, cam: [-700, 304.6, 704.6], look: [-700, 300.4, 699.6] },
};

const TITLES = {
  tr: { title: 'Yıldız Adası', sub: 'Rahat bir ada macerası' },
  en: { title: 'Starfall Isle', sub: 'A cozy island adventure' },
};

function capture(name, s, size = '1920x1080') {
  const file = path.join(SHOTS, `${name}.png`);
  const data = fs.mkdtempSync(path.join(os.tmpdir(), 'starfall-art-'));
  const v = (a) => a.map((n) => n.toFixed(2)).join(',');
  const args = ['run', '--no-build', '--', '--data', data, '--lang', 'tr', '--new', '1', '--nosound', '--hideui',
    '--size', size, '--capture', file, '--frames', '150', '--hour', String(s.hour), '--cam', v(s.cam), '--look', v(s.look)];
  if (s.pos) args.push('--pos', v(s.pos.slice(0, 3)), '--yaw', s.pos[3].toFixed(3));
  if (s.house) args.push('--house', '1');
  const [w, h] = size.split('x');
  const headless = process.platform === 'linux' && !process.env.DISPLAY;
  const cmd = headless ? 'xvfb-run' : 'dotnet';
  const full = headless ? ['-a', '-s', `-screen 0 ${w}x${h}x24`, 'dotnet', ...args] : args;
  const r = spawnSync(cmd, full, { cwd: ROOT, stdio: ['ignore', 'pipe', 'inherit'], env: { ...process.env, XDG_RUNTIME_DIR: process.env.XDG_RUNTIME_DIR || data } });
  fs.rmSync(data, { recursive: true, force: true });
  if (r.status !== 0 || !fs.existsSync(file)) throw new Error(`cekim basarisiz: ${name}`);
  console.log('cekildi', name);
  return file;
}

// argumansiz: hepsini cek + birlestir. "--compose": yalnizca birlestir. Sahne adlari: yalnizca onlari cek.
const argv = process.argv.slice(2);
const composeOnly = argv.includes('--compose');
const only = argv.filter((a) => !a.startsWith('--'));
const CACHE = path.join(ROOT, 'tools', 'out');
fs.mkdirSync(CACHE, { recursive: true });
for (const [name, s] of Object.entries(SCENES)) {
  if (composeOnly || (only.length && !only.includes(name))) continue;
  capture(name, s);
}
// kutuphane kahramani icin genis ve yuksek cozunurluklu ayri cekim (yazisiz; depoya girmez)
if (!composeOnly && (!only.length || only.includes('hero'))) {
  const s = SCENES['01_vista'];
  fs.renameSync(capture('hero_wide', { ...s, cam: [52, 26, 218] }, '3840x2160'), path.join(CACHE, 'hero_wide.png'));
}

// --- capsule'ler: cekim + logo, Chromium'da tam piksel olcusunde
const font = (f) => pathToFileURL(path.join(ROOT, 'Assets', 'fonts', f)).href;
const img = (n) => pathToFileURL(path.join(SHOTS, `${n}.png`)).href;
const css = `
@font-face { font-family: Baloo; src: url('${font('baloo800-latin.ttf')}'); }
@font-face { font-family: Baloo; src: url('${font('baloo800-latin-ext.ttf')}'); unicode-range: U+0100-024F; }
@font-face { font-family: Nunito; src: url('${font('nunito800-latin.ttf')}'); }
@font-face { font-family: Nunito; src: url('${font('nunito800-latin-ext.ttf')}'); unicode-range: U+0100-024F; }
html, body { margin: 0; overflow: hidden; background: transparent; }
.bg { position: absolute; inset: 0; background-size: cover; }
.shade { position: absolute; inset: 0; }
.logo { position: absolute; color: #fff; font-family: Baloo; line-height: .92;
  text-shadow: 0 4px 18px rgba(0,0,0,.45), 0 2px 0 rgba(0,0,0,.25); }
.star { color: #ffd84a; filter: drop-shadow(0 0 14px rgba(255,200,90,.9)); }
.sub { font-family: Nunito; letter-spacing: 3px; text-transform: uppercase; opacity: .95; }`;

// her capsule: olcu, arka plan sahnesi + odak, logo yerlesimi
const CAPSULES = {
  header_capsule: { w: 920, h: 430, bg: '01_vista', focus: '50% 40%', logo: { left: 48, bottom: 44, size: 92, sub: true } },
  small_capsule: { w: 462, h: 174, bg: '01_vista', focus: '50% 35%', logo: { left: 26, bottom: 22, size: 58, star: false } },
  main_capsule: { w: 1232, h: 706, bg: '09_observatory', focus: '50% 45%', logo: { left: 64, bottom: 60, size: 128, sub: true } },
  vertical_capsule: { w: 748, h: 896, bg: '02_village', focus: '45% 50%', logo: { center: true, top: 60, size: 112, sub: true } },
  library_capsule: { w: 600, h: 900, bg: '10_aurora', focus: '50% 50%', logo: { center: true, top: 70, size: 96, sub: true } },
  library_header: { w: 920, h: 430, bg: '08_snow_harbor', focus: '50% 45%', logo: { left: 48, bottom: 44, size: 92 } },
};

const exe = process.env.CHROMIUM_PATH || (fs.existsSync('/opt/pw-browsers/chromium') ? '/opt/pw-browsers/chromium' : undefined);
const browser = await chromium.launch(exe ? { executablePath: exe, args: ['--allow-file-access-from-files'] } : { args: ['--allow-file-access-from-files'] });

async function shot(w, h, body, file, transparent = false) {
  const page = await browser.newPage({ viewport: { width: w, height: h } });
  // file:// sayfasi: arka plan cekimleri ve yazi tipleri yerel dosyalardan yuklenebilsin
  const html = path.join(os.tmpdir(), `starfall-art-${process.pid}.html`);
  fs.writeFileSync(html, `<html><head><meta charset="utf-8"><style>${css}</style></head><body>${body}</body></html>`);
  await page.goto(pathToFileURL(html).href, { waitUntil: 'load' });
  fs.rmSync(html, { force: true });
  await page.evaluate(() => document.fonts.ready);
  await page.screenshot({ path: file, omitBackground: transparent });
  await page.close();
  console.log('yazildi', path.relative(ROOT, file));
}

function logoHtml(t, L) {
  const pos = L.center
    ? `left:0;right:0;text-align:center;top:${L.top}px`
    : `left:${L.left}px;bottom:${L.bottom}px`;
  return `<div class="logo" style="${pos}">
    ${L.star === false ? '' : `<div class="star" style="font-size:${L.size * 0.5}px">★</div>`}
    <div style="font-size:${L.size}px">${t.title}</div>
    ${L.sub ? `<div class="sub" style="font-size:${L.size * 0.2}px;margin-top:${L.size * 0.1}px">${t.sub}</div>` : ''}
  </div>`;
}

const haveAll = Object.values(CAPSULES).every((c) => fs.existsSync(path.join(SHOTS, `${c.bg}.png`)));
for (const [lang, t] of haveAll ? Object.entries(TITLES) : []) {
  const dir = path.join(OUT, lang);
  fs.mkdirSync(dir, { recursive: true });
  for (const [name, c] of Object.entries(CAPSULES)) {
    const shade = c.logo.center
      ? 'linear-gradient(180deg, rgba(10,14,40,.55) 0%, rgba(10,14,40,0) 45%)'
      : 'linear-gradient(20deg, rgba(10,14,40,.6) 0%, rgba(10,14,40,0) 55%)';
    const body = `<div class="bg" style="background-image:url('${img(c.bg)}');background-position:${c.focus}"></div>
      <div class="shade" style="background:${shade}"></div>${logoHtml(t, c.logo)}`;
    await shot(c.w, c.h, body, path.join(dir, `${name}.png`));
  }
  // seffaf logo (kutuphane kahraminin ustune Steam yerlestirir)
  await shot(1280, 720, logoHtml(t, { center: true, top: 170, size: 210, sub: true }), path.join(dir, 'library_logo.png'), true);
}
// kahraman: yazisiz, 3840x1240
const hero = path.join(CACHE, 'hero_wide.png');
if (fs.existsSync(hero))
  await shot(3840, 1240, `<div class="bg" style="background-image:url('${pathToFileURL(hero).href}');background-position:50% 42%"></div>`, path.join(OUT, 'library_hero.png'));
await browser.close();
