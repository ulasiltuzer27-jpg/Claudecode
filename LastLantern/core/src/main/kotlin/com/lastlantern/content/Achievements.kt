package com.lastlantern.content

import com.lastlantern.save.SaveData
import com.lastlantern.sim.RunState
import com.lastlantern.sim.World

/**
 * Basarimlar: her biri altin odulu verir, bazilari kahraman acar
 * ([CharacterDef.unlockAchievement]). Kosullar run sonunda (World + kayit)
 * ya da kamp/kahraman ekranlarinda (yalnizca kayit) degerlendirilir.
 */
class AchievementDef(
    val id: String,
    val icon: String,
    val reward: Int,
    /** Ilerleme gosterilebiliyorsa (mevcut, hedef). */
    val progress: ((SaveData) -> Pair<Int, Int>)? = null,
    val check: (SaveData, World?) -> Boolean,
)

object Achievements {
    private fun World.won() = state == RunState.VICTORY

    val all: List<AchievementDef> = listOf(
        AchievementDef("first_steps", "icon_ui_flag", 50) { s, _ -> s.runs >= 1 },
        AchievementDef("survive_5", "icon_ui_clock", 100) { _, w -> w != null && w.time >= 300f },
        AchievementDef("survive_10", "icon_ui_star", 200) { _, w -> w != null && w.won() },
        AchievementDef("level_10", "icon_ui_arrow_up", 60) { _, w -> w != null && w.player.level >= 10 },
        AchievementDef("level_20", "icon_ui_arrow_up", 150) { _, w -> w != null && w.player.level >= 20 },
        AchievementDef("level_30", "icon_ui_arrow_up", 300) { _, w -> w != null && w.player.level >= 30 },
        AchievementDef("kills_500", "icon_ui_skull", 80, { s -> s.totalKills.coerceAtMost(500) to 500 }) { s, _ -> s.totalKills >= 500 },
        AchievementDef("kills_2500", "icon_ui_skull", 200, { s -> s.totalKills.coerceAtMost(2500) to 2500 }) { s, _ -> s.totalKills >= 2500 },
        AchievementDef("kills_10000", "icon_ui_skull", 500, { s -> s.totalKills.coerceAtMost(10000) to 10000 }) { s, _ -> s.totalKills >= 10000 },
        AchievementDef("kills_run", "icon_ui_skull", 200) { _, w -> w != null && w.kills >= 1000 },
        AchievementDef("evolve_1", "icon_e_solar", 100) { s, _ -> s.evolutionsSeen.isNotEmpty() },
        AchievementDef("evolve_3", "icon_e_halo", 250, { s -> s.evolutionsSeen.size.coerceAtMost(3) to 3 }) { s, _ -> s.evolutionsSeen.size >= 3 },
        AchievementDef("evolve_all", "icon_e_reaper", 600, { s -> s.evolutionsSeen.size to 8 }) { s, _ -> s.evolutionsSeen.size >= 8 },
        AchievementDef("miniboss", "icon_ui_crown", 80) { _, w -> w != null && w.minibossDefeated },
        AchievementDef("boss_stag", "icon_ui_trophy", 250) { s, _ -> (s.bossKills["woods"] ?: 0) > 0 },
        AchievementDef("boss_bell", "icon_ui_trophy", 350) { s, _ -> (s.bossKills["drowned"] ?: 0) > 0 },
        AchievementDef("boss_wyrm", "icon_ui_trophy", 500) { s, _ -> (s.bossKills["frozen"] ?: 0) > 0 },
        AchievementDef("win_woods", "icon_ui_flag", 150) { s, _ -> (s.wins["woods"] ?: 0) > 0 },
        AchievementDef("win_drowned", "icon_ui_flag", 250) { s, _ -> (s.wins["drowned"] ?: 0) > 0 },
        AchievementDef("win_frozen", "icon_ui_flag", 400) { s, _ -> (s.wins["frozen"] ?: 0) > 0 },
        AchievementDef("untouched", "icon_ui_shield", 150) { _, w -> w != null && w.untouchedAt3 },
        AchievementDef("gold_1000", "pick_coin", 50, { s -> s.totalGold.coerceAtMost(1000) to 1000 }) { s, _ -> s.totalGold >= 1000 },
        AchievementDef("gold_5000", "pick_coin", 150, { s -> s.totalGold.coerceAtMost(5000) to 5000 }) { s, _ -> s.totalGold >= 5000 },
        AchievementDef("chests", "pick_chest", 120) { _, w -> w != null && w.chestsOpened >= 5 },
        AchievementDef("full_build", "icon_w_sickle", 100) { _, w -> w != null && w.weapons.size >= Content.MAX_WEAPONS },
        AchievementDef("camp_10", "icon_ui_tent", 100, { s -> s.meta.values.sum().coerceAtMost(10) to 10 }) { s, _ -> s.meta.values.sum() >= 10 },
        AchievementDef("all_heroes", "icon_ui_heart", 200, { s -> s.unlockedCharacters.size to Content.characters.size }) { s, _ ->
            s.unlockedCharacters.size >= Content.characters.size
        },
        AchievementDef("each_hero", "icon_ui_star", 400, { s -> s.wins.keys.count { it.startsWith("hero:") } to Content.characters.size }) { s, _ ->
            Content.characters.all { (s.wins["hero:${it.id}"] ?: 0) > 0 }
        },
        AchievementDef("endless", "icon_ui_clock", 300) { s, _ -> s.endlessBest.values.any { it >= 900f } },
        AchievementDef("runs_10", "icon_ui_globe", 80, { s -> s.runs.coerceAtMost(10) to 10 }) { s, _ -> s.runs >= 10 },
    )

    fun byId(id: String) = all.first { it.id == id }

    /** Yeni kazanilan basarimlari kayda isler; odul altinini ekler. */
    fun evaluate(save: SaveData, world: World?): List<AchievementDef> {
        val gained = all.filter { it.id !in save.achievements && it.check(save, world) }
        for (a in gained) {
            save.achievements.add(a.id)
            save.gold += a.reward
            save.totalGold += a.reward
            // Basarimla acilan kahramanlar
            Content.characters.filter { it.unlockAchievement == a.id }.forEach { save.unlockedCharacters.add(it.id) }
        }
        return gained
    }
}
