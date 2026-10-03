package com.lastlantern.sim

import com.lastlantern.content.Behavior
import com.lastlantern.content.EnemyDef
import kotlin.math.cos
import kotlin.math.sin

/**
 * Ara boss ve safak bosslarinin saldiri desenleri. Her desen once bir
 * "uyari" (WINDUP) asamasindan gecer: oyuncu ne geldigini gorur ve kacabilir.
 * Mobilde adil hissettiren sey budur; uyarisiz hasar telefonda sinir bozar.
 */
object Bosses {
    fun update(w: World, i: Int, d: EnemyDef, dt: Float, dx: Float, dy: Float, dist: Float, spd: Float) {
        val e = w.enemies
        Behaviors.outX = dx * spd
        Behaviors.outY = dy * spd
        e.timer2[i] += dt
        when (d.behavior) {
            Behavior.MOTH_MATRON -> {
                val wob = sin(e.anim[i] * 2.2f) * 0.8f
                val c = cos(wob)
                val s = sin(wob)
                Behaviors.outX = (dx * c - dy * s) * spd
                Behaviors.outY = (dx * s + dy * c) * spd
                if (e.timer2[i] > 4f) {
                    e.timer2[i] = 0f
                    radial(w, i, 10, d.shotSpeed.coerceAtLeast(70f), 8f, e.anim[i])
                }
                e.timer[i] += dt
                if (e.timer[i] > 7f) {
                    e.timer[i] = 0f
                    summon(w, i, "bat", 6, 26f)
                }
            }
            Behavior.MIRE_MOTHER -> {
                Behaviors.dash(w, i, dt, dx, dy, dist, spd, 140f, 4.2f, 4.5f, windup = 0.8f, dashTime = 0.5f)
                // Can esiklerinde bolunme
                val ratio = e.hp[i] / e.maxHp[i]
                val target = if (ratio < 0.33f) 2 else if (ratio < 0.66f) 1 else 0
                if (target > e.phase[i]) {
                    e.phase[i] = target
                    summon(w, i, "slime", 6, 30f)
                }
                if (e.timer2[i] > 5f) {
                    e.timer2[i] = 0f
                    radial(w, i, 8, 60f, 9f, 0f)
                }
            }
            Behavior.FROST_ALPHA -> {
                Behaviors.dash(w, i, dt, dx, dy, dist, spd, 150f, 4f, 2.6f, windup = 0.6f, dashTime = 0.5f)
                if (e.timer2[i] > 9f) {
                    e.timer2[i] = 0f
                    summon(w, i, "wolf", 4, 34f)
                }
            }
            Behavior.STAG -> {
                Behaviors.dash(w, i, dt, dx, dy, dist, spd, 260f, 5f, 3.5f, windup = 0.9f, dashTime = 0.7f)
                if (e.timer2[i] > 6.5f && e.state[i] == EState.NORMAL) {
                    e.timer2[i] = 0f
                    radial(w, i, 14, d.shotSpeed, d.shotDamage, e.anim[i] * 0.7f)
                }
            }
            Behavior.BELLKEEPER -> {
                if (e.timer2[i] > 4.2f) {
                    e.timer2[i] = 0f
                    // Bosluklu halka: oyuncu araliktan gecebilir
                    val n = 18
                    val gap = (e.anim[i] * 3f).toInt() % n
                    for (k in 0 until n) {
                        if (k == gap || k == (gap + 1) % n || k == (gap + 9) % n || k == (gap + 10) % n) continue
                        val a = k * Rng.TAU / n
                        w.enemyShoot(e.x[i], e.y[i] + 8f, cos(a), sin(a), d.shotSpeed, d.shotDamage * w.stage.dmgMult, "proj_bolt")
                    }
                    w.events.emit(Ev.SFX, e.x[i], e.y[i], 0.7f, Sfx.BELL.ordinal)
                    w.events.emit(Ev.SPLASH, e.x[i], e.y[i], 60f)
                }
                e.timer[i] += dt
                if (e.timer[i] > 10f) {
                    e.timer[i] = 0f
                    summon(w, i, "drowned", 6, 40f)
                }
            }
            Behavior.WYRM -> {
                val wob = sin(e.anim[i] * 1.6f) * 0.9f
                val c = cos(wob)
                val s = sin(wob)
                Behaviors.outX = (dx * c - dy * s) * spd
                Behaviors.outY = (dx * s + dy * c) * spd
                if (e.timer2[i] > 3f && dist < 260f) {
                    e.timer2[i] = 0f
                    // Buz nefesi: oyuncuya dogru yelpaze
                    val base = kotlin.math.atan2(dy, dx)
                    for (k in -2..2) {
                        val a = base + k * 0.16f
                        w.enemyShoot(e.x[i] + dx * 20f, e.y[i] + dy * 20f, cos(a), sin(a), d.shotSpeed, d.shotDamage * w.stage.dmgMult, d.shotSprite)
                    }
                    w.events.emit(Ev.SFX, e.x[i], e.y[i], 0.6f, Sfx.ENEMY_SHOOT.ordinal)
                }
                e.timer[i] += dt
                if (e.timer[i] > 9f) {
                    e.timer[i] = 0f
                    summon(w, i, "wolf", 4, 40f)
                }
            }
            else -> Unit
        }
    }

    private fun radial(w: World, i: Int, n: Int, speed: Float, damage: Float, offset: Float) {
        val e = w.enemies
        for (k in 0 until n) {
            val a = offset + k * Rng.TAU / n
            w.enemyShoot(e.x[i], e.y[i], cos(a), sin(a), speed, damage * w.stage.dmgMult, e.def[i]!!.shotSprite)
        }
        w.events.emit(Ev.SFX, e.x[i], e.y[i], 0.8f, Sfx.ENEMY_SHOOT.ordinal)
    }

    private fun summon(w: World, i: Int, id: String, n: Int, r: Float) {
        val e = w.enemies
        for (k in 0 until n) {
            val a = k * Rng.TAU / n
            w.spawnEnemy(id, e.x[i] + cos(a) * r, e.y[i] + sin(a) * r)
        }
        w.events.emit(Ev.SPLASH, e.x[i], e.y[i], r)
    }
}
