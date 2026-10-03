// Basarim ikonlari: oyunda VE tools/gen-icons.mjs'te ayni kodla cizilir,
// boylece Steam'e yuklenen ikonla oyundaki ikon hep ayni. Disari bagimlilik
// yok (gen-icons bu dosyayi tarayici sayfasina dogrudan enjekte ediyor).

function shade(hex, k) {
  const n = parseInt(hex.slice(1), 16);
  let r = (n >> 16) & 255;
  let g = (n >> 8) & 255;
  let b = n & 255;
  if (k > 0) {
    r += (255 - r) * k; g += (255 - g) * k; b += (255 - b) * k;
  } else {
    r *= 1 + k; g *= 1 + k; b *= 1 + k;
  }
  return `rgb(${r | 0},${g | 0},${b | 0})`;
}

function starPath(g, cx, cy, ro, ri, n = 5, rot = -Math.PI / 2) {
  g.beginPath();
  for (let i = 0; i < n * 2; i++) {
    const r = i % 2 ? ri : ro;
    const a = rot + (i * Math.PI) / n;
    const x = cx + Math.cos(a) * r;
    const y = cy + Math.sin(a) * r;
    if (i) g.lineTo(x, y); else g.moveTo(x, y);
  }
  g.closePath();
}

function fishPath(g, x, y, s, flip = 1) {
  g.save();
  g.translate(x, y);
  g.scale(s * flip, s);
  g.beginPath();
  g.ellipse(0, 0, 22, 13, 0, 0, Math.PI * 2);
  g.moveTo(18, 0);
  g.lineTo(34, -12);
  g.lineTo(34, 12);
  g.closePath();
  g.fill();
  g.fillStyle = 'rgba(0,0,0,0.55)';
  g.beginPath();
  g.arc(-11, -3, 3, 0, Math.PI * 2);
  g.fill();
  g.restore();
}

