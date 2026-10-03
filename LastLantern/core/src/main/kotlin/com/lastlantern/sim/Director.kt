package com.lastlantern.sim

import com.lastlantern.content.Content
import com.lastlantern.content.EventType
import com.lastlantern.content.Wave
import kotlin.math.atan2
import kotlin.math.cos
import kotlin.math.sin
import kotlin.math.sqrt

/**
 * Dalgalari ve zamanli olaylari yonetir: hangi dakikada hangi dusmanlar,
 * ne siklikla ve en fazla kac tane; cember, suru, elit, ara boss ve boss.
 */
class Director(private val w: World) {
    private var spawnTimer = 0.5f
    private var eventIdx = 0
    private var bossWarned = false
    private var endlessTimer = 0f

    val spawnDistance: Float
        get() = sqrt(w.viewHalfW * w.viewHalfW + w.viewHalfH * w.viewHalfH) + 18f

    fun currentWave(): Wave {
        val waves = w.stage.waves
        var cur = waves[0]
        for (wv in waves) {
            // Sonsuz modda boss asamasinin seyrek dalgasi atlanir
            if (w.config.endless && wv.start >= Content.BOSS_TIME) continue
            if (wv.start <= w.time) cur = wv
        }
        return cur
    }

    /** Sonsuz modda dusman tavani zamanla artar (480. sn'den sonra her 5 dk'da +%100). */
    private fun capScale(): Float =
        if (w.config.endless && w.time > 480f) 1f + (w.time - 480f) / 300f else 1f

    fun update(dt: Float) {
        if (w.endTimer >= 0f) return
        val wave = currentWave()
        val bossAlive = w.bossSlot >= 0
        spawnTimer -= dt
        if (spawnTimer <= 0f) {
            // Endless: sure ilerledikce aralik daralir
            val pace = if (w.config.endless && w.time > Content.RUN_LENGTH) 0.7f else 1f
            spawnTimer = wave.interval * pace
            val cap = (wave.maxAlive * capScale() * (if (bossAlive) 0.7f else 1f)).toInt().coerceAtMost(700)
            var n = wave.batch
            while (n-- > 0 && w.enemies.count < cap) {
                val (sx, sy) = spawnPoint(ahead = w.rng.chance(0.35f))
                w.spawnEnemy(pickWeighted(wave.mix), sx, sy)
            }
        }
        // Boss uyarisi: dramatik giris icin 4 sn once
        if (!bossWarned && !w.config.endless && w.time >= Content.BOSS_TIME - 4f) {
            bossWarned = true
            w.events.emit(Ev.BOSS_WARNING)
            w.events.emit(Ev.SFX, bv = Sfx.WARNING.ordinal, av = 1f)
        }
        val events = w.stage.events
        while (eventIdx < events.size && events[eventIdx].time <= w.time) {
            val ev = events[eventIdx++]
            if (w.config.endless && ev.type == EventType.BOSS) continue
            when (ev.type) {
                EventType.RING -> ring(ev.enemy, ev.count)
                EventType.SWARM -> swarm(ev.enemy, ev.count)
                EventType.ELITE -> {
                    val (sx, sy) = spawnPoint(ahead = true)
                    w.spawnEnemy(ev.enemy, sx, sy, elite = true)
                }
                EventType.MINIBOSS, EventType.BOSS -> {
                    val (sx, sy) = spawnPoint(ahead = true)
                    w.spawnEnemy(ev.enemy, sx, sy)
                }
            }
        }
        // Endless: olay listesi bitince periyodik elit ve suruler
        if (w.config.endless && w.time > Content.RUN_LENGTH) {
            endlessTimer += dt
            if (endlessTimer > 40f) {
                endlessTimer = 0f
                val mix = wave.mix
                val (sx, sy) = spawnPoint(ahead = true)
                w.spawnEnemy(pickWeighted(mix), sx, sy, elite = true)
                swarm(mix[w.rng.nextInt(mix.size)].first, 30)
            }
        }
    }

    private fun pickWeighted(mix: List<Pair<String, Int>>): String {
        var total = 0
        for ((_, wt) in mix) total += wt
        var r = w.rng.nextInt(total)
        for ((id, wt) in mix) {
            r -= wt
            if (r < 0) return id
        }
        return mix.last().first
    }

    /** Ekranin hemen disinda bir nokta; [ahead] ise oyuncunun gittigi yone yakin. */
    fun spawnPoint(ahead: Boolean = false): Pair<Float, Float> {
        val p = w.player
        val a = if (ahead && p.moving) atan2(p.faceY, p.faceX) + w.rng.range(-1.1f, 1.1f) else w.rng.angle()
        // Dikey ekranda elips: yanlara yakin, ust/alta uzak
        val rx = w.viewHalfW + 20f
        val ry = w.viewHalfH + 20f
        return Pair(p.x + cos(a) * rx, p.y + sin(a) * ry)
    }

    private fun ring(id: String, n: Int) {
        val p = w.player
        val r = minOf(w.viewHalfW, w.viewHalfH) + 40f
        // Cemberde rastgele bir aciklik birakilir: kacis yolu her zaman var
        val gap = w.rng.nextInt(n)
        val gapSize = maxOf(3, n / 7)
        for (k in 0 until n) {
            if ((k - gap + n) % n < gapSize) continue
            val a = k * Rng.TAU / n
            w.spawnEnemy(id, p.x + cos(a) * r, p.y + sin(a) * r)
        }
    }

    private fun swarm(id: String, n: Int) {
        val p = w.player
        val a = w.rng.angle()
        val dx = cos(a)
        val dy = sin(a)
        // Suru oyuncunun yanindan gecen bir serit halinde akar
        val startX = p.x - dx * spawnDistance
        val startY = p.y - dy * spawnDistance
        for (k in 0 until n) {
            val off = w.rng.range(-50f, 50f)
            val back = k * 6f
            val i = w.spawnEnemy(id, startX - dx * back - dy * off, startY - dy * back + dx * off)
            if (i >= 0) {
                w.enemies.state[i] = EState.SWARM
                w.enemies.ax[i] = dx
                w.enemies.ay[i] = dy
            }
        }
    }
}
