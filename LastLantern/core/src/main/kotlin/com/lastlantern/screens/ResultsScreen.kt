package com.lastlantern.screens

import com.badlogic.gdx.graphics.Color
import com.badlogic.gdx.scenes.scene2d.Touchable
import com.badlogic.gdx.scenes.scene2d.actions.Actions
import com.badlogic.gdx.scenes.scene2d.ui.Label
import com.badlogic.gdx.scenes.scene2d.ui.Table
import com.badlogic.gdx.scenes.scene2d.ui.TextButton
import com.badlogic.gdx.utils.Align
import com.lastlantern.LastLanternGame
import com.lastlantern.content.Content
import com.lastlantern.save.RunSummary
import com.lastlantern.sim.RunState
import com.lastlantern.sim.Sfx
import com.lastlantern.sim.World
import com.lastlantern.ui.UiKit

/** Gecenin sonu: istatistikler, altin (2x reklam secenegi), yeni acilanlar. */
class ResultsScreen(game: LastLanternGame, private val world: World, private val summary: RunSummary) : BaseScreen(game) {
    private val backdrop = Backdrop(game, world.stage.id, world.config.character.id).apply { cameraLift = 0.32f }
    private var doubled = false
    private lateinit var goldLabel: Label

    init {
        game.audio.playMusic("menu")
        build()
    }

    private fun build() {
        val won = world.state == RunState.VICTORY
        val titleText = when {
            world.config.endless -> t["stage.endless"] + "  " + time(world.time)
            summary.bossKilled -> t.f("results.victory_boss", t["hud.boss_name.${bossId()}"])
            won -> t["results.victory"]
            else -> t["results.defeat"]
        }
        val title = ui.label(titleText, if (won || world.config.endless) UiKit.GOLD else UiKit.RED, scale = 2, wrap = true)
        root.add(title).width(260f).padTop(14f).padBottom(4f).row()
        if (summary.newRecord) root.add(ui.label(t["results.record"], UiKit.GREEN)).padBottom(4f).row()

        val stats = Table()
        stats.background = ui.panel
        stats.pad(8f, 12f, 8f, 12f)
        fun stat(key: String, value: String, color: Color = Color.WHITE) {
            stats.add(ui.label(t[key], UiKit.DIM, align = Align.left)).left().expandX()
            stats.add(ui.label(value, color)).right().row()
        }
        stat("results.time", time(world.time))
        stat("results.kills", world.kills.toString())
        stat("results.level", world.player.level.toString())
        stats.add(ui.label(t["results.gold"], UiKit.DIM, align = Align.left)).left().expandX()
        val g = Table()
        g.add(ui.icon("pick_coin")).padRight(3f)
        goldLabel = ui.label(goldText(), UiKit.GOLD)
        g.add(goldLabel)
        stats.add(g).right().row()
        root.add(stats).width(230f).padBottom(6f).row()

        // Silah hasarlari
        val dmg = Table()
        for (wp in world.weapons.sortedByDescending { it.totalDamage }) {
            ui.iconCell(dmg, wp.def.icon).padRight(4f)
            dmg.add(ui.label(t["weapon.${wp.def.id}"], UiKit.DIM, align = Align.left)).left().expandX()
            dmg.add(ui.label(compact(wp.totalDamage), Color.WHITE)).right().row()
        }
        root.add(ui.label(t["results.weapons"], UiKit.MUTED)).padBottom(2f).row()
        root.add(dmg).width(220f).padBottom(6f).row()

        // Yeni acilanlar
        val news = Table()
        summary.unlockedStage?.let {
            news.add(ui.label(t.f("results.unlocked", t["stage.$it"]), UiKit.GREEN, wrap = true)).width(240f).row()
        }
        for (a in summary.achievements) {
            news.add(ui.label(t.f("results.achievement", t["ach.${a.id}"]) + "  +${a.reward}", UiKit.GOLD, wrap = true)).width(240f).row()
            Content.characters.filter { it.unlockAchievement == a.id }.forEach {
                news.add(ui.label(t.f("results.unlocked", t["char.${it.id}"]), UiKit.GREEN)).row()
            }
        }
        root.add(news).padBottom(4f).row()
        root.add().expand().row()

        if (summary.totalGold > 0) {
            val supporter = game.supporter
            val label = if (supporter) t["results.double_supporter"] else t["results.double"]
            lateinit var dbl: TextButton
            dbl = ui.button(label, primaryStyle = true) {
                if (supporter) applyDouble(dbl)
                else {
                    dbl.touchable = Touchable.disabled
                    game.audio.pause()
                    game.platform.showRewarded("double_gold", onReward = {
                        game.audio.resume()
                        applyDouble(dbl)
                    }, onClosed = {
                        game.audio.resume()
                        dbl.touchable = Touchable.enabled
                    })
                }
            }
            if (!supporter && !game.platform.rewardedReady()) dbl.isDisabled = true
            root.add(dbl).width(220f).padBottom(6f).row()
        }
        root.add(ui.button(t["common.continue"]) { onBack() }).width(220f).padBottom(4f).row()
        enter(title, stats, dmg, news)
        maybeAskReview(won)
    }

    private fun bossId() = Content.enemies.values.first { it.tier == com.lastlantern.content.Tier.BOSS && world.stage.events.any { e -> e.enemy == it.id } }.id

    private fun goldText() = "+${if (doubled) summary.totalGold * 2 else summary.totalGold}"

    private fun applyDouble(btn: TextButton) {
        if (doubled) return
        doubled = true
        game.save.data.gold += summary.totalGold
        game.save.data.totalGold += summary.totalGold
        game.save.save()
        goldLabel.setText(goldText())
        goldLabel.addAction(Actions.sequence(Actions.scaleTo(1.3f, 1.3f, 0.1f), Actions.scaleTo(1f, 1f, 0.2f)))
        game.audio.play(Sfx.COIN)
        btn.setText(t["results.doubled"])
        btn.isDisabled = true
    }

    /** Play In-App Review: yalnizca olumlu bir anda ve bir kez. */
    private fun maybeAskReview(won: Boolean) {
        val d = game.save.data
        if (won && !d.reviewAsked && d.runs >= 3) {
            d.reviewAsked = true
            game.save.save()
            game.platform.requestReview()
        }
    }

    private fun compact(v: Float): String = when {
        v >= 1_000_000f -> game.i18n.num(v / 1_000_000f, 1) + "M"
        v >= 10_000f -> game.i18n.num(v / 1000f, 1) + "K"
        else -> v.toInt().toString()
    }

    override fun resize(width: Int, height: Int) {
        super.resize(width, height)
        backdrop.resize(width, height)
    }

    override fun drawBackground(dt: Float) = backdrop.render(dt)

    override fun onBack() = game.go(if (game.save.data.runs <= 1) MenuScreen(game) else StageSelectScreen(game))

    override fun dispose() {
        super.dispose()
        backdrop.dispose()
    }
}