const GLYPHS = {
  paw(g) {
    g.beginPath(); g.ellipse(50, 60, 17, 14, 0, 0, Math.PI * 2); g.fill();
    for (const [x, y, r] of [[30, 40, 7.5], [43, 30, 8], [57, 30, 8], [70, 40, 7.5]]) { g.beginPath(); g.ellipse(x, y, r, r * 1.2, 0, 0, Math.PI * 2); g.fill(); }
  },
  star(g) { starPath(g, 50, 52, 32, 14); g.fill(); },
  stars(g) { starPath(g, 42, 56, 24, 10); g.fill(); starPath(g, 70, 30, 12, 5); g.fill(); starPath(g, 74, 66, 9, 4); g.fill(); },
  constellation(g) {
    const pts = [[22, 70], [36, 44], [56, 52], [70, 28], [80, 62]];
    g.lineWidth = 3; g.strokeStyle = 'rgba(255,255,255,0.8)';
    g.beginPath(); pts.forEach(([x, y], i) => (i ? g.lineTo(x, y) : g.moveTo(x, y))); g.stroke();
    for (const [x, y] of pts) { g.beginPath(); g.arc(x, y, 4.5, 0, Math.PI * 2); g.fill(); }
    starPath(g, 70, 28, 13, 5.5); g.fill();
  },
  feather(g) {
    g.save(); g.translate(50, 50); g.rotate(-0.6);
    g.beginPath(); g.moveTo(0, -36); g.quadraticCurveTo(20, -5, 3, 30); g.lineTo(-3, 30); g.quadraticCurveTo(-20, -5, 0, -36); g.fill();
    g.strokeStyle = 'rgba(0,0,0,0.25)'; g.lineWidth = 2.5; g.beginPath(); g.moveTo(0, -30); g.lineTo(0, 40); g.stroke();
    for (let i = 0; i < 5; i++) { g.beginPath(); g.moveTo(0, -18 + i * 9); g.lineTo(10, -24 + i * 9); g.moveTo(0, -18 + i * 9); g.lineTo(-10, -24 + i * 9); g.stroke(); }
    g.restore();
  },
  wings(g) {
    for (const s of [-1, 1]) {
      g.save(); g.translate(50, 55); g.scale(s, 1);
      g.beginPath(); g.moveTo(4, 8); g.quadraticCurveTo(20, -30, 42, -26); g.quadraticCurveTo(36, -14, 40, -8); g.quadraticCurveTo(30, -4, 34, 4); g.quadraticCurveTo(22, 6, 24, 14); g.quadraticCurveTo(12, 16, 4, 8); g.fill();
      g.restore();
    }
  },
  shell(g) {
    g.beginPath(); g.moveTo(50, 78); g.lineTo(22, 44); g.quadraticCurveTo(50, 8, 78, 44); g.closePath(); g.fill();
    g.strokeStyle = 'rgba(0,0,0,0.22)'; g.lineWidth = 2.5;
    for (let i = -2; i <= 2; i++) { g.beginPath(); g.moveTo(50, 76); g.lineTo(50 + i * 11, 26 + Math.abs(i) * 5); g.stroke(); }
    g.beginPath(); g.moveTo(40, 78); g.lineTo(60, 78); g.lineTo(56, 86); g.lineTo(44, 86); g.closePath(); g.fill();
  },
  crown(g) {
    g.beginPath(); g.moveTo(22, 70); g.lineTo(18, 34); g.lineTo(36, 50); g.lineTo(50, 26); g.lineTo(64, 50); g.lineTo(82, 34); g.lineTo(78, 70); g.closePath(); g.fill();
    g.fillRect(22, 72, 56, 8);
    g.fillStyle = 'rgba(0,0,0,0.25)';
    for (const x of [34, 50, 66]) { g.beginPath(); g.arc(x, 62, 4, 0, Math.PI * 2); g.fill(); }
  },
  lighthouse(g) {
    g.beginPath(); g.moveTo(40, 84); g.lineTo(44, 38); g.lineTo(56, 38); g.lineTo(60, 84); g.closePath(); g.fill();
    g.fillRect(40, 28, 20, 10);
    g.beginPath(); g.moveTo(38, 28); g.lineTo(50, 16); g.lineTo(62, 28); g.closePath(); g.fill();
    g.fillStyle = 'rgba(0,0,0,0.25)'; g.fillRect(42, 50, 16, 7); g.fillRect(41, 68, 18, 7);
    g.fillStyle = 'rgba(255,255,255,0.75)';
    g.beginPath(); g.moveTo(60, 30); g.lineTo(92, 20); g.lineTo(92, 42); g.closePath(); g.fill();
    g.beginPath(); g.moveTo(40, 30); g.lineTo(8, 20); g.lineTo(8, 42); g.closePath(); g.fill();
  },
  mountain(g) {
    g.beginPath(); g.moveTo(10, 80); g.lineTo(40, 34); g.lineTo(52, 50); g.lineTo(64, 28); g.lineTo(92, 80); g.closePath(); g.fill();
    g.fillStyle = 'rgba(0,0,0,0.2)'; g.beginPath(); g.moveTo(64, 28); g.lineTo(92, 80); g.lineTo(70, 80); g.closePath(); g.fill();
    g.fillStyle = '#ffffff'; g.fillRect(63, 12, 2.5, 18);
    g.beginPath(); g.moveTo(65.5, 12); g.lineTo(78, 16); g.lineTo(65.5, 20); g.closePath(); g.fill();
  },
  leaf(g) {
    g.save(); g.translate(50, 50); g.rotate(-0.5);
    g.beginPath(); g.moveTo(0, -36); g.quadraticCurveTo(30, 0, 0, 36); g.quadraticCurveTo(-30, 0, 0, -36); g.fill();
    g.strokeStyle = 'rgba(0,0,0,0.25)'; g.lineWidth = 3; g.beginPath(); g.moveTo(0, -30); g.lineTo(0, 44); g.stroke();
    g.restore();
    g.strokeStyle = 'rgba(255,255,255,0.7)'; g.lineWidth = 3;
    for (const y of [70, 80]) { g.beginPath(); g.moveTo(14, y); g.quadraticCurveTo(30, y - 6, 46, y); g.stroke(); }
  },
  cloud(g) {
    // tek yol: parcalarin golgeleri birbirinin ustune binmesin
    g.beginPath();
    for (const [x, y, r] of [[34, 58, 16], [52, 46, 20], [70, 58, 15], [50, 62, 16]]) { g.moveTo(x + r, y); g.arc(x, y, r, 0, Math.PI * 2); }
    g.rect(30, 58, 42, 16);
    g.fill('nonzero');
  },
  fish(g) { fishPath(g, 46, 52, 1.25); },
  fishes(g) { fishPath(g, 40, 38, 0.9); fishPath(g, 60, 66, 0.9, -1); },
  trophy(g) {
    g.beginPath(); g.moveTo(30, 22); g.lineTo(70, 22); g.quadraticCurveTo(70, 58, 50, 60); g.quadraticCurveTo(30, 58, 30, 22); g.fill();
    g.lineWidth = 6; g.strokeStyle = '#ffffff';
    g.beginPath(); g.arc(28, 34, 10, Math.PI * 0.5, Math.PI * 1.5); g.stroke();
    g.beginPath(); g.arc(72, 34, 10, -Math.PI * 0.5, Math.PI * 0.5); g.stroke();
    g.fillRect(45, 58, 10, 12); g.fillRect(34, 70, 32, 8);
  },
  heart(g) {
    g.beginPath(); g.moveTo(50, 80);
    g.bezierCurveTo(10, 54, 18, 18, 50, 34);
    g.bezierCurveTo(82, 18, 90, 54, 50, 80); g.fill();
  },
  chat(g) {
    g.beginPath(); g.ellipse(40, 42, 24, 17, 0, 0, Math.PI * 2); g.fill();
    g.beginPath(); g.moveTo(26, 52); g.lineTo(20, 66); g.lineTo(36, 56); g.fill();
    g.globalAlpha = 0.75;
    g.beginPath(); g.ellipse(64, 60, 20, 14, 0, 0, Math.PI * 2); g.fill();
    g.beginPath(); g.moveTo(74, 68); g.lineTo(82, 80); g.lineTo(66, 72); g.fill();
    g.globalAlpha = 1;
  },
  wave(g) {
    g.lineWidth = 7; g.strokeStyle = '#ffffff'; g.lineCap = 'round';
    for (const y of [36, 52, 68]) {
      g.beginPath(); g.moveTo(16, y);
      for (let x = 16; x < 84; x += 17) g.quadraticCurveTo(x + 8.5, y - 10, x + 17, y);
      g.stroke();
    }
  },
  moon(g) {
    // hilal: dis daire eksi ic daire, kesisim noktalarindan analitik yol
    // (destination-out arka plani da delerdi)
    const [x1, y1, r1, x2, y2, r2] = [46, 54, 28, 60, 42, 23];
    const d = Math.hypot(x2 - x1, y2 - y1);
    const a = (r1 * r1 - r2 * r2 + d * d) / (2 * d);
    const h = Math.sqrt(r1 * r1 - a * a);
    const bx = x1 + (a * (x2 - x1)) / d;
    const by = y1 + (a * (y2 - y1)) / d;
    const p1 = [bx + (h * (y2 - y1)) / d, by - (h * (x2 - x1)) / d];
    const p2 = [bx - (h * (y2 - y1)) / d, by + (h * (x2 - x1)) / d];
    const ang = (cx, cy, p) => Math.atan2(p[1] - cy, p[0] - cx);
    g.beginPath();
    g.arc(x1, y1, r1, ang(x1, y1, p1), ang(x1, y1, p2), true);
    g.arc(x2, y2, r2, ang(x2, y2, p2), ang(x2, y2, p1), false);
    g.closePath();
    g.fill();
    starPath(g, 76, 68, 7, 3); g.fill(); starPath(g, 82, 24, 5, 2); g.fill();
  },
  map(g) {
    g.beginPath(); g.moveTo(16, 26); g.lineTo(38, 20); g.lineTo(62, 28); g.lineTo(84, 22); g.lineTo(84, 74); g.lineTo(62, 80); g.lineTo(38, 72); g.lineTo(16, 78); g.closePath(); g.fill();
    g.strokeStyle = 'rgba(0,0,0,0.3)'; g.lineWidth = 3; g.setLineDash([5, 5]);
    g.beginPath(); g.moveTo(24, 66); g.quadraticCurveTo(42, 36, 64, 52); g.stroke(); g.setLineDash([]);
    g.lineWidth = 4; g.beginPath(); g.moveTo(64, 40); g.lineTo(74, 50); g.moveTo(74, 40); g.lineTo(64, 50); g.stroke();
  },
  bounce(g) {
    for (const [y, a] of [[66, 0.6], [48, 0.8], [30, 1]]) {
      g.globalAlpha = a;
      g.beginPath(); g.moveTo(30, y + 10); g.lineTo(50, y - 6); g.lineTo(70, y + 10); g.lineTo(62, y + 10); g.lineTo(50, y + 2); g.lineTo(38, y + 10); g.closePath(); g.fill();
    }
    g.globalAlpha = 1;
    g.fillRect(28, 80, 44, 6);
  },
  camera(g) {
    g.beginPath(); g.roundRect ? g.roundRect(16, 32, 68, 44, 8) : g.rect(16, 32, 68, 44); g.fill();
    g.fillRect(36, 24, 22, 10);
    g.fillStyle = 'rgba(0,0,0,0.35)'; g.beginPath(); g.arc(50, 54, 15, 0, Math.PI * 2); g.fill();
    g.fillStyle = '#ffffff'; g.beginPath(); g.arc(50, 54, 9, 0, Math.PI * 2); g.fill();
  },
  gem(g) {
    g.beginPath(); g.moveTo(30, 36); g.lineTo(50, 18); g.lineTo(70, 36); g.lineTo(50, 84); g.closePath(); g.fill();
    g.fillStyle = 'rgba(0,0,0,0.18)'; g.beginPath(); g.moveTo(50, 18); g.lineTo(70, 36); g.lineTo(50, 84); g.closePath(); g.fill();
    g.strokeStyle = 'rgba(0,0,0,0.2)'; g.lineWidth = 2; g.beginPath(); g.moveTo(30, 36); g.lineTo(70, 36); g.stroke();
    starPath(g, 22, 22, 6, 2.5); g.fill(); starPath(g, 80, 64, 5, 2); g.fill();
  },
  clock(g) {
    g.beginPath(); g.arc(56, 52, 26, 0, Math.PI * 2); g.fill();
    g.strokeStyle = 'rgba(0,0,0,0.4)'; g.lineWidth = 4; g.lineCap = 'round';
    g.beginPath(); g.moveTo(56, 52); g.lineTo(56, 36); g.moveTo(56, 52); g.lineTo(68, 58); g.stroke();
    g.strokeStyle = '#ffffff'; g.lineWidth = 5;
    for (const y of [40, 52, 64]) { g.beginPath(); g.moveTo(10, y); g.lineTo(24, y); g.stroke(); }
  },
  medal(g) {
    g.beginPath(); g.moveTo(34, 14); g.lineTo(46, 14); g.lineTo(54, 44); g.lineTo(42, 44); g.closePath(); g.fill();
    g.beginPath(); g.moveTo(66, 14); g.lineTo(54, 14); g.lineTo(46, 44); g.lineTo(58, 44); g.closePath(); g.fill();
    g.beginPath(); g.arc(50, 62, 22, 0, Math.PI * 2); g.fill();
    g.fillStyle = 'rgba(0,0,0,0.25)'; starPath(g, 50, 63, 13, 5.5); g.fill();
  },
};

