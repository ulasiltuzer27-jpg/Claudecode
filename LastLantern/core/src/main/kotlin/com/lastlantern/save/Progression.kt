package com.lastlantern.save

import com.lastlantern.content.AchievementDef
import com.lastlantern.content.Achievements
import com.lastlantern.content.Content
import com.lastlantern.sim.RunState
import com.lastlantern.sim.World

/** Bir run'in kayda yansimasi: altin, istatistikler, rekorlar, basarimlar. */
class RunSummary(
    val victory: Boolean,
    val bossKilled: Boolean,
    val goldFound: Int,
    val bonusGold: Int,
    val newRecord: Boolean,
    val achievements: List<AchievementDef>,
    val unlockedStage: String?,
) {
    val totalGold get() = goldFound + bonusGold
}

object Progression {
    fun stageUnlocked(save: SaveData, stageId: String): Boolean {
        val st = Content.stage(stageId)
        if (st.index == 0) return true
        val prev = Content.stages[st.index - 1]
        return (save.wins[prev.id] ?: 0) > 0
    }

    fun endlessUnlocked(save: SaveData, stageId: String) = (save.wins[stageId] ?: 0) > 0

    fun applyRun(save: SaveData, w: World): RunSummary {
        val stage = w.stage.id
        val victory = w.state == RunState.VICTORY && !w.config.endless
        val nextStageWasLocked = Content.stages.getOrNull(w.stage.index + 1)?.let { !stageUnlocked(save, it.id) } ?: false
        var bonus = 0
        if (victory) {
            bonus += 100 * (w.stage.index + 1)
            save.wins[stage] = (save.wins[stage] ?: 0) + 1
            val hero = "hero:${w.config.character.id}"
            save.wins[hero] = (save.wins[hero] ?: 0) + 1
        }
        if (w.bossDefeated) {
            bonus += 150
            save.bossKills[stage] = (save.bossKills[stage] ?: 0) + 1
        }
        val gold = w.gold + bonus
        save.gold += gold
        save.totalGold += gold
        save.totalKills += w.kills
        save.runs++
        save.bestLevel = maxOf(save.bestLevel, w.player.level)
        var record = false
        if (w.config.endless) {
            if (w.time > (save.endlessBest[stage] ?: 0f)) {
                save.endlessBest[stage] = w.time
                record = true
            }
        } else if (w.time > (save.bestTime[stage] ?: 0f) + 0.5f) {
            record = (save.bestTime[stage] ?: 0f) > 0f
            save.bestTime[stage] = w.time
        }
        for (wp in w.weapons) if (wp.evolved) save.evolutionsSeen.add(wp.def.id)
        val gained = Achievements.evaluate(save, w)
        val unlockedStage = Content.stages.getOrNull(w.stage.index + 1)
            ?.takeIf { nextStageWasLocked && stageUnlocked(save, it.id) }?.id
        return RunSummary(victory, w.bossDefeated, w.gold, bonus, record, gained, unlockedStage)
    }
}
