// Steam magaza ve kutuphane gorselleri: oyunun kendi sahnelerinden, Steam'in
// istedigi piksel olculerinde. Cikti: steam/store_art/
//   <dil>/header_capsule.png 920x430, small_capsule.png 462x174,
//   main_capsule.png 1232x706, vertical_capsule.png 748x896,
//   library_capsule.png 600x900, library_header.png 920x430,
//   library_logo.png 1280x720 (seffaf)
//   library_hero.png 3840x1240 (yazisiz, Steam kurali)
//   screenshots/*.png 1920x1080
// Not: bunlar kullanilabilir ilk surumlerdir; magaza sayfasi icin bir
// illustratorun elinden cikmis capsule cok daha fazla tiklanir.
import fs from 'node:fs';
import path from 'node:path';
import { startPreview, launch, waitForGame, settle, ROOT, sleep } from './lib.mjs';

const OUT = path.join(ROOT, 'steam', 'store_art');
fs.mkdirSync(path.join(OUT, 'screenshots'), { recursive: true });

const srv = await startPreview();
const { browser, page, errors } = await launch(1920, 1080);

const TITLES = { tr: { title: 'Yıldız Adası', sub: 'Rahat bir ada macerası' }, en: { title: 'Starfall Isle', sub: 'A cozy island adventure' } };

// Sahneler: oyuncu konumu + kamera. Hepsi oyunun gercek dunyasi.
const SCENES = {
  vista: { hour: 16.2, player: [8, null, 170, Math.PI], cam: [52, 22, 214], look: [4, 18, 40] },
  glide: { hour: 16.4, player: [22, 26, 118, Math.PI * 1.05], glide: true, cam: [18.2, 27.4, 122.5], look: [34, 17, 60] },
  village: { hour: 10.5, player: [10, null, 116, Math.PI * 0.8], cam: [24, 9, 132], look: [4, 3, 100] },
  meadow: { hour: 15.5, player: [-48, null, 58, 2.6], cam: [-38, 6, 66], look: [-56, 4, 46] },
  hollow: { hour: 12.5, player: [-82, null, -66, -2.6], cam: [-62, 17, -50], look: [-89, 4, -82] },
  lake: { hour: 18.0, player: [10, null, -4, 1.3], cam: [-2, 12, -14], look: [32, 4, 4] },
  peak: { hour: 18.8, player: [8, null, -96, 0.2], cam: [20, 46, -78], look: [6, 38, -110] },
  night: { hour: 22.6, player: [8, null, 108, 0.4], cam: [20, 7, 126], look: [6, 3, 102] },
  cove: { hour: 11.5, player: [130, null, 56, 1.0], cam: [118, 9, 74], look: [146, 2, 58] },
};

// Cekimde oyuncu fizigi durur ('photo' durumu, photo mode kapali): kamera ve
// model tam istenen pozda kalir; gokyuzu, su, bitki ortusu canli kalir.
async function scene(name, opts = {}) {
  const s = SCENES[name];
  await page.evaluate(({ s: sc, hideUi, finale }) => {
    const g = window.__game;
    const [px, py, pz, yaw] = sc.player;
    let y = py;
    if (y === null) { const hit = g.physics.groundAt(px, pz, 200); y = (hit ? hit.y : g.world.terrain.height(px, pz)) + 0.05; }
    g.player.spawn(px, y, pz, yaw);
    g.setHour(sc.hour);
    g.state = 'photo';
    g.cameraRig.override = null;
    g.camera.position.set(...sc.cam);
    g.camera.lookAt(...sc.look);
    g.camera.fov = 55;
    g.camera.updateProjectionMatrix();
    const pose = sc.glide ? { speed: 1, grounded: false, vy: -2, gliding: true } : { speed: 0, grounded: true, vy: 0 };
    for (let i = 0; i < 40; i++) g.player.model.update(0.03, pose);
    g.player.model.root.rotation.z = sc.glide ? -0.18 : 0;
    document.getElementById('ui').style.display = hideUi ? 'none' : '';
    if (finale) { g.finale.setLit(true); for (let i = 0; i < 6; i++) g.finale.firework(); }
  }, { s, hideUi: opts.hideUi !== false, finale: !!opts.finale });
  await settle(page, opts.frames || 8);
}

