package com.lastlantern

import com.badlogic.gdx.Screen
import com.lastlantern.content.Content
import com.lastlantern.save.Progression
import com.lastlantern.screens.AchievementsScreen
import com.lastlantern.screens.CampScreen
import com.lastlantern.screens.GameScreen
import com.lastlantern.screens.HeroesScreen
import com.lastlantern.screens.MenuScreen
import com.lastlantern.screens.ResultsScreen
import com.lastlantern.screens.SettingsScreen
import com.lastlantern.screens.StageSelectScreen
import com.lastlantern.screens.SupporterScreen
import com.lastlantern.sim.Bot
import com.lastlantern.sim.RunConfig
import com.lastlantern.sim.RunState

/**
 * Masaustu ekran goruntusu senaryolari (--capture <ad>). Her senaryo oyunu
 * gercek kodla belirli bir ana getirir: magaza gorselleri ve gorsel
 * regresyon kontrolu ayni yoldan uretilir.
 */
object Capture {
    val scenarios = listOf(
        "menu", "stages", "heroes", "camp", "achievements", "settings", "supporter",
        "game_start", "game_early", "game_crowd", "game_late", "levelup", "chest", "boss", "results",
        "drowned", "frozen", "evolve",
    )

    fun firstScreen(game: LastLanternGame): Screen {
        val name = game.launch.capture ?: game.launch.startScreen ?: "menu"
        val d = game.save.data
        // Gosterim icin dolu bir ilerleme
        if (game.launch.capture != null) {
            d.tutorialDone = true
            d.gold = 2480
            d.runs = 7
            d.totalKills = 3120
            d.wins["woods"] = 2
            d.bestTime["woods"] = 600f
            d.bestTime["drowned"] = 412f
            d.unlockedCharacters.addAll(listOf("keeper", "monk", "witch"))
            d.meta.putAll(mapOf("might" to 2, "vital" to 2, "magnet" to 1, "greed" to 1, "recover" to 1))
            d.achievements.addAll(listOf("first_steps", "survive_5", "survive_10", "level_10", "level_20", "kills_500", "kills_2500", "win_woods", "miniboss"))
        }
        return when (name) {
            "menu" -> MenuScreen(game)
            "stages" -> StageSelectScreen(game)
            "heroes" -> HeroesScreen(game)
            "camp" -> CampScreen(game)
            "achievements" -> AchievementsScreen(game)
            "settings" -> SettingsScreen(game)
            "supporter" -> SupporterScreen(game)
            "game_start" -> game(game, "woods", "keeper", 4f)
            "game_early" -> game(game, "woods", "keeper", 75f)
            "game_crowd" -> game(game, "woods", "keeper", 330f, meta = true, skill = 0.6f)
            "game_late" -> game(game, "woods", "keeper", 470f, meta = true)
            "drowned" -> game(game, "drowned", "monk", 260f, meta = true, skill = 0.6f)
            "frozen" -> game(game, "frozen", "tinker", 300f, meta = true, skill = 0.6f)
            "boss" -> game(game, "woods", "keeper", 552f, meta = true)
            "levelup" -> game(game, "woods", "keeper", 40f, stopAt = RunState.LEVEL_UP)
            "chest" -> game(game, "woods", "keeper", 125f, meta = true, forceChest = true)
            "evolve" -> {
                // Sandiktan evrim cikar: kivilcim maks. seviye + fener yagi
                val s = game(game, "woods", "keeper", 230f, meta = true, skill = 0.6f)
                val w = s.world
                val spark = w.weapons.first { it.def.id == "spark" }
                while (!spark.maxed) spark.levelUp()
                if ("oil" !in w.passives) w.addPassive("oil")
                s.bot = null
                w.chestsOpened++
                w.upgrades.rollChest()
                w.state = RunState.CHEST
                s
            }
            "results" -> {
                val s = game(game, "woods", "keeper", 600f, meta = true)
                val w = s.world
                if (w.state != RunState.VICTORY) w.giveUp()
                val summary = Progression.applyRun(game.save.data, w)
                ResultsScreen(game, w, summary)
            }
            else -> MenuScreen(game)
        }
    }

    private fun game(
        game: LastLanternGame, stage: String, hero: String, seconds: Float, meta: Boolean = false,
        stopAt: RunState? = null, forceChest: Boolean = false, skill: Float = 1f,
    ): GameScreen {
        val metaRanks = if (meta) Content.meta.associate { it.id to it.maxRank } else emptyMap()
        val cfg = RunConfig(Content.stage(stage), Content.character(hero), metaRanks, seed = game.launch.seed ?: 21L)
        val screen = GameScreen(game, cfg)
        val w = screen.world
        val bot = Bot(w, skill)
        // Gercek ekran oranina yakin bir gorus alani (yumurtlama mesafeleri icin)
        w.viewHalfW = 180f
        w.viewHalfH = 400f
        var guard = 0
        while (w.time < seconds && guard++ < 100_000) {
            if (stopAt != null && w.state == stopAt) break
            if (w.state == RunState.DEAD) {
                w.reviveByAd()
                w.adReviveUsed = false
            }
            if (w.state == RunState.VICTORY || w.state == RunState.DEFEAT) break
            if (w.state == RunState.LEVEL_UP && stopAt != RunState.LEVEL_UP) {
                w.upgrades.choose(bot.chooseOffer(w.upgrades.offers))
                w.events.clear()
                continue
            }
            if (w.state == RunState.CHEST) {
                w.resume()
                continue
            }
            bot.think(GameScreen.STEP)
            w.update(GameScreen.STEP, bot.mx, bot.my)
            w.events.clear()
        }
        if (forceChest && w.state == RunState.PLAYING) {
            w.chestsOpened++
            w.upgrades.rollChest()
            w.state = RunState.CHEST
        }
        // Son birkac saniye canli: parcaciklar, isiklar ve animasyonlar dolsun
        if (stopAt == null && !forceChest && w.state == RunState.PLAYING) screen.bot = bot
        return screen
    }
}
