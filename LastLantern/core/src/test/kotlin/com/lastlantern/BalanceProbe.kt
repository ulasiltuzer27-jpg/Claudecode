package com.lastlantern

import com.lastlantern.content.Content
import com.lastlantern.sim.Bot
import com.lastlantern.sim.RunConfig
import com.lastlantern.sim.RunState
import com.lastlantern.sim.World
import org.junit.jupiter.api.Tag
import org.junit.jupiter.api.Test

/** Denge olcumu (assert yok): ./gradlew :core:test --tests '*BalanceProbe*' -Pprobe */
@Tag("probe")
class BalanceProbe {
    @Test
    fun probe() {
        val stages = (System.getProperty("probe.stages") ?: "woods").split(",")
        val skills = listOf(0.45f, 1f)
        for (st in stages) for (skill in skills) for (metaOn in listOf(false, true)) {
            val meta = if (metaOn) Content.meta.associate { it.id to it.maxRank } else emptyMap()
            val rows = (1L..8L).map { seed ->
                val w = World(RunConfig(Content.stage(st), Content.character("keeper"), meta, seed * 31))
                val bot = Bot(w, skill)
                while (bot.step(1f / 30f) && w.time < 700f) Unit
                w
            }
            val wins = rows.count { it.state == RunState.VICTORY }
            val boss = rows.count { it.bossDefeated }
            println("%-8s skill=%.2f meta=%-5s ort=%3d sn  min=%3d  kazan=%d/8 boss=%d  sv=%s  hasar=%d".format(
                st, skill, metaOn, rows.map { it.time }.average().toInt(), rows.minOf { it.time }.toInt(),
                wins, boss, rows.map { it.player.level }.average().toInt(), rows.map { it.damageTaken }.average().toInt()))
        }
    }
}
