package com.lastlantern

import com.lastlantern.content.Content
import com.lastlantern.sim.Bot
import com.lastlantern.sim.RunConfig
import com.lastlantern.sim.RunState
import com.lastlantern.sim.World
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

class SimulationTest {
    private val dt = 1f / 60f

    private fun run(stage: String, seed: Long, meta: Map<String, Int> = emptyMap(), char: String = "keeper",
                    maxTime: Float = 700f, skill: Float = 0.45f): World {
        val w = World(RunConfig(Content.stage(stage), Content.character(char), meta, seed))
        val bot = Bot(w, skill)
        var guard = 0
        while (bot.step(dt) && w.time < maxTime && guard++ < 200_000) Unit
        return w
    }

    @Test
    fun deterministicWithSameSeed() {
        val a = run("woods", 42, maxTime = 90f)
        val b = run("woods", 42, maxTime = 90f)
        assertEquals(a.kills, b.kills)
        assertEquals(a.player.level, b.player.level)
        assertEquals(a.time, b.time)
    }

    @Test
    fun stageOneBalanceWithoutMeta() {
        // Siradan bir oyuncu (bot beceri 0.45) kamp yukseltmesi olmadan ilk
        // bolgede birkac dakika dayanmali ama cogunlukla geceyi bitirememeli.
        val results = (1L..6L).map { run("woods", it * 31) }
        val times = results.map { it.time }
        val wins = results.count { it.state == RunState.VICTORY }
        println("woods meta=0 sureler=${times.map { it.toInt() }} seviyeler=${results.map { it.player.level }} kazanma=$wins")
        val avg = times.average()
        assertTrue(avg > 200.0, "cok zor: ortalama ${avg.toInt()} sn")
        assertTrue(avg < 590.0, "cok kolay: ortalama ${avg.toInt()} sn")
        assertTrue(results.map { it.player.level }.average() > 8.0)
    }

    @Test
    fun stageOneWinnableWithMeta() {
        val meta = Content.meta.associate { it.id to it.maxRank }
        val results = (11L..14L).map { run("woods", it, meta) }
        val wins = results.count { it.state == RunState.VICTORY }
        println("woods meta=max sureler=${results.map { it.time.toInt() }} kazanma=$wins")
        assertTrue(wins >= 3, "tam kamp yukseltmesiyle bile kazanilamiyor: $wins/4")
    }

    @Test
    fun allStagesAndCharactersRun() {
        for (st in Content.stages) for (ch in Content.characters) {
            val w = run(st.id, 7, char = ch.id, maxTime = 75f)
            assertTrue(w.kills > 0, "${st.id}/${ch.id}: hic oldurme yok")
        }
    }

    @Test
    fun tickPerformanceUnderLoad() {
        val w = World(RunConfig(Content.stage("woods"), Content.character("keeper"), seed = 3))
        // Butun silahlari ekle ve kalabalik olustur
        for (id in Content.baseWeapons) if (!w.ownsWeapon(id)) w.addWeapon(id)
        val rng = java.util.Random(1)
        repeat(600) {
            w.spawnEnemy(if (it % 3 == 0) "husk" else "shade",
                (rng.nextFloat() - 0.5f) * 500f, (rng.nextFloat() - 0.5f) * 800f)
        }
        // Isinma
        repeat(120) { w.update(dt, 0.3f, 0.1f); w.state = RunState.PLAYING }
        val n = 300
        val t0 = System.nanoTime()
        repeat(n) {
            w.update(dt, 0.3f, 0.1f)
            w.state = RunState.PLAYING
            if (w.enemies.count < 500) repeat(50) { k ->
                w.spawnEnemy("shade", w.player.x + (k - 25) * 9f, w.player.y + 200f)
            }
        }
        val ms = (System.nanoTime() - t0) / 1e6 / n
        println("tick: %.3f ms (%d dusman, %d mermi)".format(ms, w.enemies.count, w.shots.count))
        assertTrue(ms < 4.0, "tick cok yavas: $ms ms")
    }
}
