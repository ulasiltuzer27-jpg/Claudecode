package com.lastlantern

import com.lastlantern.content.Content
import com.lastlantern.sim.RunConfig
import com.lastlantern.sim.RunState
import com.lastlantern.sim.World
import org.junit.jupiter.api.Tag
import org.junit.jupiter.api.Test

/**
 * Silah DPS olcumu: hareketsiz, olumsuz hedeflerle dolu bir alanda her silah
 * tek basina 20 sn ateslenir. Seviye 1, 6 ve evrim karsilastirilir.
 *   ./gradlew :core:test --tests '*WeaponDpsProbe*' -Pprobe
 */
@Tag("probe")
class WeaponDpsProbe {
    private fun dps(id: String, level: Int, evolve: Boolean): Float {
        val w = World(RunConfig(Content.stage("woods"), Content.character("keeper"), seed = 5))
        w.weapons.clear()
        val wp = w.addWeapon(id)
        repeat(level - 1) { wp.levelUp() }
        if (evolve) wp.evolveInto(Content.weapons.getValue(Content.weapons.getValue(id).evolvesTo!!))
        // 160 hedef: 20-150 px halkalarda
        var k = 0
        // ~70 hedef: oyunun ortalarindaki kalabaliga yakin yogunluk
        for (r in listOf(26f, 55f, 90f, 130f)) {
            val n = (r / 7f).toInt()
            for (j in 0 until n) {
                val a = j * 6.2832f / n + r
                val i = w.spawnEnemy("golem", kotlin.math.cos(a) * r, kotlin.math.sin(a) * r)
                if (i >= 0) {
                    w.enemies.hp[i] = 1e9f; w.enemies.maxHp[i] = 1e9f
                    w.enemies.speed[i] = 0f; w.enemies.damage[i] = 0f
                }
                k++
            }
        }
        val t = 20f
        var time = 0f
        while (time < t) {
            w.update(1f / 60f, 0f, 0f)
            w.events.clear()
            if (w.state != RunState.PLAYING) w.state = RunState.PLAYING
            // Geri tepme dagitmasin: hedefleri yerine sabitle
            for (i in 0 until w.enemies.high) if (w.enemies.active[i]) {
                w.enemies.kbx[i] = 0f; w.enemies.kby[i] = 0f
            }
            time += 1f / 60f
        }
        return wp.totalDamage / t
    }

    @Test
    fun probe() {
        println("silah        sv1     sv6    evrim")
        for (id in Content.baseWeapons) {
            println("%-10s %6.0f  %6.0f  %7.0f".format(id, dps(id, 1, false), dps(id, 6, false), dps(id, 6, true)))
        }
    }
}
