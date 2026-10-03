// Dunyadaki toplanabilirler: yildiz parcalari, altin tuyler, deniz kabuklari,
// havuclar ve kunduzun aletleri. Konumlar WorldData'dan, "toplandi mi"
// bilgisi kayit dosyasindan gelir.
import * as THREE from 'three';
import * as WD from '../world/WorldData.js';
import { starGeo, starMaterial, featherGeo, featherMaterial, shellGeo, carrotGeo, toolGeo } from '../models/Collectibles.js';
import { vcMaterial } from '../models/geom.js';

const PICK_R = { shard: 1.3, feather: 1.3, shell: 1.0, carrot: 1.1, tool: 1.1 };

export class Collectibles {
  constructor(game) {
    this.game = game;
    this.items = [];
    this.byId = new Map();
    this.group = new THREE.Group();
    game.renderer.scene.add(this.group);
    this.t = 0;
  }

  resolve(def, defaultDy = 1.0) {
    const W = this.game.world;
    if (def.on) {
      const a = W.anchors.get(def.on);
      if (!a) throw new Error(`Bilinmeyen capa: ${def.on} (${def.id})`);
      const p = a.clone();
      if (def.dy !== undefined && def.x === undefined) p.y += def.dy - 0.9;
      return p;
    }
    let y = def.y;
    if (y === undefined) {
      const hit = this.game.physics.groundAt(def.x, def.z, 120);
      const ground = hit ? hit.y : W.terrain.height(def.x, def.z);
      y = Math.max(ground, W.terrain.waterLevel(def.x, def.z)) + (def.dy ?? defaultDy);
    }
    return new THREE.Vector3(def.x, y, def.z);
  }

  build() {
    for (const s of WD.SHARDS) this.add('shard', s.id, this.resolve(s));
    for (const f of WD.FEATHERS) this.add('feather', f.id, this.resolve(f));
    for (const c of WD.CARROTS) this.add('carrot', c.id, this.resolve({ ...c, dy: 0.05 }));
    for (const t of WD.TOOLS) this.add('tool', t.id, this.resolve({ ...t, dy: 0.05 }), t.kind);

    // kabuklar: tek InstancedMesh (100 adet)
    this.shellDefs = [];
    WD.SHELL_GROUPS.forEach(([cx, cz, deg], gi) => {
      const a = (deg * Math.PI) / 180;
      for (let k = 0; k < 5; k++) {
        const off = (k - 2) * 1.7;
        const x = cx + Math.cos(a) * off;
        const z = cz + Math.sin(a) * off;
        const id = `sh_${String(gi * 5 + k).padStart(2, '0')}`;
        this.shellDefs.push({ id, pos: this.resolve({ id, x, z, dy: 0.02 }), yaw: a + k });
      }
    });
    this.shellMesh = new THREE.InstancedMesh(shellGeo(), vcMaterial({ flat: false, roughness: 0.4 }), this.shellDefs.length);
    this.shellMesh.castShadow = true;
    this.shellDefs.forEach((d, i) => {
      const it = { kind: 'shell', id: d.id, pos: d.pos, index: i, yaw: d.yaw, taken: false };
      this.items.push(it);
      this.byId.set(d.id, it);
    });
    this.group.add(this.shellMesh);
    this.refreshShells();
  }

  add(kind, id, pos, sub) {
    let obj;
    if (kind === 'shard') {
      obj = new THREE.Mesh(starGeo(), starMaterial);
      const glow = new THREE.Sprite(new THREE.SpriteMaterial({ map: glowTexture(), color: '#ffd76a', transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, opacity: 0.55 }));
      glow.scale.setScalar(2.2);
      obj.add(glow);
      obj.castShadow = true;
    } else if (kind === 'feather') {
      obj = new THREE.Mesh(featherGeo(), featherMaterial);
      obj.scale.setScalar(0.9);
      const glow = new THREE.Sprite(new THREE.SpriteMaterial({ map: glowTexture(), color: '#ffcf4a', transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, opacity: 0.45 }));
      glow.scale.setScalar(2.0);
      obj.add(glow);
    } else if (kind === 'carrot') {
      obj = new THREE.Mesh(carrotGeo(), vcMaterial({ flat: false }));
      obj.rotation.z = 0.5;
    } else if (kind === 'tool') {
      obj = new THREE.Mesh(toolGeo(sub), vcMaterial({ flat: false }));
      obj.rotation.z = Math.PI / 2.3;
    }
    obj.castShadow = true;
    obj.position.copy(pos);
    this.group.add(obj);
    const it = { kind, id, pos: pos.clone(), obj, taken: false, phase: Math.random() * 6, sub };
    this.items.push(it);
    this.byId.set(id, it);
    return it;
  }

