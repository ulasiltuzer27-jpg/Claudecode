// Basarim ikonlari + Steamworks tablosu (istege bagli varlik araci; Node + Playwright).
//   ../Assets/icons/ach/<ID>.png, <ID>_locked.png   128x128 (oyuna gomulur)
//   ../Assets/icons/ach/hidden.png                  gizli basarim yer tutucusu
//   ../steam/achievement_icons/<ID>.png             256x256 (Steam'e yuklenir)
//   ../steam/achievement_icons/64/<ID>.png          64x64
//   ../steam/ACHIEVEMENTS.md                        Steamworks'e girilecek tablo
// Calistirma: cd Starfall/tools && npm install && npx playwright install chromium && npm run icons
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const read = (p) => JSON.parse(fs.readFileSync(path.join(ROOT, p), 'utf8'));
const ach = read('Assets/achievements.json');
const tr = read('Assets/i18n/tr.json');
const en = read('Assets/i18n/en.json');
const art = fs.readFileSync(path.join(ROOT, 'tools/iconArt.js'), 'utf8').replace(/^export /gm, '');

const gameDir = path.join(ROOT, 'Assets', 'icons', 'ach');
const steamDir = path.join(ROOT, 'steam', 'achievement_icons');
for (const d of [gameDir, path.join(steamDir, '64')]) fs.mkdirSync(d, { recursive: true });

const exe = process.env.CHROMIUM_PATH || (fs.existsSync('/opt/pw-browsers/chromium') ? '/opt/pw-browsers/chromium' : undefined);
const browser = await chromium.launch(exe ? { executablePath: exe } : {});
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

const only = process.argv.slice(2);
for (const a of ach.achievements) {
  if (only.length && !only.includes(a.id)) continue;
  for (const locked of [false, true]) {
    const name = `${a.id}${locked ? '_locked' : ''}.png`;
    save(path.join(gameDir, name), await render(a.icon, locked, 128));
    save(path.join(steamDir, name), await render(a.icon, locked, 256));
    save(path.join(steamDir, '64', name), await render(a.icon, locked, 64));
  }
}
save(path.join(gameDir, 'hidden.png'), await render({ glyph: 'star', color: '#6d7480' }, true, 128));

const rows = ach.achievements.map((a) => `| \`${a.id}\` | ${tr[`ach.${a.id}.name`]} / ${en[`ach.${a.id}.name`]} | ${en[`ach.${a.id}.desc`]} | \`${a.stat}\` >= ${a.threshold} | ${a.hidden ? 'evet' : ''} |`);
const stats = ach.stats.map((s) => `| \`${s.key}\` | ${s.kind === 'sum' ? 'INT (toplam)' : 'INT (en yuksek)'} |`);
fs.writeFileSync(path.join(ROOT, 'steam', 'ACHIEVEMENTS.md'), `# Steamworks basarim ve istatistik tablosu

Bu dosya \`tools/gen-icons.mjs\` ile uretilir. Steamworks > Stats & Achievements'a ayni API adlariyla girin.
Ikonlar: \`steam/achievement_icons/\` (256 px, kilitli surumler \`_locked\`).

## Basarimlar (${ach.achievements.length})

| API adi | Ad (TR / EN) | Aciklama (EN) | Kosul | Gizli |
|---|---|---|---|---|
${rows.join('\n')}

## Istatistikler (${ach.stats.length})

| API adi | Tur |
|---|---|
${stats.join('\n')}
`);
await browser.close();
console.log(`${ach.achievements.length} basarim ikonu yazildi`);
