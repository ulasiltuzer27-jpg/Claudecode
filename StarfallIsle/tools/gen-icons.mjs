// Steam'e yuklenecek basarim ikonlari + uygulama ikonu + partner tablosu.
//   steam/achievement_icons/<ID>.png          256x256, acik
//   steam/achievement_icons/<ID>_locked.png   256x256, kilitli (gri)
//   steam/achievement_icons/64/...            ayni ikonlar 64x64
//   build/icon.png                            512x512 uygulama ikonu
//   steam/ACHIEVEMENTS.md                     Steamworks'e girilecek tablo
// Ikonlar oyunun kullandigi src/achievements/iconArt.js ile cizilir.
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from 'playwright';
import { ROOT } from './lib.mjs';

const ach = JSON.parse(fs.readFileSync(path.join(ROOT, 'src/achievements/achievements.json'), 'utf8'));
const tr = JSON.parse(fs.readFileSync(path.join(ROOT, 'src/i18n/tr.json'), 'utf8'));
const en = JSON.parse(fs.readFileSync(path.join(ROOT, 'src/i18n/en.json'), 'utf8'));
const art = fs.readFileSync(path.join(ROOT, 'src/achievements/iconArt.js'), 'utf8').replace(/^export /gm, '');

const outDir = path.join(ROOT, 'steam', 'achievement_icons');
fs.mkdirSync(path.join(outDir, '64'), { recursive: true });
fs.mkdirSync(path.join(ROOT, 'build'), { recursive: true });

const browser = await chromium.launch();
const page = await browser.newPage();
await page.setContent('<html><body></body></html>');
await page.addScriptTag({ content: `${art}\nwindow.drawAchievementIcon = drawAchievementIcon;` });

const render = (icon, locked, size) => page.evaluate(({ icon: ic, locked: lk, size: sz }) => {
  const c = document.createElement('canvas');
  c.width = c.height = sz;
  window.drawAchievementIcon(c, ic, lk);
  return c.toDataURL('image/png');
}, { icon, locked, size });

const save = (file, dataUrl) => fs.writeFileSync(file, Buffer.from(dataUrl.split(',')[1], 'base64'));

for (const a of ach.achievements) {
  for (const locked of [false, true]) {
    const name = `${a.id}${locked ? '_locked' : ''}.png`;
    save(path.join(outDir, name), await render(a.icon, locked, 256));
    save(path.join(outDir, '64', name), await render(a.icon, locked, 64));
  }
}