async function overlayLogo(lang, layout) {
  await page.evaluate(({ t, layout: L }) => {
    document.getElementById('store-logo')?.remove();
    const d = document.createElement('div');
    d.id = 'store-logo';
    d.innerHTML = `<div class="sl-star">★</div><div class="sl-title">${t.title}</div>${L.sub ? `<div class="sl-sub">${t.sub}</div>` : ''}`;
    Object.assign(d.style, {
      position: 'fixed', zIndex: 50, color: '#fff', fontFamily: "'Baloo 2', sans-serif", textAlign: L.align || 'left',
      left: L.left ?? 'auto', right: L.right ?? 'auto', top: L.top ?? 'auto', bottom: L.bottom ?? 'auto',
      transform: L.transform || 'none', textShadow: '0 4px 18px rgba(0,0,0,0.45), 0 2px 0 rgba(0,0,0,0.25)', lineHeight: 0.95,
      width: L.width || 'auto',
    });
    const st = document.createElement('style');
    st.id = 'store-logo-style';
    st.textContent = `#store-logo .sl-star{font-size:${L.size * 0.55}px;color:#ffd84a;filter:drop-shadow(0 0 14px rgba(255,200,90,.9));display:${L.star === false ? 'none' : 'block'}}
      #store-logo .sl-title{font-size:${L.size}px;font-weight:800;letter-spacing:1px}
      #store-logo .sl-sub{font-family:'Nunito',sans-serif;font-weight:800;font-size:${L.size * 0.22}px;letter-spacing:3px;text-transform:uppercase;margin-top:${L.size * 0.08}px;opacity:.95}`;
    document.getElementById('store-logo-style')?.remove();
    document.head.appendChild(st);
    document.body.appendChild(d);
  }, { t: TITLES[lang], layout });
}

async function clearLogo() {
  await page.evaluate(() => { document.getElementById('store-logo')?.remove(); });
}

async function capture(file, w, h, sceneName, opts = {}) {
  await page.setViewportSize({ width: w, height: h });
  await sleep(150);
  await scene(sceneName, opts);
  if (opts.logo) await overlayLogo(opts.lang, opts.logo);
  await settle(page, 3);
  await page.screenshot({ path: file });
  await clearLogo();
  console.log('yazildi:', path.relative(ROOT, file));
}

try {
  await page.goto(`${srv.url}?test=1&fresh=1&quality=high`);
  await waitForGame(page);
  await page.evaluate(() => window.__game.newGame(1));
  await sleep(300);
  await page.evaluate(() => { window.__game.ui.hud.hint(null); });

  for (const lang of ['en', 'tr']) {
    const dir = path.join(OUT, lang);
    fs.mkdirSync(dir, { recursive: true });
    await capture(path.join(dir, 'header_capsule.png'), 920, 430, 'vista', { lang, logo: { size: 92, left: '48px', top: '70px', sub: true } });
    await capture(path.join(dir, 'library_header.png'), 920, 430, 'vista', { lang, logo: { size: 92, left: '48px', top: '70px', sub: false } });
    await capture(path.join(dir, 'small_capsule.png'), 462, 174, 'vista', { lang, logo: { size: 60, left: '22px', top: '28px', sub: false, star: false } });
    await capture(path.join(dir, 'main_capsule.png'), 1232, 706, 'glide', { lang, logo: { size: 120, left: '64px', top: '90px', sub: true } });
    await capture(path.join(dir, 'vertical_capsule.png'), 748, 896, 'vista', { lang, logo: { size: 104, left: '50%', top: '70px', transform: 'translateX(-50%)', align: 'center', width: '90%', sub: true } });
    await capture(path.join(dir, 'library_capsule.png'), 600, 900, 'peak', { lang, logo: { size: 92, left: '50%', bottom: '90px', transform: 'translateX(-50%)', align: 'center', width: '92%', sub: false } });
    // seffaf logo
    await page.setViewportSize({ width: 1280, height: 720 });
    await page.evaluate(() => { document.getElementById('game').style.visibility = 'hidden'; document.body.style.background = 'transparent'; document.documentElement.style.background = 'transparent'; document.getElementById('ui').style.display = 'none'; });
    await overlayLogo(lang, { size: 190, left: '50%', top: '50%', transform: 'translate(-50%, -50%)', align: 'center', width: '100%', sub: false });
    await page.screenshot({ path: path.join(dir, 'library_logo.png'), omitBackground: true });
    await clearLogo();
    await page.evaluate(() => { document.getElementById('game').style.visibility = ''; document.body.style.background = ''; document.documentElement.style.background = ''; });
    console.log('yazildi:', path.relative(ROOT, path.join(dir, 'library_logo.png')));
  }
  await capture(path.join(OUT, 'library_hero.png'), 3840, 1240, 'vista', { frames: 4 });
  const shots = ['vista', 'village', 'meadow', 'hollow', 'lake', 'peak', 'night', 'cove', 'glide'];
  for (const [i, s] of shots.entries()) {
    await capture(path.join(OUT, 'screenshots', `${String(i + 1).padStart(2, '0')}_${s}.png`), 1920, 1080, s, { finale: s === 'peak' });
  }
} finally {
  if (errors.length) console.log('KONSOL HATALARI:\n' + errors.slice(0, 10).join('\n---\n'));
  await browser.close();
  srv.stop();
}
process.exit(errors.length ? 1 : 0);
