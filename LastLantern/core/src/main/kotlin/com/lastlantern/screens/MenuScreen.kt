package com.lastlantern.screens

import com.badlogic.gdx.graphics.Color
import com.badlogic.gdx.scenes.scene2d.ui.Table
import com.lastlantern.LastLanternGame
import com.lastlantern.ui.UiKit

/** Baslangic ekrani: karanlik orman, fener, gozler ve ana dugmeler. */
class MenuScreen(game: LastLanternGame) : BaseScreen(game) {
    private val backdrop = Backdrop(game).apply { cameraLift = 0.12f }
    override val handlesBack = false

    init {
        game.audio.playMusic("menu")
        build()
    }

    private fun build() {
        root.clearChildren()
        val d = game.save.data
        val top = Table()
        top.add().expandX()
        top.add(ui.icon("pick_coin")).padRight(3f)
        top.add(ui.label(d.gold.toString(), UiKit.GOLD))
        root.add(top).growX().row()

        val title = ui.label(t["app.title"], UiKit.GOLD, scale = 3)
        root.add(title).padTop(18f).row()
        val tag = ui.label(t["app.tagline"], UiKit.DIM)
        root.add(tag).padTop(4f).row()
        root.add().expand().row()

        val play = ui.button(t["menu.play"], primaryStyle = true) { game.go(StageSelectScreen(game)) }
        play.label.setFontScale(2f)
        root.add(play).width(180f).height(34f).padBottom(8f).row()
        val row1 = Table()
        row1.add(ui.button(t["menu.heroes"]) { game.go(HeroesScreen(game)) }).width(110f).padRight(6f)
        row1.add(ui.button(t["menu.camp"]) { game.go(CampScreen(game)) }).width(110f)
        root.add(row1).padBottom(6f).row()
        val row2 = Table()
        row2.add(ui.button(t["menu.achievements"]) { game.go(AchievementsScreen(game)) }).width(110f).padRight(6f)
        row2.add(ui.button(t["menu.settings"]) { game.go(SettingsScreen(game)) }).width(110f)
        root.add(row2).padBottom(6f).row()
        if (!game.supporter) {
            val sup = ui.button(t["menu.supporter"]) { game.go(SupporterScreen(game)) }
            sup.label.color = Color(UiKit.GOLD)
            root.add(sup).width(226f).padBottom(4f).row()
        }
        enter(title, tag)
    }

    override fun resize(width: Int, height: Int) {
        super.resize(width, height)
        backdrop.resize(width, height)
    }

    override fun drawBackground(dt: Float) = backdrop.render(dt)

    override fun onBack() = game.platform.exitApp()

    override fun dispose() {
        super.dispose()
        backdrop.dispose()
    }
}
