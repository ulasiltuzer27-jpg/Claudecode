package com.lastlantern

import com.lastlantern.content.Content
import com.lastlantern.save.Progression
import com.lastlantern.save.SaveData
import com.lastlantern.sim.Bot
import com.lastlantern.sim.RunConfig
import com.lastlantern.sim.RunState
import com.lastlantern.sim.World
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertNotNull
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

class ProgressionTest {
    private fun playTo(stage: String, seconds: Float, meta: Boolean = true): World {
        val m = if (meta) Content.meta.associate { it.id to it.maxRank } else emptyMap()
        val w = World(RunConfig(Content.stage(stage), Content.character("keeper"), m, seed = 9))
        val bot = Bot(w, 1f)
        while (w.time < seconds && bot.step(1f / 30f)) Unit
        return w
    }

    @Test
    fun stagesUnlockInOrder() {
        val s = SaveData()
        assertTrue(Progression.stageUnlocked(s, "woods"))
        assertFalse(Progression.stageUnlocked(s, "drowned"))
        assertFalse(Progression.endlessUnlocked(s, "woods"))
        val w = playTo("woods", 700f)
        assertEquals(RunState.VICTORY, w.state, "tam kamp + usta bot ilk geceyi bitirmeli")
        val sum = Progression.applyRun(s, w)
        assertTrue(sum.victory)
        assertEquals("drowned", sum.unlockedStage)
        assertTrue(Progression.stageUnlocked(s, "drowned"))
        assertTrue(Progression.endlessUnlocked(s, "woods"))
        assertFalse(Progression.stageUnlocked(s, "frozen"))
        assertEquals(1, s.wins["hero:keeper"])
    }

    @Test
    fun goldAndAchievementsAreCredited() {
        val s = SaveData()
        val w = playTo("woods", 320f, meta = false)
        if (w.state == RunState.PLAYING) w.giveUp()
        val goldBefore = s.gold
        val sum = Progression.applyRun(s, w)
        val achGold = sum.achievements.sumOf { it.reward }
        assertEquals(goldBefore + sum.totalGold + achGold, s.gold)
        assertEquals(1, s.runs)
        assertEquals(w.kills, s.totalKills)
        assertTrue(sum.achievements.any { it.id == "first_steps" })
        // 5 dakikayi gecti: Kesis acilir
        if (w.time >= 300f) {
            assertTrue("survive_5" in s.achievements)
            assertTrue("monk" in s.unlockedCharacters)
        }
        // Ayni basarim ikinci kez odul vermez
        val again = Progression.applyRun(s, w)
        assertTrue(again.achievements.none { it.id == "first_steps" })
    }

    @Test
    fun recordsAreTracked() {
        val s = SaveData()
        val w1 = playTo("woods", 100f, meta = false)
        w1.giveUp()
        Progression.applyRun(s, w1)
        assertNotNull(s.bestTime["woods"])
        val first = s.bestTime["woods"]!!
        val w2 = playTo("woods", 200f, meta = false)
        if (w2.state == RunState.PLAYING) w2.giveUp()
        val sum = Progression.applyRun(s, w2)
        if (w2.time > first + 0.5f) {
            assertTrue(sum.newRecord)
            assertEquals(w2.time, s.bestTime["woods"])
        }
    }
}
