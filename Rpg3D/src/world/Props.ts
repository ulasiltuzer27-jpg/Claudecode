import * as THREE from 'three';
import type { Assets } from '../core/Assets';

/**
 * Paketler farklı ölçeklerde: Medieval Hexagon minyatür (ev 0.9 birim),
 * Dungeon ve karakterler 1:1 (duvar 4, karakter 2.4), Forest Nature biraz
 * küçük. Burada hepsi karakter ölçeğine çekiliyor.
 */
const HEX_BUILDING = 6.5;
export const PROP_SCALE: Record<string, number> = {
  b_home_A: HEX_BUILDING, b_home_B: HEX_BUILDING, b_tavern: HEX_BUILDING, b_blacksmith: HEX_BUILDING,
  b_market: HEX_BUILDING, b_well: 4.2, b_windmill: HEX_BUILDING, b_church: HEX_BUILDING, b_tower_A: HEX_BUILDING,
  b_destroyed: HEX_BUILDING,
  p_barrel: 5, p_crate_a_big: 5, p_crate_b_small: 5, p_sack: 5, p_tent: 6.5, p_weaponrack: 5, p_wheelbarrow: 5,
  p_resource_lumber: 5, p_flag_red: 5, p_target: 4.5, p_bucket_water: 5,
  p_fence_wood_straight: 3.4, p_fence_stone_straight: 3.4,
  d_chest: 0.75, d_chest_gold: 0.75, d_barrel_large: 0.8, d_keg: 0.8, d_box_large: 0.8,
  d_coin_stack_small: 0.7, d_coin: 0.6, d_bottle_a_green: 0.6, d_bottle_a_labeled_brown: 0.6, d_bottle_c_brown: 0.6,
  n_tree_1_a: 2.3, n_tree_1_b: 2.3, n_tree_2_a: 2.3, n_tree_2_c: 2.3, n_tree_3_a: 2.4, n_tree_4_a: 2.3,
  n_tree_5_a: 2.4, n_tree_6_a: 2.3, n_tree_bare_1_a: 2.2, n_tree_bare_2_a: 2.2,
  n_bush_1_a: 6, n_bush_2_a: 6, n_bush_4_a: 6,
  n_rock_1_a: 2.2, n_rock_1_j: 1.4, n_rock_2_a: 2.2, n_rock_3_a: 2.2, n_rock_3_e: 2.2,
  n_grass_1_a: 1.6, n_grass_2_a: 1.6,
};

export const scaleOf = (id: string): number => PROP_SCALE[id] ?? 1;

export interface Placement { x: number; y: number; z: number; rot: number; scale: number }

/** Model kimliğinden doku atlası paketi: aynı paketteki modeller birleştirilebilir. */
export function packOf(id: string): string {
  if (id.startsWith('b_') || id.startsWith('p_')) return 'hex';
  if (id.startsWith('d_')) return 'dungeon';
  if (id.startsWith('n_')) return 'nature';
  return id;
}

/** Bir model için yerleşim matrisi. */
export function placementMatrix(id: string, p: Placement, out = new THREE.Matrix4()): THREE.Matrix4 {
  return out.compose(
    new THREE.Vector3(p.x, p.y, p.z),
    new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), p.rot),
    new THREE.Vector3().setScalar(p.scale * scaleOf(id)),
  );
}

/** Tek bir modelin kopyasını yerleştir (bina, sandık gibi az sayıdaki nesneler). */
export function placeModel(assets: Assets, id: string, p: Placement, parent: THREE.Object3D): THREE.Object3D {
  const obj = assets.gltf(id).scene.clone(true);
  obj.position.set(p.x, p.y, p.z);
  obj.rotation.y = p.rot;
  obj.scale.setScalar(p.scale * scaleOf(id));
  obj.name = id;
  parent.add(obj);
  obj.updateMatrixWorld(true);
  // Statik: her kare matris hesaplamasına gerek yok.
  obj.traverse((o) => { o.matrixAutoUpdate = false; });
  return obj;
}