// uygulama ikonu: gece gokyuzu, ada, fener ve yildiz
const appIcon = await page.evaluate(() => {
  const S = 512;
  const c = document.createElement('canvas');
  c.width = c.height = S;
  const g = c.getContext('2d');
  const r = S * 0.22;
  g.beginPath();
  g.roundRect(0, 0, S, S, r);
  g.clip();
  const sky = g.createLinearGradient(0, 0, 0, S);
  sky.addColorStop(0, '#1c2a6b');
  sky.addColorStop(0.55, '#4b5bb5');
  sky.addColorStop(0.75, '#f2a07a');
  sky.addColorStop(1, '#ffd29a');
  g.fillStyle = sky;
  g.fillRect(0, 0, S, S);
  g.fillStyle = 'rgba(255,255,255,0.8)';
  for (const [x, y, s] of [[60, 70, 3], [130, 40, 2], [420, 60, 3], [470, 140, 2], [90, 160, 2], [380, 30, 2]]) { g.beginPath(); g.arc(x, y, s, 0, Math.PI * 2); g.fill(); }
  // deniz
  const sea = g.createLinearGradient(0, S * 0.72, 0, S);
  sea.addColorStop(0, '#3fc8c0');
  sea.addColorStop(1, '#1b5fa8');
  g.fillStyle = sea;
  g.fillRect(0, S * 0.74, S, S * 0.26);
  // ada
  g.fillStyle = '#5fbf4f';
  g.beginPath();
  g.moveTo(40, S * 0.78);
  g.quadraticCurveTo(150, S * 0.5, 260, S * 0.52);
  g.quadraticCurveTo(380, S * 0.55, 470, S * 0.78);
  g.closePath();
  g.fill();
  g.fillStyle = '#ead7a0';
  g.fillRect(40, S * 0.765, 430, 12);
  // fener
  const lx = 262;
  const ly = S * 0.53;
  for (let i = 0; i < 5; i++) {
    g.fillStyle = i % 2 ? '#e04848' : '#fbf7ef';
    g.fillRect(lx - 22 + i * 1.5, ly - 30 - i * 28, 44 - i * 3, 28);
  }
  g.fillStyle = '#fff3c4';
  g.fillRect(lx - 16, ly - 192, 32, 26);
  g.fillStyle = '#e04848';
  g.beginPath(); g.moveTo(lx - 22, ly - 192); g.lineTo(lx, ly - 220); g.lineTo(lx + 22, ly - 192); g.fill();
  g.fillStyle = 'rgba(255,240,180,0.45)';
  g.beginPath(); g.moveTo(lx + 14, ly - 182); g.lineTo(S, ly - 250); g.lineTo(S, ly - 120); g.closePath(); g.fill();
  // buyuk yildiz
  const star = (cx, cy, ro, ri) => {
    g.beginPath();
    for (let i = 0; i < 10; i++) {
      const rr = i % 2 ? ri : ro;
      const a = -Math.PI / 2 + (i * Math.PI) / 5;
      if (i) g.lineTo(cx + Math.cos(a) * rr, cy + Math.sin(a) * rr); else g.moveTo(cx + Math.cos(a) * rr, cy + Math.sin(a) * rr);
    }
    g.closePath();
  };
  g.shadowColor = 'rgba(255,210,100,0.9)';
  g.shadowBlur = 40;
  g.fillStyle = '#ffd84a';
  star(130, 130, 78, 34);
  g.fill();
  g.shadowBlur = 0;
  g.lineWidth = 8;
  g.strokeStyle = '#fff3b0';
  g.stroke();
  return c.toDataURL('image/png');
});
save(path.join(ROOT, 'build', 'icon.png'), appIcon);
await browser.close();

// partner sitesi tablosu
const rows = ach.achievements.map((a) => `| \`${a.id}\` | ${tr[`ach.${a.id}.name`]} | ${tr[`ach.${a.id}.desc`]} | ${en[`ach.${a.id}.name`]} | ${en[`ach.${a.id}.desc`]} | ${a.hidden ? 'evet' : 'hayir'} | \`${a.stat}\` >= ${a.threshold} | \`${a.id}.png\` / \`${a.id}_locked.png\` |`);
const statRows = ach.stats.map((s) => `| \`${s.key}\` | INT | ${s.kind === 'sum' ? 'birikimli (artar)' : 'rekor (yalnizca buyurse yazilir)'} |`);
const md = `# Steamworks basarim tablosu

Bu dosya \`tools/gen-icons.mjs\` ile uretildi; elle duzenlemeyin
(kaynak: \`src/achievements/achievements.json\` + \`src/i18n/*.json\`).

Steamworks partner sitesinde **Uygulama Yonetimi -> Stats & Achievements**
sayfasina asagidaki her satiri girin. "API Name" sutunu oyunun Steam'e
gonderdigi addir, birebir ayni olmali. Ikonlar \`steam/achievement_icons/\`
klasorunde (256x256; 64x64 kopyalari \`64/\` altinda).

## Basarimlar (${ach.achievements.length})

| API Name | Ad (TR) | Aciklama (TR) | Name (EN) | Description (EN) | Gizli | Kosul | Ikon (acik / kilitli) |
|---|---|---|---|---|---|---|---|
${rows.join('\n')}

## Istatistikler

Istatistikler Steam'de basarim ilerleme cubugunu gostermek icin kullanilir
(istege bagli). Tanimlanmazsa oyun calismaya devam eder; yalnizca
\`setInt\` cagrisi sessizce basarisiz olur.

| API Name | Tur | Davranis |
|---|---|---|
${statRows.join('\n')}
`;
fs.writeFileSync(path.join(ROOT, 'steam', 'ACHIEVEMENTS.md'), md);
console.log(`${ach.achievements.length * 4} ikon, build/icon.png ve steam/ACHIEVEMENTS.md yazildi`);
