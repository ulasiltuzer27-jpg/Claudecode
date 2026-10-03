package com.lastlantern.sim

import com.lastlantern.content.Behavior
import com.lastlantern.content.EnemyDef
import kotlin.math.cos
import kotlin.math.sin

/**
 * Siradan dusmanlarin hareket kurallari. Hiz sonucu [outX]/[outY]'ye yazilir
 * (her dusman icin Vector2 ayirmamak icin).
 */
object Behaviors {
    var outX = 0f
    var outY = 0f

    /** false donerse dusman bu karede kaldirildi. */
    fun normal(w: World, i: Int, d: EnemyDef, dt: Float, dx: Float, dy: Float, dist: Float, spd: Float): Boolean {
        val e = w.enemies
        outX = dx * spd
        outY = dy * spd
        when (d.behavior) {
            Behavior.CHASE -> Unit
            Behavior.ERRATIC -> {
                val wob = sin(e.anim[i] * 5f + e.uid[i] * 1.7f) * 0.9f
                val c = cos(wob)
                val s = sin(wob)
                outX = (dx * c - dy * s) * spd
                outY = (dx * s + dy * c) * spd
            }
            Behavior.RANGED -> {
                when {
                    dist > 115f -> Unit
                    dist < 75f -> {
                        outX = -dx * spd * 0.6f
                        outY = -dy * spd * 0.6f
                    }
                    else -> {
                        val side = if (e.uid[i] % 2 == 0) 1f else -1f
                        outX = -dy * spd * 0.7f * side
                        outY = dx * spd * 0.7f * side
                    }
                }
                e.timer2[i] += dt
                if (e.timer2[i] >= d.shotInterval && dist < 230f) {
                    e.timer2[i] = 0f
                    w.enemyShoot(e.x[i], e.y[i], dx, dy, d.shotSpeed, d.shotDamage * w.stage.dmgMult, d.shotSprite)
                    w.events.emit(Ev.SFX, e.x[i], e.y[i], 1f, Sfx.ENEMY_SHOOT.ordinal)
                }
            }
            Behavior.DASH -> dash(w, i, dt, dx, dy, dist, spd, 80f, 2.9f, 2.8f, windup = 0.55f)
            else -> Unit
        }
        return true
    }

    /** Hucum: yaklas -> dur ve parla (uyari) -> duz cizgide atil -> soguma. */
    fun dash(w: World, i: Int, dt: Float, dx: Float, dy: Float, dist: Float, spd: Float,
             trigger: Float, mult: Float, cool: Float, windup: Float = 0.45f, dashTime: Float = 0.45f) {
        val e = w.enemies
        when (e.state[i]) {
            EState.NORMAL -> {
                e.timer[i] -= dt
                if (dist < trigger && e.timer[i] <= 0f) {
                    e.state[i] = EState.WINDUP
                    e.timer[i] = windup
                    e.ax[i] = dx
                    e.ay[i] = dy
                }
            }
            EState.WINDUP -> {
                outX = 0f
                outY = 0f
                e.timer[i] -= dt
                if (e.timer[i] <= 0f) {
                    e.state[i] = EState.DASH
                    e.timer[i] = dashTime
                }
            }
            EState.DASH -> {
                outX = e.ax[i] * spd * mult
                outY = e.ay[i] * spd * mult
                e.timer[i] -= dt
                if (e.timer[i] <= 0f) {
                    e.state[i] = EState.NORMAL
                    e.timer[i] = cool
                }
            }
        }
    }
}
