import * as THREE from 'three';
import type { Assets } from '../core/Assets';
import { ITEMS, RARITY_COLOR } from '../rpg/Items';

/**
 * Eşya ikonları: modeli olan eşyalar 3D modelden çizilir (kendi küçük
 * sahnesinde, ayrı bir render target'a), modelsizler (yüzük, tılsım, kemik)
 * canvas'a elle çizilir. Hepsi açılışta bir kez üretilip data URL olarak
 * saklanır.
 */
export class Icons {
  private cache = new Map<string, string>();

  build(renderer: THREE.WebGLRenderer, assets: Assets): void {
    const size = 96;
    const rt = new THREE.WebGLRenderTarget(size, size, { colorSpace: THREE.SRGBColorSpace });
    const scene = new THREE.Scene();
    scene.add(new THREE.HemisphereLight(0xffffff, 0x445566, 2.2));
    const dl = new THREE.DirectionalLight(0xffffff, 2.5);
    dl.position.set(2, 3, 4);
    scene.add(dl);
    const cam = new THREE.PerspectiveCamera(30, 1, 0.01, 100);
    const pixels = new Uint8Array(size * size * 4);
    const canvas = document.createElement('canvas');
    canvas.width = canvas.height = size;
    const g = canvas.getContext('2d')!;
    const prevTarget = renderer.getRenderTarget();
    const prevClear = renderer.getClearColor(new THREE.Color());
    const prevAlpha = renderer.getClearAlpha();
    renderer.setClearColor(0x000000, 0);

    for (const def of Object.values(ITEMS)) {
      if (!def.model || !assets.has(def.model)) {
        this.cache.set(def.id, drawFallback(def.id, RARITY_COLOR[def.rarity]));
        continue;
      }
      const obj = assets.gltf(def.model).scene.clone(true);
      // Silahlar çapraz dursun
      if (def.type === 'weapon') obj.rotation.set(0, 0, -Math.PI / 4);
      if (def.type === 'shield') obj.rotation.set(0, Math.PI / 2, 0);
      scene.add(obj);
      const box = new THREE.Box3().setFromObject(obj);
      const c = box.getCenter(new THREE.Vector3());
      const r = box.getSize(new THREE.Vector3()).length() / 2;
      cam.position.copy(c).add(new THREE.Vector3(0.35, 0.35, 1).normalize().multiplyScalar(r / Math.tan((15 * Math.PI) / 180)));
      cam.lookAt(c);
      renderer.setRenderTarget(rt);
      renderer.clear();
      renderer.render(scene, cam);
      renderer.readRenderTargetPixels(rt, 0, 0, size, size, pixels);
      const img = g.createImageData(size, size);
      // WebGL alttan yukarı okur; çevir.
      for (let y = 0; y < size; y++) img.data.set(pixels.subarray((size - 1 - y) * size * 4, (size - y) * size * 4), y * size * 4);
      g.clearRect(0, 0, size, size);
      g.putImageData(img, 0, 0);
      this.cache.set(def.id, canvas.toDataURL());
      scene.remove(obj);
    }
    renderer.setRenderTarget(prevTarget);
    renderer.setClearColor(prevClear, prevAlpha);
    rt.dispose();
  }

  get(id: string): string {
    return this.cache.get(id) ?? '';
  }
}

function drawFallback(id: string, color: string): string {
  const c = document.createElement('canvas');
  c.width = c.height = 96;
  const g = c.getContext('2d')!;
  g.lineCap = 'round';
  g.lineJoin = 'round';
  if (id.includes('ring')) {
    g.strokeStyle = '#d9b44a';
    g.lineWidth = 10;
    g.beginPath(); g.arc(48, 56, 22, 0, Math.PI * 2); g.stroke();
    g.fillStyle = color;
    g.beginPath(); g.moveTo(48, 18); g.lineTo(60, 32); g.lineTo(48, 42); g.lineTo(36, 32); g.closePath(); g.fill();
    g.strokeStyle = '#fff8'; g.lineWidth = 2; g.stroke();
  } else if (id.includes('amulet')) {
    g.strokeStyle = '#c9a640'; g.lineWidth = 4;
    g.beginPath(); g.moveTo(22, 14); g.quadraticCurveTo(48, 56, 74, 14); g.stroke();
    g.fillStyle = '#d9b44a';
    g.beginPath(); g.arc(48, 60, 20, 0, Math.PI * 2); g.fill();
    g.fillStyle = '#7fe0ff';
    g.beginPath(); g.arc(48, 60, 11, 0, Math.PI * 2); g.fill();
    g.fillStyle = '#fff9'; g.beginPath(); g.arc(44, 56, 4, 0, Math.PI * 2); g.fill();
  } else {
    // kemik
    g.strokeStyle = '#e9e2cc'; g.lineWidth = 12;
    g.beginPath(); g.moveTo(28, 68); g.lineTo(68, 28); g.stroke();
    g.fillStyle = '#e9e2cc';
    for (const [x, y] of [[22, 66], [30, 74], [66, 22], [74, 30]]) { g.beginPath(); g.arc(x, y, 9, 0, Math.PI * 2); g.fill(); }
  }
  return c.toDataURL();
}