export const ICON_GLYPHS = Object.keys(GLYPHS);

export function drawAchievementIcon(canvas, icon, locked = false) {
  const size = canvas.width;
  const g = canvas.getContext('2d');
  g.clearRect(0, 0, size, size);
  const color = locked ? '#6d7480' : icon.color;
  const r = size * 0.2;
  g.save();
  g.beginPath();
  if (g.roundRect) g.roundRect(0, 0, size, size, r); else g.rect(0, 0, size, size);
  g.clip();
  const grd = g.createRadialGradient(size * 0.35, size * 0.3, size * 0.05, size * 0.5, size * 0.5, size * 0.75);
  grd.addColorStop(0, shade(color, 0.35));
  grd.addColorStop(0.55, color);
  grd.addColorStop(1, shade(color, -0.4));
  g.fillStyle = grd;
  g.fillRect(0, 0, size, size);
  // isik halkasi
  g.globalAlpha = 0.12;
  g.fillStyle = '#ffffff';
  g.beginPath();
  g.arc(size * 0.5, size * 0.5, size * 0.42, 0, Math.PI * 2);
  g.fill();
  g.globalAlpha = 1;
  // parilti noktalari
  g.fillStyle = 'rgba(255,255,255,0.5)';
  for (const [x, y, s] of [[0.15, 0.18, 0.025], [0.85, 0.2, 0.018], [0.82, 0.85, 0.022], [0.14, 0.8, 0.015]]) {
    starPath(g, x * size, y * size, s * size * 2, s * size * 0.8, 4, 0);
    g.fill();
  }
  // sembol
  g.save();
  g.scale(size / 100, size / 100);
  g.shadowColor = 'rgba(0,0,0,0.35)';
  g.shadowBlur = 4;
  g.shadowOffsetY = 2;
  g.fillStyle = locked ? 'rgba(235,238,242,0.75)' : '#ffffff';
  g.strokeStyle = g.fillStyle;
  (GLYPHS[icon.glyph] || GLYPHS.star)(g);
  g.restore();
  // ic kenar
  g.lineWidth = size * 0.035;
  g.strokeStyle = 'rgba(255,255,255,0.35)';
  g.beginPath();
  if (g.roundRect) g.roundRect(size * 0.02, size * 0.02, size * 0.96, size * 0.96, r * 0.9); else g.rect(size * 0.02, size * 0.02, size * 0.96, size * 0.96);
  g.stroke();
  g.restore();
  if (locked) {
    // gri ton + hafif karartma (Steam'in kilitli ikon beklentisi)
    const img = g.getImageData(0, 0, size, size);
    const d = img.data;
    for (let i = 0; i < d.length; i += 4) {
      const l = d[i] * 0.3 + d[i + 1] * 0.59 + d[i + 2] * 0.11;
      d[i] = d[i + 1] = d[i + 2] = l * 0.8;
    }
    g.putImageData(img, 0, 0);
  }
  return canvas;
}
