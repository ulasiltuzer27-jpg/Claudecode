import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';

/**
 * Statik dünya birleştirici.
 *
 * KayKit'in her paketi tek bir doku atlası kullanıyor (köy binaları,
 * harabe parçaları, ağaçlar). Aynı atlası kullanan statik nesneleri
 * 64x64'lük parça (chunk) başına TEK bir mesh'te birleştirmek, yüzlerce
 * draw call'u birkaç düzineye indiriyor. Ölçüldü: köyden bakınca 441 ->
 * ~80 çağrı. Gerçek GPU'da da zayıf dizüstülerde fark ediliyor, yazılım
 * render'ında (headless test) ise oyunu oynanabilir kılan şey bu.
 *
 * Her parça kendi sınır küresine sahip, yani kamera ve gölge kamerası
 * uzaktakileri eler; ayrıca mesafe eleme (çimen 70, ağaç 260 birim).
 */
/**
 * mergeGeometries uyumsuz öznitelikte console.error basıp null döner; önce
 * kendimiz denetleyelim: ortak öznitelikler aynı tip ve boyutta mı.
 */
function compatible(geoms: THREE.BufferGeometry[], attrs: string[]): boolean {
  const f = geoms[0];
  return attrs.every((a) => {
    const ref = f.attributes[a] as THREE.BufferAttribute;
    return geoms.every((g) => {
      const x = g.attributes[a] as THREE.BufferAttribute;
      return x.itemSize === ref.itemSize && x.normalized === ref.normalized && x.array.constructor === ref.array.constructor;
    });
  });
}

interface Group {
  material: THREE.Material;
  geoms: THREE.BufferGeometry[];
  castShadow: boolean;
  maxDist: number;
  cx: number;
  cz: number;
}

export interface BatchChunk { mesh: THREE.Mesh; center: THREE.Vector3; maxDist: number }

export class StaticBatcher {
  static readonly CHUNK = 64;
  private groups = new Map<string, Group>();
  private canonical = new Map<string, THREE.Material>();
  readonly chunks: BatchChunk[] = [];
  private tmp = new THREE.Matrix4();
  added = 0;

  /**
   * Materyal anahtarı: paket + materyal adı. Aynı paketteki modeller aynı
   * atlası taşıyor ama her .glb kendi Material/Texture kopyasını yaratıyor;
   * ilk görüleni hepsi için kullanıyoruz.
   */
  private materialFor(pack: string, m: THREE.Material): { key: string; mat: THREE.Material } {
    const std = m as THREE.MeshStandardMaterial;
    const img = std.map?.image as { width?: number; height?: number } | undefined;
    const key = `${pack}:${m.name}:${img?.width ?? 0}x${img?.height ?? 0}:${m.transparent ? 't' : 'o'}`;
    let mat = this.canonical.get(key);
    if (!mat) this.canonical.set(key, (mat = m));
    return { key, mat };
  }

  /** Dünyaya yerleştirilmiş bir nesneyi (matrixWorld hazır) birleştirmeye ekle. */
  addObject(pack: string, obj: THREE.Object3D, opts: { castShadow?: boolean; maxDist?: number } = {}): void {
    obj.updateMatrixWorld(true);
    obj.traverse((o) => {
      const mesh = o as THREE.Mesh;
      if (!mesh.isMesh || (mesh as THREE.SkinnedMesh).isSkinnedMesh) return;
      this.push(pack, mesh.geometry, mesh.material as THREE.Material, mesh.matrixWorld, opts);
    });
  }

  /** Bir modelin (sahne kökü birim matris) bir kopyasını verilen dönüşümle ekle. */
  addInstance(pack: string, template: THREE.Object3D, matrix: THREE.Matrix4, opts: { castShadow?: boolean; maxDist?: number } = {}): void {
    template.updateMatrixWorld(true);
    template.traverse((o) => {
      const mesh = o as THREE.Mesh;
      if (!mesh.isMesh) return;
      this.tmp.multiplyMatrices(matrix, mesh.matrixWorld);
      this.push(pack, mesh.geometry, mesh.material as THREE.Material, this.tmp, opts);
    });
  }

  private push(pack: string, geo: THREE.BufferGeometry, material: THREE.Material, matrix: THREE.Matrix4, opts: { castShadow?: boolean; maxDist?: number }): void {
    const { key, mat } = this.materialFor(pack, Array.isArray(material) ? material[0] : material);
    const pos = new THREE.Vector3().setFromMatrixPosition(matrix);
    const C = StaticBatcher.CHUNK;
    const cx = Math.floor(pos.x / C);
    const cz = Math.floor(pos.z / C);
    const castShadow = opts.castShadow ?? true;
    const maxDist = opts.maxDist ?? 400;
    const gk = `${key}|${cx},${cz}|${castShadow ? 1 : 0}|${maxDist}`;
    let g = this.groups.get(gk);
    if (!g) this.groups.set(gk, (g = { material: mat, geoms: [], castShadow, maxDist, cx, cz }));
    const copy = geo.clone();
    copy.applyMatrix4(matrix);
    g.geoms.push(copy);
    this.added++;
  }

