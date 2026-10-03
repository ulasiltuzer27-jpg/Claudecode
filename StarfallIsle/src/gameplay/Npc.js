// Adalilarin sahnedeki halleri: model, basinin ustundeki gorev isareti,
// oyuncuya bakma ve konusma mesafesi.
import * as THREE from 'three';
import { AnimalModel } from '../models/Animals.js';
import { NPCS } from '../world/WorldData.js';
import { questMarkerTexture } from '../models/Collectibles.js';

const MARKER_H = { owl: 1.75, hedgehog: 1.2, frog: 1.0, bear: 2.1, rabbit: 1.55, beaver: 1.55 };

export class Npcs {
  constructor(game) {
    this.game = game;
    this.list = [];
    this.byId = new Map();
    this.markerTex = { '!': questMarkerTexture('!', '#ffb627'), '?': questMarkerTexture('?', '#4fb4ff'), '★': questMarkerTexture('★', '#b98aff') };
  }

  build() {
    for (const [id, def] of Object.entries(NPCS)) {
      if (id === 'owlPeak') continue;
      const model = new AnimalModel(def.kind);
      const npc = { id, def, model, pos: new THREE.Vector3(), yaw: def.yaw, marker: null, baseYaw: def.yaw };
      const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: this.markerTex['!'], depthTest: true, transparent: true }));
      sprite.scale.setScalar(0.55);
      sprite.visible = false;
      model.root.add(sprite);
      sprite.position.y = MARKER_H[def.kind] || 1.5;
      npc.sprite = sprite;
      this.game.renderer.scene.add(model.root);
      this.list.push(npc);
      this.byId.set(id, npc);
      this.placeAt(npc, def.x, def.z, def.yaw);
      // basit carpisma: oyuncu adalilarin icinden gecmesin
      npc.collider = this.game.physics.addCollider({ type: 'cyl', r: def.kind === 'bear' ? 0.6 : 0.4, hh: 0.6, pos: [0, 0.6, 0] }, { x: npc.pos.x, y: npc.pos.y, z: npc.pos.z, yaw: 0 });
    }
  }

  placeAt(npc, x, z, yaw) {
    const hit = this.game.physics.groundAt(x, z, 150, npc.collider || null);
    const y = hit ? hit.y : this.game.world.terrain.height(x, z);
    npc.pos.set(x, y, z);
    npc.model.root.position.copy(npc.pos);
    npc.yaw = yaw;
    npc.baseYaw = yaw;
    npc.model.root.rotation.y = yaw;
    if (npc.collider) npc.collider.setTranslation({ x, y: y + 0.6, z });
  }

  moveOwlToPeak() {
    const owl = this.byId.get('owl');
    const d = NPCS.owlPeak;
    this.placeAt(owl, d.x, d.z, d.yaw);
  }

  update(dt, player) {
    const quests = this.game.quests;
    for (const npc of this.list) {
      const d2 = npc.pos.distanceToSquared(player.pos);
      if (d2 > 140 * 140) {
        npc.model.root.visible = false;
        continue;
      }
      npc.model.root.visible = !npc.hidden;
      const near = d2 < 7 * 7;
      npc.model.lookTarget = near ? player.pos.clone().add(new THREE.Vector3(0, 0.6, 0)) : null;
      // konusurken govdesiyle de don
      if (npc.talking) {
        const want = Math.atan2(player.pos.x - npc.pos.x, player.pos.z - npc.pos.z);
        let dd = want - npc.yaw;
        dd = Math.atan2(Math.sin(dd), Math.cos(dd));
        npc.yaw += dd * Math.min(1, dt * 4);
      } else if (!npc.racing) {
        let dd = npc.baseYaw - npc.yaw;
        dd = Math.atan2(Math.sin(dd), Math.cos(dd));
        npc.yaw += dd * Math.min(1, dt * 1.5);
      }
      npc.model.root.rotation.y = npc.yaw;
      npc.model.talking = npc.talking ? 1 : 0;
      npc.model.update(dt);
      const mk = quests ? quests.markerFor(npc.id) : null;
      if (mk) {
        npc.sprite.visible = true;
        npc.sprite.material.map = this.markerTex[mk];
        npc.sprite.position.y = (MARKER_H[npc.def.kind] || 1.5) + Math.sin(performance.now() / 300) * 0.06;
      } else npc.sprite.visible = false;
    }
  }

  nearest(pos, maxDist = 2.6) {
    let best = null;
    let bd = maxDist * maxDist;
    for (const npc of this.list) {
      if (npc.hidden || npc.racing) continue;
      const d = npc.pos.distanceToSquared(pos);
      if (d < bd) { bd = d; best = npc; }
    }
    return best;
  }
}
