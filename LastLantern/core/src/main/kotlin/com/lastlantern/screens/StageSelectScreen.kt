package com.lastlantern.screens

import com.badlogic.gdx.graphics.Color
import com.badlogic.gdx.scenes.scene2d.ui.Table
import com.badlogic.gdx.scenes.scene2d.utils.NinePatchDrawable
import com.badlogic.gdx.utils.Align
import com.lastlantern.LastLanternGame
import com.lastlantern.content.Content
import com.lastlantern.save.Progression
import com.lastlantern.sim.Sfx
import com.lastlantern.ui.UiKit

/** Gece secimi: bolge, kahraman ve sonsuz mod. */
class StageSelectScreen(game: LastLanternGame) : BaseScreen(game) {
    private val backdrop = Backdrop(game).apply { cameraLift = 0.3f }
    private var endless = false

    init {
        build()
    }

    private fun build() {
        root.clearChildren()
        val d = game.save.data
        if (!Progression.stageUnlocked(d, d.selectedStage)) d.selectedStage = "woods"
        if (!Progression.endlessUnlocked(d, d.selectedStage)) endless = false

        val head = Table()
        head.add(ui.button(t["common.back"], sound = Sfx.UI_BACK) { onBack() }).left()
        head.add(ui.label(t["stage.choose"], Color.WHITE, scale = 2)).expandX()
        head.add().width(44f)
        root.add(head).growX().padBottom(10f).row()

        // Kahraman
        val hero = Content.character(d.selectedCharacter)
        val heroRow = Table()
        heroRow.background = ui.panel
        heroRow.pad(5f)
        ui.iconCell(heroRow, "portrait_${hero.id}", 2).padRight(8f)
        val col = Table()
        col.add(ui.label(t["char.${hero.id}"], UiKit.GOLD, align = Align.left)).left().row()
        col.add(ui.label(t["char.${hero.id}.desc"], UiKit.DIM, wrap = true, align = Align.left)).width(150f).left().row()
        heroRow.add(col).expandX().left()
        heroRow.add(ui.button(t["menu.heroes"]) { game.go(HeroesScreen(game, returnToStages = true)) })
        root.add(heroRow).width(250f).padBottom(10f).row()

        for (st in Content.stages) {
            val unlocked = Progression.stageUnlocked(d, st.id)
            val selected = d.selectedStage == st.id
            val card = Table()
            card.background = NinePatchDrawable(ui.card).tint(
                when {
                    !unlocked -> Color(0.35f, 0.32f, 0.42f, 1f)
                    selected -> UiKit.GOLD
                    else -> Color(0.55f, 0.5f, 0.7f, 1f)
                })
            card.pad(6f, 8f, 7f, 8f)
            ui.iconCell(card, "${st.tileset}_0", 2).padRight(8f)
            val info = Table()
            info.add(ui.label(t["stage.${st.id}"], if (unlocked) Color.WHITE else UiKit.MUTED, align = Align.left)).left().row()
            val sub = when {
                !unlocked -> t.f("stage.locked", t["stage.${Content.stages[st.index - 1].id}"])
                (d.wins[st.id] ?: 0) > 0 -> t["stage.cleared"] + "  •  " + t.f("stage.best", time(d.bestTime[st.id] ?: 0f))
                (d.bestTime[st.id] ?: 0f) > 0f -> t.f("stage.best", time(d.bestTime[st.id] ?: 0f))
                else -> ""
            }
            info.add(ui.label(sub, UiKit.DIM, wrap = true, align = Align.left)).width(170f).left().row()
            card.add(info).expandX().left()
            if (!unlocked) card.add(ui.icon("icon_ui_lock"))
            if (unlocked) ui.onClick(card) {
                d.selectedStage = st.id
                game.save.save()
                build()
            }
            root.add(card).width(250f).padBottom(5f).row()
        }

        // Sonsuz mod
        val endlessOk = Progression.endlessUnlocked(d, d.selectedStage)
        val eRow = Table()
        if (endlessOk) {
            val label = "${t["stage.endless"]}: ${if (endless) t["settings.on"] else t["settings.off"]}"
            eRow.add(ui.button(label) {
                endless = !endless
                build()
            }).width(150f).row()
            if (endless) {
                eRow.add(ui.label(t["stage.endless_desc"], UiKit.DIM, wrap = true)).width(230f).padTop(3f).row()
                (d.endlessBest[d.selectedStage])?.let {
                    eRow.add(ui.label(t.f("stage.best", time(it)), UiKit.GOLD)).padTop(2f).row()
                }
            }
        } else {
            eRow.add(ui.label(t["stage.endless_locked"], UiKit.MUTED, wrap = true)).width(230f)
        }
        root.add(eRow).padTop(6f).row()
        root.add().expand().row()
        val play = ui.button(t["menu.play"], primaryStyle = true) {
            game.go(GameScreen.start(game, d.selectedStage, endless))
        }
        play.label.setFontScale(2f)
        root.add(play).width(180f).height(34f).padBottom(6f).row()
    }

    override fun resize(width: Int, height: Int) {
        super.resize(width, height)
        backdrop.resize(width, height)
    }

    override fun drawBackground(dt: Float) = backdrop.render(dt)

    override fun onBack() = game.go(MenuScreen(game))

    override fun dispose() {
        super.dispose()
        backdrop.dispose()
    }
}