  applySave(save) {
    const taken = new Set(save.collected);
    for (const it of this.items) {
      it.taken = taken.has(it.id);
      if (it.obj) it.obj.visible = !it.taken;
    }
    this.refreshShells();
  }

  refreshShells() {
    if (!this.shellMesh) return;
    const m = new THREE.Matrix4();
    const q = new THREE.Quaternion();
    const s = new THREE.Vector3(1, 1, 1);
    const zero = new THREE.Vector3(0, 0, 0);
    for (const it of this.items) {
      if (it.kind !== 'shell') continue;
      q.setFromAxisAngle(new THREE.Vector3(0, 1, 0), it.yaw);
      m.compose(it.pos, q, it.taken ? zero : s);
      this.shellMesh.setMatrixAt(it.index, m);
    }
    this.shellMesh.instanceMatrix.needsUpdate = true;
    this.shellMesh.computeBoundingSphere();
  }

  update(dt, player) {
    this.t += dt;
    const t = this.t;
    const p = player.pos;
    const center = new THREE.Vector3(p.x, p.y + 0.5, p.z);
    let shellsChanged = false;
    for (const it of this.items) {
      if (it.taken) continue;
      if (it.obj) {
        const bob = Math.sin(t * 2 + it.phase) * 0.15;
        it.obj.position.y = it.pos.y + bob;
        if (it.kind === 'shard') it.obj.rotation.y = t * 1.6 + it.phase;
        else if (it.kind === 'feather') { it.obj.rotation.y = t * 1.2 + it.phase; it.obj.rotation.z = Math.sin(t * 1.5 + it.phase) * 0.3; }
        else it.obj.rotation.y = t * 0.8 + it.phase;
      }
      const dx = it.pos.x - center.x;
      const dy = it.pos.y - center.y;
      const dz = it.pos.z - center.z;
      const r = PICK_R[it.kind] + (it.kind === 'shell' ? 0 : 0.2);
      if (dx * dx + dz * dz < r * r && Math.abs(dy) < r + 0.6) {
        it.taken = true;
        if (it.obj) it.obj.visible = false;
        if (it.kind === 'shell') shellsChanged = true;
        this.game.onCollect(it);
      }
    }
    if (shellsChanged) this.refreshShells();

    // yakin yildizlar hafif parildasin
    this._sparkT = (this._sparkT || 0) - dt;
    if (this._sparkT <= 0) {
      this._sparkT = 0.25;
      for (const it of this.items) {
        if (it.taken || (it.kind !== 'shard' && it.kind !== 'feather')) continue;
        if (it.pos.distanceToSquared(p) > 45 * 45) continue;
        this.game.particles.additive.emit({ pos: it.obj.position, count: 1, spread: 0.5, vel: { x: 0, y: 0.6, z: 0 }, velSpread: 0.2, life: 1.2, size: 0.18, color: ['#fff6b0', '#ffd76a'], alpha: 0.9 });
      }
    }
  }

  nearestUntaken(kind, from) {
    let best = null;
    let bd = Infinity;
    for (const it of this.items) {
      if (it.taken || it.kind !== kind) continue;
      const d = it.pos.distanceToSquared(from);
      if (d < bd) { bd = d; best = it; }
    }
    return best;
  }
}

let _glow = null;
export function glowTexture() {
  if (_glow) return _glow;
  const c = document.createElement('canvas');
  c.width = c.height = 64;
  const g = c.getContext('2d');
  const grd = g.createRadialGradient(32, 32, 0, 32, 32, 32);
  grd.addColorStop(0, 'rgba(255,255,255,1)');
  grd.addColorStop(0.3, 'rgba(255,255,255,0.45)');
  grd.addColorStop(1, 'rgba(255,255,255,0)');
  g.fillStyle = grd;
  g.fillRect(0, 0, 64, 64);
  _glow = new THREE.CanvasTexture(c);
  return _glow;
}