  build(parent: THREE.Object3D): void {
    const C = StaticBatcher.CHUNK;
    for (const g of this.groups.values()) {
      // Birleştirme için bütün geometriler aynı öznitelikleri taşımalı.
      const common = Object.keys(g.geoms[0].attributes).filter((a) => g.geoms.every((x) => x.attributes[a]));
      const indexed = g.geoms.every((x) => x.index);
      for (const x of g.geoms) {
        for (const a of Object.keys(x.attributes)) if (!common.includes(a)) x.deleteAttribute(a);
        x.morphAttributes = {};
      }
      const list = indexed ? g.geoms : g.geoms.map((x) => (x.index ? x.toNonIndexed() : x));
      if (!compatible(list, common)) continue;
      const merged = mergeGeometries(list, false);
      for (const x of g.geoms) x.dispose();
      if (!merged) continue;
      merged.computeBoundingSphere();
      const mesh = new THREE.Mesh(merged, g.material);
      mesh.castShadow = g.castShadow;
      mesh.receiveShadow = true;
      mesh.matrixAutoUpdate = false;
      mesh.name = 'batch';
      parent.add(mesh);
      this.chunks.push({ mesh, center: new THREE.Vector3((g.cx + 0.5) * C, 0, (g.cz + 0.5) * C), maxDist: g.maxDist });
    }
    this.groups.clear();
  }

  /** Teşhis: bütün birleşik parçaları gizle. */
  hidden = false;

  cull(cam: THREE.Vector3): void {
    const half = StaticBatcher.CHUNK * 0.71;
    for (const c of this.chunks) c.mesh.visible = !this.hidden && Math.hypot(c.center.x - cam.x, c.center.z - cam.z) - half < c.maxDist;
  }
}

/**
 * Karakter şablonundaki gövde parçalarını (kol, bacak, gövde, baş...) tek
 * SkinnedMesh'te birleştir. Hepsi aynı iskelet ve materyale bağlı;
 * ayrı tutmanın tek sonucu karakter başına 6-9 draw call. Şablonda bir kez
 * yapılır, SkeletonUtils.clone kopyaları birleşik mesh'i miras alır.
 */
export function mergeSkinnedParts(root: THREE.Object3D): number {
  const byParent = new Map<THREE.Object3D, THREE.SkinnedMesh[]>();
  root.traverse((o) => {
    const sm = o as THREE.SkinnedMesh;
    if (!sm.isSkinnedMesh || !sm.parent) return;
    let arr = byParent.get(sm.parent);
    if (!arr) byParent.set(sm.parent, (arr = []));
    arr.push(sm);
  });
  let merged = 0;
  for (const [parent, parts] of byParent) {
    const byMat = new Map<THREE.Material, THREE.SkinnedMesh[]>();
    for (const p of parts) {
      const m = p.material as THREE.Material;
      let arr = byMat.get(m);
      if (!arr) byMat.set(m, (arr = []));
      arr.push(p);
    }
    for (const [mat, list] of byMat) {
      if (list.length < 2) continue;
      const first = list[0];
      // Aynı bind matrisi ve aynı kemik dizisi şart; değilse dokunma.
      const same = list.every((p) => p.bindMatrix.equals(first.bindMatrix) && p.skeleton.bones.length === first.skeleton.bones.length
        && p.skeleton.bones.every((b, i) => b === first.skeleton.bones[i]) && p.position.equals(first.position) && p.quaternion.equals(first.quaternion));
      if (!same) continue;
      const common = Object.keys(first.geometry.attributes).filter((a) => list.every((p) => p.geometry.attributes[a]));
      const geoms = list.map((p) => {
        const g = p.geometry.clone();
        for (const a of Object.keys(g.attributes)) if (!common.includes(a)) g.deleteAttribute(a);
        g.morphAttributes = {};
        return g;
      });
      if (!compatible(geoms, common) || !geoms.every((g) => !!g.index === !!geoms[0].index)) continue;
      const geo = mergeGeometries(geoms, false);
      if (!geo) continue;
      const mesh = new THREE.SkinnedMesh(geo, mat);
      mesh.name = `${first.name}_merged`;
      mesh.position.copy(first.position);
      mesh.quaternion.copy(first.quaternion);
      mesh.scale.copy(first.scale);
      mesh.bind(first.skeleton, first.bindMatrix);
      mesh.castShadow = mesh.receiveShadow = true;
      for (const p of list) parent.remove(p);
      parent.add(mesh);
      merged += list.length - 1;
    }
  }
  return merged;
}
