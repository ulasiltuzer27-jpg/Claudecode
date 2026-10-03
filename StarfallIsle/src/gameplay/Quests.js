// Gorevler ve adalilarin ne soyleyecegi. Tum metinler i18n'de; burada
// yalnizca "hangi durumda hangi anahtar" ve odul mantigi var.
import { SHOP_ITEMS, CARROTS, TOOLS, FISH, SHARDS_FOR_FINALE, SHARD_TOTAL, FEATHER_TOTAL } from '../world/WorldData.js';
import { t } from '../core/i18n.js';

export class Quests {
  constructor(game) {
    this.game = game;
  }

  get save() {
    return this.game.save;
  }

  has(id) {
    return this.save.collected.includes(id);
  }

  rewarded(id) {
    return this.save.rewards.includes(id);
  }

  carrots() {
    return CARROTS.filter((c) => this.has(c.id)).length;
  }

  tools() {
    return TOOLS.filter((c) => this.has(c.id)).length;
  }

  fishTotal() {
    return Object.values(this.save.fish).reduce((a, b) => a + b, 0);
  }

  species() {
    return FISH.filter((f) => this.save.fish[f.id] > 0).length;
  }

  questsDone() {
    return Object.values(this.save.quests).filter((s) => s === 'done').length;
  }

  // Basin ustundeki isaret: '!' yeni bir sey var, '?' teslim edilebilir.
  markerFor(id) {
    const s = this.save;
    if (!s) return null;
    const p = this.game.progress;
    switch (id) {
      case 'owl':
        if (!s.flags.owlIntro) return '!';
        if (!s.flags.finale && p.shardCount() >= SHARDS_FOR_FINALE) return '★';
        return null;
      case 'hedgehog':
        return s.talked.includes('hedgehog') ? null : '!';
      case 'rabbit':
        if (s.quests.rabbit === 'none') return '!';
        if (s.quests.rabbit === 'active' && this.carrots() >= 3) return '?';
        return null;
      case 'beaver':
        if (s.quests.beaver === 'none') return '!';
        if (s.quests.beaver === 'active' && this.tools() >= 3) return '?';
        return null;
      case 'frog':
        return s.quests.frog === 'done' ? null : '!';
      case 'bear':
        if (!s.flags.rod) return s.talked.includes('bear') ? null : '!';
        if (s.quests.bear === 'none') return '!';
        if (!this.rewarded('q_bear') && this.fishTotal() >= 3) return '?';
        if (!this.rewarded('bear_feather') && this.species() >= FISH.length) return '?';
        return null;
      default:
        return null;
    }
  }

  talk(npc) {
    const s = this.save;
    const g = this.game;
    if (!s.talked.includes(npc.id)) {
      s.talked.push(npc.id);
      g.stats.max('npcs_max', s.talked.length);
    }
    const say = (key, params, then) => g.dialogue.start({ npc, lines: t(key, params), onEnd: then });
    const ask = (key, params, choices) => g.dialogue.start({ npc, lines: t(key, params), choices });

    switch (npc.id) {
      case 'owl': return this.talkOwl(npc, say, ask);
      case 'hedgehog': return this.talkShop(npc, say, ask);
      case 'rabbit': {
        const q = s.quests.rabbit;
        if (q === 'none') return say('dlg.rabbit.ask', null, () => { s.quests.rabbit = 'active'; g.autosave(); });
        if (q === 'active') {
          const n = this.carrots();
          if (n >= 3) return say('dlg.rabbit.thanks', null, () => this.finishQuest('rabbit', 'rabbit_feather', 'feather'));
          return say('dlg.rabbit.progress', { n });
        }
        return say('dlg.rabbit.done');
      }
      case 'beaver': {
        const q = s.quests.beaver;
        if (q === 'none') return say('dlg.beaver.ask', null, () => { s.quests.beaver = 'active'; g.autosave(); });
        if (q === 'active') {
          const n = this.tools();
          if (n >= 3) {
            return say('dlg.beaver.thanks', null, () => {
              s.flags.bridge = true;
              g.world.buildBridge(true);
              g.events.emit('bridge');
              this.finishQuest('beaver', 'q_beaver', 'shard');
            });
          }
          return say('dlg.beaver.progress', { n });
        }
        return say('dlg.beaver.done');
      }
      case 'frog': {
        const won = s.quests.frog === 'done';
        return ask(won ? 'dlg.frog.rematch' : 'dlg.frog.challenge', null, [
          { label: t('choice.race'), action: () => g.race.start(npc) },
          { label: t('choice.later'), action: () => {} },
        ]);
      }
      case 'bear': return this.talkBear(npc, say);
      default:
        return null;
    }
  }

