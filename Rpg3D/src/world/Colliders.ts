/**
 * XZ düzleminde statik engeller: daireler (ağaç, kaya, kuyu) ve döndürülmüş
 * kutular (bina, duvar). Hareketli bir daire (oyuncu, düşman) bunlardan
 * dışarı itilir. Spatial hash: her sorgu yalnızca yakın hücrelere bakar.
 *
 * Fizik motoru yok, gerek de yok: arazi bir yükseklik haritası, engeller
 * dikey prizmalar. `height` alanı zıplayarak üstüne çıkılabilen alçak
 * engeller (çit, sandık) için.
 */
export interface CircleCollider { kind: 'circle'; x: number; z: number; r: number; height: number }
export interface BoxCollider {
  kind: 'box'; x: number; z: number; hw: number; hd: number; rot: number; height: number;
  cos: number; sin: number;
}
export type Collider = CircleCollider | BoxCollider;

export class Colliders {
  private cell = 8;
  private grid = new Map<string, Collider[]>();
  readonly all: Collider[] = [];

  private key(ix: number, iz: number) {
    return `${ix},${iz}`;
  }

  private insert(c: Collider, radius: number) {
    this.all.push(c);
    const x0 = Math.floor((c.x - radius) / this.cell);
    const x1 = Math.floor((c.x + radius) / this.cell);
    const z0 = Math.floor((c.z - radius) / this.cell);
    const z1 = Math.floor((c.z + radius) / this.cell);
    for (let ix = x0; ix <= x1; ix++) {
      for (let iz = z0; iz <= z1; iz++) {
        const k = this.key(ix, iz);
        let arr = this.grid.get(k);
        if (!arr) this.grid.set(k, (arr = []));
        arr.push(c);
      }
    }
  }

  addCircle(x: number, z: number, r: number, height = 99): void {
    this.insert({ kind: 'circle', x, z, r, height }, r);
  }

  /** hw/hd: yarı genişlik/derinlik. rot: Y ekseni etrafında dönüş (radyan). */
  addBox(x: number, z: number, hw: number, hd: number, rot: number, height = 99): void {
    this.insert({ kind: 'box', x, z, hw, hd, rot, height, cos: Math.cos(rot), sin: Math.sin(rot) }, Math.hypot(hw, hd));
  }

  near(x: number, z: number, r: number): Collider[] {
    const out = new Set<Collider>();
    const x0 = Math.floor((x - r) / this.cell);
    const x1 = Math.floor((x + r) / this.cell);
    const z0 = Math.floor((z - r) / this.cell);
    const z1 = Math.floor((z + r) / this.cell);
    for (let ix = x0; ix <= x1; ix++) {
      for (let iz = z0; iz <= z1; iz++) {
        const arr = this.grid.get(this.key(ix, iz));
        if (arr) for (const c of arr) out.add(c);
      }
    }
    return [...out];
  }

  /**
   * Daireyi engellerden dışarı it. `feetY - groundY`: engelin üstündeysek
   * (zıplayıp sandığa çıkmış) itme. Döner: düzeltilmiş konum.
   */
  resolve(x: number, z: number, r: number, heightAboveGround = 0): { x: number; z: number; hit: boolean } {
    let hit = false;
    // İki tur: köşede iki engele birden değen daire tek turda birini
    // çözerken ötekine geri itilebiliyor.
    for (let pass = 0; pass < 2; pass++) {
      for (const c of this.near(x, z, r + 1)) {
        if (heightAboveGround > c.height) continue;
        if (c.kind === 'circle') {
          const dx = x - c.x;
          const dz = z - c.z;
          const d = Math.hypot(dx, dz);
          const min = r + c.r;
          if (d < min) {
            const nx = d > 1e-5 ? dx / d : 1;
            const nz = d > 1e-5 ? dz / d : 0;
            x = c.x + nx * min;
            z = c.z + nz * min;
            hit = true;
          }
        } else {
          // Kutunun yerel uzayına çevir
          const dx = x - c.x;
          const dz = z - c.z;
          const lx = dx * c.cos - dz * c.sin;
          const lz = dx * c.sin + dz * c.cos;
          const qx = Math.max(-c.hw, Math.min(c.hw, lx));
          const qz = Math.max(-c.hd, Math.min(c.hd, lz));
          const ox = lx - qx;
          const oz = lz - qz;
          const d = Math.hypot(ox, oz);
          if (d >= r) continue;
          let nlx: number;
          let nlz: number;
          if (d > 1e-5) {
            // Kutunun en yakın noktasından dışarı doğru tam r uzağa
            nlx = qx + (ox / d) * r;
            nlz = qz + (oz / d) * r;
          } else {
            // Merkez kutunun içinde: en yakın kenardan çıkar.
            const px = c.hw - Math.abs(lx);
            const pz = c.hd - Math.abs(lz);
            if (px < pz) {
              nlx = Math.sign(lx || 1) * (c.hw + r);
              nlz = lz;
            } else {
              nlx = lx;
              nlz = Math.sign(lz || 1) * (c.hd + r);
            }
          }
          // Dünya uzayına geri çevir (ters dönüş)
          x = c.x + nlx * c.cos + nlz * c.sin;
          z = c.z - nlx * c.sin + nlz * c.cos;
          hit = true;
        }
      }
    }
    return { x, z, hit };
  }

  /** Bir nokta herhangi bir engelin içinde mi (yerleştirme için). */
  blocked(x: number, z: number, r: number): boolean {
    const res = this.resolve(x, z, r, 0);
    return res.hit;
  }
}
