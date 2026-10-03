// Rapier sarmalayicisi: statik dunya (heightfield + yapilar) ve oyuncunun
// kinematik karakter kontrolcusu. Dinamik govde yok; dunya tamamen statik
// oldugu icin step() yalnizca sorgu yapisini guncellemeye yarar.
import RAPIER from '@dimforge/rapier3d-compat';
import { VERTS, CELLS, WORLD_SIZE } from '../world/Terrain.js';

let ready = null;
export function initPhysics() {
  if (!ready) ready = RAPIER.init();
  return ready;
}

export class Physics {
  constructor() {
    this.R = RAPIER;
    this.world = new RAPIER.World({ x: 0, y: 0, z: 0 });
    this.world.timestep = 1 / 60;
    this.tags = new Map(); // collider handle -> etiket ('bounce', 'wood' ...)
  }

  addTerrain(terrain) {
    // Rapier heightfield: sutun-oncelikli, sutun = x, satir = z.
    // (tools/verify.mjs bu yonelimi isinlarla dogruluyor.)
    const n = VERTS;
    const h = new Float32Array(n * n);
    for (let ix = 0; ix < n; ix++) {
      for (let iz = 0; iz < n; iz++) h[ix * n + iz] = terrain.heights[iz * n + ix];
    }
    const desc = this.R.ColliderDesc.heightfield(CELLS, CELLS, h, { x: WORLD_SIZE, y: 1, z: WORLD_SIZE });
    desc.setFriction(0.8);
    this.terrainCollider = this.world.createCollider(desc);
    this.tags.set(this.terrainCollider.handle, 'ground');
  }

  // d: { type:'box'|'cyl'|'ball'|'hull', ... pos:[x,y,z], rot:[x,y,z], order }
  // world: { x, y, z, yaw } — yerel tanimi dunyaya tasiyan donusum
  addCollider(d, xf = { x: 0, y: 0, z: 0, yaw: 0 }, tag = null) {
    const R = this.R;
    let desc;
    if (d.type === 'box') desc = R.ColliderDesc.cuboid(d.hx, d.hy, d.hz);
    else if (d.type === 'cyl') desc = R.ColliderDesc.cylinder(d.hh, d.r);
    else if (d.type === 'ball') desc = R.ColliderDesc.ball(d.r);
    else if (d.type === 'hull') {
      desc = R.ColliderDesc.convexHull(new Float32Array(d.points));
      if (!desc) return null;
    } else return null;

    const cy = Math.cos(xf.yaw || 0);
    const sy = Math.sin(xf.yaw || 0);
    const p = d.pos || [0, 0, 0];
    const wx = xf.x + p[0] * cy + p[2] * sy;
    const wz = xf.z - p[0] * sy + p[2] * cy;
    const wy = xf.y + p[1];
    desc.setTranslation(wx, wy, wz);
    const rot = d.rot || [0, 0, 0];
    const q = eulerToQuat(rot[0], rot[1] + (xf.yaw || 0), rot[2], d.order || 'YXZ');
    desc.setRotation(q);
    desc.setFriction(0.8);
    const col = this.world.createCollider(desc);
    if (tag) this.tags.set(col.handle, tag);
    return col;
  }

  removeCollider(col) {
    if (!col) return;
    this.tags.delete(col.handle);
    this.world.removeCollider(col, false);
  }

  createCharacter(radius, halfHeight, pos) {
    const R = this.R;
    const body = this.world.createRigidBody(R.RigidBodyDesc.kinematicPositionBased().setTranslation(pos.x, pos.y, pos.z));
    const collider = this.world.createCollider(R.ColliderDesc.capsule(halfHeight, radius), body);
    const ctrl = this.world.createCharacterController(0.03);
    ctrl.setUp({ x: 0, y: 1, z: 0 });
    ctrl.setMaxSlopeClimbAngle((48 * Math.PI) / 180);
    ctrl.setMinSlopeSlideAngle((52 * Math.PI) / 180);
    ctrl.enableAutostep(0.35, 0.2, false);
    ctrl.enableSnapToGround(0.35);
    ctrl.setSlideEnabled(true);
    ctrl.setApplyImpulsesToDynamicBodies(false);
    return { body, collider, ctrl };
  }

  step() {
    this.world.step();
  }

  // Asagi dogru isin: zemin yuksekligi (yapilar dahil). Yoksa null.
  groundAt(x, z, fromY = 200, exclude = null) {
    const ray = new this.R.Ray({ x, y: fromY, z }, { x: 0, y: -1, z: 0 });
    const hit = this.world.castRay(ray, fromY + 50, true, undefined, undefined, exclude || undefined);
    if (!hit) return null;
    return { y: fromY - hit.timeOfImpact, collider: hit.collider };
  }

  raycast(origin, dir, maxDist, exclude = null) {
    const ray = new this.R.Ray(origin, dir);
    const hit = this.world.castRay(ray, maxDist, true, undefined, undefined, exclude || undefined);
    return hit ? hit.timeOfImpact : null;
  }

  pointInside(p, exclude = null) {
    let inside = false;
    this.world.intersectionsWithPoint(p, () => { inside = true; return false; }, undefined, undefined, exclude || undefined);
    return inside;
  }
}

export function eulerToQuat(x, y, z, order = 'YXZ') {
  const c1 = Math.cos(x / 2), c2 = Math.cos(y / 2), c3 = Math.cos(z / 2);
  const s1 = Math.sin(x / 2), s2 = Math.sin(y / 2), s3 = Math.sin(z / 2);
  // three.js Euler -> Quaternion ile ayni formuller
  switch (order) {
    case 'XYZ':
      return { x: s1 * c2 * c3 + c1 * s2 * s3, y: c1 * s2 * c3 - s1 * c2 * s3, z: c1 * c2 * s3 + s1 * s2 * c3, w: c1 * c2 * c3 - s1 * s2 * s3 };
    case 'YXZ':
    default:
      return { x: s1 * c2 * c3 + c1 * s2 * s3, y: c1 * s2 * c3 - s1 * c2 * s3, z: c1 * c2 * s3 - s1 * s2 * c3, w: c1 * c2 * c3 + s1 * s2 * s3 };
  }
}
