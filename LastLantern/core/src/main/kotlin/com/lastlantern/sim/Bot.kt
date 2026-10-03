package com.lastlantern.sim

import kotlin.math.sqrt

/**
 * Basit otomatik oyuncu: denge testleri, masaustu --autoplay ve ekran
 * goruntusu senaryolari icin. Kalabaliktan kacar, guvendeyken koz toplar,
 * kart secerken once evrim, sonra silah yukseltmesi.
 */
class Bot(private val w: World, private val skill: Float = 1f) {
    var mx = 0f
        private set
    var my = 0f
        private set
    private var wanderA = 0f
    private var thinkTimer = 0f
    private val noise = Rng(w.config.seed xor 0x5EED)

    /** Beceri < 1: tepki gecikmesi, dar algi ve rastgele sapma (siradan oyuncu). */
    fun think(dt: Float) {
        thinkTimer -= dt
        if (thinkTimer > 0f) return
        thinkTimer = (1f - skill).coerceAtLeast(0f) * 0.35f
        decide(dt)
        if (skill < 1f) {
            val a = kotlin.math.atan2(my, mx) + noise.range(-1f, 1f) * (1f - skill) * 1.6f
            val l = sqrt(mx * mx + my * my)
            mx = kotlin.math.cos(a) * l
            my = kotlin.math.sin(a) * l
        }
    }

    private fun decide(dt: Float) {
        val p = w.player
        val e = w.enemies
        var fx = 0f
        var fy = 0f
        var danger = 0f
        val sense = 110f * skill
        for (i in 0 until e.high) {
            if (!e.active[i]) continue
            val dx = p.x - e.x[i]
            val dy = p.y - e.y[i]
            val d2 = dx * dx + dy * dy
            if (d2 > sense * sense) continue
            val d = sqrt(d2).coerceAtLeast(1f)
            val wgt = (e.damage[i] + 4f) / (d * d)
            fx += dx / d * wgt
            fy += dy / d * wgt
            if (d < 40f) danger += 1f
        }
        val es = w.enemyShots
        for (i in 0 until es.high) {
            if (!es.active[i]) continue
            val dx = p.x - es.x[i]
            val dy = p.y - es.y[i]
            val d2 = dx * dx + dy * dy
            if (d2 > 60f * 60f) continue
            val d = sqrt(d2).coerceAtLeast(1f)
            fx += dx / d * 8f / (d * d)
            fy += dy / d * 8f / (d * d)
        }
        // Guvendeyse en yakin koze/sandiga yonel
        if (danger < 2f) {
            var best = -1
            var bestD = 160f * 160f
            val pk = w.pickups
            for (i in 0 until pk.high) {
                if (!pk.active[i]) continue
                val dx = pk.x[i] - p.x
                val dy = pk.y[i] - p.y
                val d2 = dx * dx + dy * dy
                if (d2 < bestD) {
                    bestD = d2; best = i
                }
            }
            if (best >= 0) {
                val dx = pk.x[best] - p.x
                val dy = pk.y[best] - p.y
                val d = sqrt(bestD).coerceAtLeast(1f)
                val pull = 0.02f * skill
                fx += dx / d * pull
                fy += dy / d * pull
            }
        }
        val l = sqrt(fx * fx + fy * fy)
        if (l < 1e-5f) {
            wanderA += dt * 0.4f
            mx = kotlin.math.cos(wanderA) * 0.5f
            my = kotlin.math.sin(wanderA) * 0.5f
        } else {
            mx = fx / l
            my = fy / l
        }
    }

    fun chooseOffer(offers: List<Offer>): Offer = offers.minByOrNull {
        when (it) {
            is Offer.Evolve -> 0
            is Offer.WeaponUp -> 1
            is Offer.NewWeapon -> if (w.weapons.size < 4) 1 else 3
            is Offer.PassiveUp -> 2
            is Offer.NewPassive -> 3
            is Offer.Heal -> 4
            is Offer.Gold -> 5
        }
    }!!

    /** Durum makinesini otomatik ilerletir (level-up/sandik). false: run bitti. */
    fun step(dt: Float): Boolean {
        w.events.clear()
        when (w.state) {
            RunState.PLAYING -> {
                think(dt)
                w.update(dt, mx, my)
            }
            RunState.LEVEL_UP -> w.upgrades.choose(chooseOffer(w.upgrades.offers))
            RunState.CHEST -> w.resume()
            RunState.DEAD -> w.giveUp()
            RunState.VICTORY, RunState.DEFEAT -> return false
        }
        return true
    }
}
