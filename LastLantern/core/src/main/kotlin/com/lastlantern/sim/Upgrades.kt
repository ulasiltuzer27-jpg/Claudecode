package com.lastlantern.sim

import com.lastlantern.content.Content
import com.lastlantern.content.PassiveDef
import com.lastlantern.content.WeaponDef

/** Seviye atlarken ya da sandikta sunulan tek bir secenek. */
sealed class Offer {
    class NewWeapon(val def: WeaponDef) : Offer()
    class WeaponUp(val weapon: Weapon) : Offer()
    class NewPassive(val def: PassiveDef) : Offer()
    class PassiveUp(val def: PassiveDef, val toLevel: Int) : Offer()
    class Evolve(val weapon: Weapon, val into: WeaponDef) : Offer()
    class Gold(val amount: Int) : Offer()
    class Heal(val amount: Int) : Offer()

    val icon: String
        get() = when (this) {
            is NewWeapon -> def.icon
            is WeaponUp -> weapon.def.icon
            is NewPassive -> def.icon
            is PassiveUp -> def.icon
            is Evolve -> into.icon
            is Gold -> "pick_coin"
            is Heal -> "pick_simit"
        }
}

class Upgrades(private val w: World) {
    var offers: List<Offer> = emptyList()
        private set
    var chestRewards: List<Offer> = emptyList()
        private set
    var chestGold = 0
        private set

    // ---------------------------------------------------------------- Aday listesi
    fun candidates(): MutableList<Pair<Offer, Float>> {
        val list = ArrayList<Pair<Offer, Float>>()
        for (wp in w.weapons) {
            if (!wp.evolved && !wp.maxed) list.add(Offer.WeaponUp(wp) to 1.3f)
        }
        if (w.weapons.size < Content.MAX_WEAPONS) {
            for (id in Content.baseWeapons) {
                if (!w.ownsWeapon(id)) list.add(Offer.NewWeapon(Content.weapons.getValue(id)) to 1f)
            }
        }
        for ((id, lvl) in w.passives) {
            val d = Content.passives.getValue(id)
            if (lvl < d.maxLevel) list.add(Offer.PassiveUp(d, lvl + 1) to 1.1f)
        }
        if (w.passives.size < Content.MAX_PASSIVES) {
            for (d in Content.passives.values) {
                if (d.id !in w.passives) {
                    // Sahip olunan bir silahin evrim pasifi daha sik gelir: oyuncuya
                    // evrime giden yolu gostermenin en dogal yolu.
                    val wanted = w.weapons.any { !it.evolved && it.def.evolvePassive == d.id }
                    list.add(Offer.NewPassive(d) to if (wanted) 1.6f else 0.8f)
                }
            }
        }
        return list
    }

    fun evolutionsAvailable(): List<Offer.Evolve> = w.weapons.filter { wp ->
        !wp.evolved && wp.maxed && wp.def.evolvePassive != null && wp.def.evolvePassive in w.passives
    }.map { Offer.Evolve(it, Content.weapons.getValue(it.def.evolvesTo!!)) }

    private fun pickDistinct(n: Int, pool: MutableList<Pair<Offer, Float>>): List<Offer> {
        val out = ArrayList<Offer>()
        while (out.size < n && pool.isNotEmpty()) {
            var total = 0f
            for ((_, wt) in pool) total += wt
            var r = w.rng.nextFloat() * total
            var idx = pool.size - 1
            for (k in pool.indices) {
                r -= pool[k].second
                if (r <= 0f) {
                    idx = k; break
                }
            }
            out.add(pool.removeAt(idx).first)
        }
        return out
    }

    // ---------------------------------------------------------------- Seviye
    fun rollLevelUp() {
        val n = 3 + (if (w.rng.chance(((w.stats.luck - 1f) * 0.8f).coerceIn(0f, 0.9f))) 1 else 0)
        val picked = pickDistinct(n, candidates())
        offers = picked.ifEmpty {
            listOf(Offer.Gold((20 * w.stats.greed).toInt()), Offer.Heal(30))
        }
    }

    fun reroll(): Boolean {
        if (w.rerollsLeft <= 0) return false
        w.rerollsLeft--
        rollLevelUp()
        return true
    }

    fun choose(o: Offer) {
        apply(o)
        w.pendingLevelUps--
        if (w.pendingLevelUps > 0) {
            rollLevelUp()
            w.events.emit(Ev.LEVEL_UP, w.player.x, w.player.y)
        } else {
            w.resume()
        }
    }

    fun apply(o: Offer) {
        when (o) {
            is Offer.NewWeapon -> w.addWeapon(o.def.id)
            is Offer.WeaponUp -> o.weapon.levelUp()
            is Offer.NewPassive -> w.addPassive(o.def.id)
            is Offer.PassiveUp -> w.addPassive(o.def.id)
            is Offer.Evolve -> {
                o.weapon.evolveInto(o.into)
                w.evolutions++
                w.events.emit(Ev.EVOLVE, w.player.x, w.player.y)
                w.events.emit(Ev.SFX, bv = Sfx.EVOLVE.ordinal, av = 1f)
            }
            is Offer.Gold -> w.gold += o.amount
            is Offer.Heal -> w.heal(o.amount.toFloat())
        }
    }

    // ---------------------------------------------------------------- Sandik
    fun rollChest() {
        val luck = w.stats.luck
        val r = w.rng.nextFloat()
        val count = when {
            r < 0.03f * luck -> 5
            r < 0.18f * luck -> 3
            else -> 1
        }
        val rewards = ArrayList<Offer>()
        val evos = evolutionsAvailable().toMutableList()
        repeat(count) {
            if (evos.isNotEmpty()) {
                rewards.add(evos.removeAt(0))
            } else {
                // Sandik sahip olunanlari gelistirmeyi tercih eder
                val pool = candidates().map { (o, wt) ->
                    o to when (o) {
                        is Offer.WeaponUp, is Offer.PassiveUp -> wt * 3f
                        else -> wt * 0.4f
                    }
                }.toMutableList()
                // Ayni silahi iki kez yukseltmeyi engellemek icin secileni cikar
                val pick = pickDistinct(1, pool.filter { (o, _) -> rewards.none { same(it, o) } }.toMutableList())
                if (pick.isNotEmpty()) rewards.add(pick[0])
            }
        }
        chestRewards = rewards
        chestGold = ((20 + w.rng.nextInt(30)) * w.stats.greed).toInt()
        // Oduller hemen uygulanir; ekran yalnizca gosterir.
        for (o in rewards) apply(o)
        w.gold += chestGold
    }

    private fun same(a: Offer, b: Offer): Boolean = when {
        a is Offer.WeaponUp && b is Offer.WeaponUp -> a.weapon === b.weapon
        a is Offer.PassiveUp && b is Offer.PassiveUp -> a.def === b.def
        a is Offer.NewWeapon && b is Offer.NewWeapon -> a.def === b.def
        a is Offer.NewPassive && b is Offer.NewPassive -> a.def === b.def
        else -> false
    }
}