  talkOwl(npc, say, ask) {
    const s = this.save;
    const g = this.game;
    const p = g.progress;
    if (!s.flags.owlIntro) {
      return say('dlg.owl.intro', null, () => {
        s.flags.owlIntro = true;
        g.grantReward('owl_feather', 'feather');
        const params = { jump: g.input.glyph('jump') };
        g.dialogue.start({
          npc, lines: t('dlg.owl.introAfter', params), onEnd: () => {
            g.flyOwlToPeak();
          },
        });
      });
    }
    if (s.flags.finale) {
      return say('dlg.owl.after', { shards: p.shardCount(), total: SHARD_TOTAL, feathers: p.featherCount(), ftotal: FEATHER_TOTAL });
    }
    const n = p.shardCount();
    if (n < SHARDS_FOR_FINALE) return say('dlg.owl.waiting', { n, need: SHARDS_FOR_FINALE - n, goal: SHARDS_FOR_FINALE });
    return ask('dlg.owl.ready', { n }, [
      { label: t('choice.light'), action: () => g.finale.start() },
      { label: t('choice.notYet'), action: () => {} },
    ]);
  }

  talkShop(npc, say, ask) {
    const s = this.save;
    const g = this.game;
    const first = !s.flags.shopVisited;
    s.flags.shopVisited = true;
    const open = () => {
      const choices = [];
      for (const item of SHOP_ITEMS) {
        if (this.soldOut(item.id)) continue;
        choices.push({
          label: t('shop.item', { name: t(`shop.${item.id}`), price: item.price }),
          action: () => this.buy(npc, item),
        });
      }
      if (s.flags.hat) {
        choices.push({ label: t(s.flags.hatOn ? 'shop.hatOff' : 'shop.hatOn'), action: () => { s.flags.hatOn = !s.flags.hatOn; g.player.model.setHat(s.flags.hatOn); g.autosave(); } });
      }
      choices.push({ label: t('choice.bye'), action: () => {} });
      g.dialogue.start({ npc, lines: t(choices.length > 1 ? 'dlg.hedgehog.menu' : 'dlg.hedgehog.soldOut', { shells: s.shells }), choices });
    };
    if (first) return say('dlg.hedgehog.hello', null, open);
    return open();
  }

  soldOut(id) {
    const s = this.save;
    if (id === 'rod') return !!s.flags.rod;
    if (id === 'feather') return this.rewarded('shop_feather');
    if (id === 'shard') return this.rewarded('q_shop');
    if (id === 'hat') return !!s.flags.hat;
    return true;
  }

  buy(npc, item) {
    const s = this.save;
    const g = this.game;
    if (s.shells < item.price) {
      g.dialogue.start({ npc, lines: t('dlg.hedgehog.poor', { need: item.price - s.shells }) });
      g.audio.sfx('error');
      return;
    }
    s.shells -= item.price;
    if (item.id === 'rod') s.flags.rod = true;
    if (item.id === 'hat') { s.flags.hat = true; s.flags.hatOn = true; g.player.model.setHat(true); }
    if (item.id === 'feather') g.grantReward('shop_feather', 'feather');
    if (item.id === 'shard') g.grantReward('q_shop', 'shard');
    g.audio.sfx('buy');
    g.events.emit('shells', s.shells);
    g.autosave();
    g.dialogue.start({ npc, lines: t(`dlg.hedgehog.bought.${item.id}`) });
  }

  talkBear(npc, say) {
    const s = this.save;
    if (!s.flags.rod) return say('dlg.bear.norod');
    if (s.quests.bear === 'none') {
      return say('dlg.bear.teach', { key: this.game.input.glyph('interact') }, () => { s.quests.bear = 'active'; this.game.autosave(); });
    }
    if (!this.rewarded('q_bear') && this.fishTotal() >= 3) {
      return say('dlg.bear.reward3', null, () => this.finishQuest('bear', 'q_bear', 'shard'));
    }
    if (!this.rewarded('bear_feather') && this.species() >= FISH.length) {
      return say('dlg.bear.reward6', null, () => this.game.grantReward('bear_feather', 'feather'));
    }
    return say('dlg.bear.progress', { n: this.fishTotal(), species: this.species(), total: FISH.length });
  }

  finishQuest(id, rewardId, kind) {
    this.save.quests[id] = 'done';
    this.game.grantReward(rewardId, kind);
    this.game.stats.max('quests_max', this.questsDone());
    this.game.events.emit('questDone', id);
    this.game.autosave();
  }
}
