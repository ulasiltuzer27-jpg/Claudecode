package com.lastlantern.screens

import com.badlogic.gdx.graphics.Color
import com.badlogic.gdx.scenes.scene2d.ui.ScrollPane
import com.badlogic.gdx.scenes.scene2d.ui.Slider
import com.badlogic.gdx.scenes.scene2d.ui.Table
import com.badlogic.gdx.scenes.scene2d.utils.ChangeListener
import com.badlogic.gdx.scenes.scene2d.utils.NinePatchDrawable
import com.badlogic.gdx.scenes.scene2d.utils.TextureRegionDrawable
import com.badlogic.gdx.utils.Align
import com.lastlantern.LastLanternGame
import com.lastlantern.Links
import com.lastlantern.content.Achievements
import com.lastlantern.content.Content
import com.lastlantern.sim.Sfx
import com.lastlantern.ui.ConfirmOverlay
import com.lastlantern.ui.UiKit

/** Ust basligi (geri + baslik + altin) olan liste ekranlarinin temeli. */
abstract class MenuPage(game: LastLanternGame, private val titleKey: String) : BaseScreen(game) {
    private val backdrop = Backdrop(game).apply { cameraLift = 0.36f }
    protected val body = Table()
    private val gold = ui.label("", UiKit.GOLD)

    init {
        val head = Table()
        head.add(ui.button(t["common.back"], sound = Sfx.UI_BACK) { onBack() }).left()
        head.add(ui.label(t[titleKey], Color.WHITE, scale = 2)).expandX()
        val g = Table()
        g.add(ui.icon("pick_coin")).padRight(3f)
        g.add(gold)
        head.add(g).width(44f).right()
        root.add(head).growX().padBottom(8f).row()
        val scroll = ScrollPane(body)
        scroll.setScrollingDisabled(true, false)
        scroll.setOverscroll(false, true)
        scroll.fadeScrollBars = true
        root.add(scroll).grow().row()
        refreshGold()
    }

    protected fun refreshGold() {
        gold.setText(game.save.data.gold.toString())
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

    protected fun row(highlight: Color? = null): Table = Table().apply {
        background = if (highlight != null) NinePatchDrawable(ui.card).tint(highlight) else ui.panel
        pad(6f, 7f, 7f, 7f)
    }
}

// --------------------------------------------------------------------------- Kahramanlar
class HeroesScreen(game: LastLanternGame, private val returnToStages: Boolean = false) : MenuPage(game, "heroes.title") {
    init {
        build()
    }

    private fun build() {
        body.clearChildren()
        val d = game.save.data
        for (c in Content.characters) {
            val unlocked = c.id in d.unlockedCharacters
            val selected = d.selectedCharacter == c.id
            val r = row(if (selected) UiKit.GOLD else null)
            ui.iconCell(r, "portrait_${c.id}", 3).padRight(8f)
            val col = Table()
            col.add(ui.label(t["char.${c.id}"], if (unlocked) Color.WHITE else UiKit.MUTED, align = Align.left)).left().row()
            col.add(ui.label(t["char.${c.id}.desc"], UiKit.DIM, wrap = true, align = Align.left)).width(130f).left().row()
            val w = Content.weapons.getValue(c.startWeapon)
            val sw = Table()
            sw.add(ui.icon(w.icon)).padRight(3f)
            sw.add(ui.label(t["weapon.${w.id}"], UiKit.MUTED, align = Align.left)).left()
            col.add(sw).left().padTop(2f).row()
            r.add(col).expandX().left()
            val action = Table()
            when {
                selected -> action.add(ui.label(t["heroes.selected"], UiKit.GOLD))
                unlocked -> action.add(ui.button(t["heroes.select"]) {
                    d.selectedCharacter = c.id
                    game.save.save()
                    if (returnToStages) game.go(StageSelectScreen(game)) else build()
                })
                else -> {
                    val buy = ui.button(c.price.toString(), primaryStyle = true) {
                        if (d.gold >= c.price) {
                            d.gold -= c.price
                            d.unlockedCharacters.add(c.id)
                            d.selectedCharacter = c.id
                            Achievements.evaluate(d, null)
                            game.save.save()
                            game.audio.play(Sfx.CHEST)
                            refreshGold()
                            build()
                        }
                    }
                    buy.isDisabled = d.gold < c.price
                    buy.add(ui.icon("pick_coin")).padLeft(3f)
                    action.add(buy).width(72f).row()
                    c.unlockAchievement?.let {
                        action.add(ui.label(t.f("heroes.or", t["ach.$it"]), UiKit.MUTED, wrap = true)).width(78f).padTop(2f)
                    }
                }
            }
            r.add(action).width(80f)
            body.add(r).width(258f).padBottom(5f).row()
        }
    }

    override fun onBack() = game.go(if (returnToStages) StageSelectScreen(game) else MenuScreen(game))
}

// --------------------------------------------------------------------------- Kamp
class CampScreen(game: LastLanternGame) : MenuPage(game, "camp.title") {
    init {
        build()
    }

    private fun build() {
        body.clearChildren()
        val d = game.save.data
        body.add(ui.label(t["camp.desc"], UiKit.DIM, wrap = true)).width(250f).padBottom(6f).row()
        for (m in Content.meta) {
            val rank = d.meta[m.id] ?: 0
            val r = row()
            val iconBox = Table()
            iconBox.background = ui.slot
            ui.iconCell(iconBox, m.icon).pad(2f)
            r.add(iconBox).padRight(6f)
            val col = Table()
            col.add(ui.label(t["meta.${m.id}"], Color.WHITE, align = Align.left)).left().row()
            col.add(ui.label(t["meta.${m.id}.desc"], UiKit.DIM, wrap = true, align = Align.left)).width(120f).left().row()
            val pips = Table()
            for (k in 0 until m.maxRank) {
                val pip = com.badlogic.gdx.scenes.scene2d.ui.Image(game.assets.pixel)
                pip.color = if (k < rank) UiKit.GOLD else Color(0.3f, 0.26f, 0.4f, 1f)
                pips.add(pip).size(5f, 3f).padRight(2f)
            }
            col.add(pips).left().padTop(3f).row()
            r.add(col).expandX().left()
            if (rank >= m.maxRank) {
                r.add(ui.label(t["common.max"], UiKit.GOLD)).width(64f)
            } else {
                val cost = m.cost(rank)
                val btn = ui.button(t.f("camp.buy", cost), primaryStyle = d.gold >= cost) {
                    if (d.gold >= cost) {
                        d.gold -= cost
                        d.meta[m.id] = rank + 1
                        Achievements.evaluate(d, null)
                        game.save.save()
                        game.audio.play(Sfx.LEVEL_UP, 1.2f)
                        refreshGold()
                        build()
                    }
                }
                btn.isDisabled = d.gold < cost
                btn.add(ui.icon("pick_coin")).padLeft(3f)
                r.add(btn).width(64f)
            }
            body.add(r).width(258f).padBottom(4f).row()
        }
        if (d.meta.values.sum() > 0) {
            body.add(ui.button(t["camp.refund"], sound = Sfx.UI_BACK) {
                stage.addActor(ConfirmOverlay(game, t["camp.refund_confirm"]) {
                    var back = 0
                    for (m in Content.meta) {
                        val rank = d.meta[m.id] ?: 0
                        for (k in 0 until rank) back += m.cost(k)
                    }
                    d.gold += back
                    d.meta.clear()
                    game.save.save()
                    refreshGold()
                    build()
                })
            }).padTop(6f).padBottom(10f).row()
        }
    }
}

// --------------------------------------------------------------------------- Basarimlar
class AchievementsScreen(game: LastLanternGame) : MenuPage(game, "ach.title") {
    init {
        val d = game.save.data
        body.add(ui.label(t.f("ach.progress", d.achievements.size, Achievements.all.size), UiKit.GOLD)).padBottom(6f).row()
        val sorted = Achievements.all.sortedBy { if (it.id in d.achievements) 1 else 0 }
        for (a in sorted) {
            val done = a.id in d.achievements
            val r = row(if (done) UiKit.GOLD else null)
            val icon = ui.icon(a.icon)
            if (!done) icon.color = Color(0.35f, 0.32f, 0.42f, 1f)
            r.add(icon).size(game.assets.region(a.icon).regionWidth.toFloat(), game.assets.region(a.icon).regionHeight.toFloat()).padRight(7f)
            val col = Table()
            col.add(ui.label(t["ach.${a.id}"], if (done) Color.WHITE else UiKit.DIM, align = Align.left)).left().row()
            col.add(ui.label(t["ach.${a.id}.desc"], UiKit.MUTED, wrap = true, align = Align.left)).width(160f).left().row()
            if (!done) a.progress?.invoke(d)?.let { (cur, max) ->
                col.add(ui.label(t.f("ach.progress", cur, max), UiKit.DIM, align = Align.left)).left().row()
            }
            r.add(col).expandX().left()
            r.add(ui.label(t.f("ach.reward", a.reward), if (done) UiKit.MUTED else UiKit.GOLD)).right()
            body.add(r).width(258f).padBottom(4f).row()
        }
    }
}

// --------------------------------------------------------------------------- Ayarlar
class SettingsScreen(game: LastLanternGame) : MenuPage(game, "settings.title") {
    init {
        build()
    }

    private fun build() {
        body.clearChildren()
        val s = game.settings
        slider("settings.music", s.music) { s.music = it }
        slider("settings.sfx", s.sfx) {
            s.sfx = it
            game.audio.play(Sfx.EMBER)
        }
        toggle("settings.vibration", s.vibration) { s.vibration = !s.vibration; if (s.vibration) game.platform.vibrate(40) }
        toggle("settings.shake", s.screenShake) { s.screenShake = !s.screenShake }
        toggle("settings.numbers", s.damageNumbers) { s.damageNumbers = !s.damageNumbers }
        toggle("settings.flashes", s.reduceFlashes) { s.reduceFlashes = !s.reduceFlashes }
        choice("settings.joystick", if (s.fixedJoystick) t["settings.joystick.fixed"] else t["settings.joystick.floating"]) {
            s.fixedJoystick = !s.fixedJoystick
        }
        val langName = when (s.language) {
            "en" -> "English"
            "tr" -> "Türkçe"
            else -> t["settings.language.auto"]
        }
        choice("settings.language", langName) {
            s.language = when (s.language) {
                "" -> "en"
                "en" -> "tr"
                else -> ""
            }
            game.save.save()
            game.reloadLanguage()
            game.go(SettingsScreen(game))
        }
        if (game.platform.privacyOptionsRequired()) {
            body.add(ui.button(t["settings.privacy_options"]) { game.platform.showPrivacyOptions() }).width(220f).padTop(6f).row()
        }
        body.add(ui.button(t["settings.privacy_policy"]) { game.platform.openUrl(Links.PRIVACY_POLICY) }).width(220f).padTop(6f).row()
        body.add(ui.button(t["settings.restore"]) {
            game.platform.restorePurchases { ok ->
                if (ok) game.save.data.supporterCached = true
                game.save.save()
                stage.addActor(ConfirmOverlayOk(game, if (ok) t["settings.restored"] else t["settings.not_found"]))
            }
        }).width(220f).padTop(6f).row()
        body.add(ui.button(t["settings.reset"], sound = Sfx.UI_BACK) {
            stage.addActor(ConfirmOverlay(game, t["settings.reset_confirm"]) {
                val keepSettings = game.save.data.settings
                game.save.reset()
                game.save.data.settings = keepSettings
                game.save.data.tutorialDone = true
                game.save.save()
                game.go(MenuScreen(game))
            })
        }).width(220f).padTop(6f).row()
        body.add(ui.label(t["settings.credits"], UiKit.MUTED, wrap = true)).width(240f).padTop(12f).row()
        body.add(ui.label(t.f("settings.version", Links.VERSION), UiKit.MUTED)).padTop(4f).padBottom(10f).row()
    }

    private fun slider(key: String, value: Float, onChange: (Float) -> Unit) {
        val r = row()
        r.add(ui.label(t[key], Color.WHITE, align = Align.left)).width(90f).left()
        val style = Slider.SliderStyle(ui.bar, TextureRegionDrawable(game.assets.region("circle8")).tint(UiKit.GOLD))
        style.knobBefore = TextureRegionDrawable(game.assets.pixel).tint(UiKit.EMBER).apply { minHeight = 4f }
        val sl = Slider(0f, 1f, 0.05f, false, style)
        sl.value = value
        sl.addListener(object : ChangeListener() {
            override fun changed(event: ChangeEvent?, actor: com.badlogic.gdx.scenes.scene2d.Actor?) {
                onChange(sl.value)
            }
        })
        r.add(sl).width(140f)
        body.add(r).width(258f).padBottom(4f).row()
    }

    private fun toggle(key: String, on: Boolean, flip: () -> Unit) =
        choice(key, if (on) t["settings.on"] else t["settings.off"], flip)

    private fun choice(key: String, value: String, change: () -> Unit) {
        val r = row()
        r.add(ui.label(t[key], Color.WHITE, align = Align.left)).expandX().left()
        r.add(ui.button(value) {
            change()
            game.save.save()
            build()
        }).width(100f)
        body.add(r).width(258f).padBottom(4f).row()
    }

    override fun hide() {
        game.save.save()
        super.hide()
    }
}

/** Tek dugmeli bilgi kutusu. */
class ConfirmOverlayOk(game: LastLanternGame, message: String) : com.lastlantern.ui.Overlay(game) {
    init {
        val box = Table()
        box.background = ui.panel
        box.pad(10f)
        box.add(ui.label(message, Color.WHITE, wrap = true)).width(200f).padBottom(10f).row()
        box.add(ui.button(t["common.ok"]) { close() }).width(100f)
        add(box)
    }
}

// --------------------------------------------------------------------------- Destekci
class SupporterScreen(game: LastLanternGame) : MenuPage(game, "supporter.title") {
    init {
        build()
    }

    private fun build() {
        body.clearChildren()
        val lantern = ui.icon("icon_p_oil", 4)
        lantern.color = UiKit.GOLD
        body.add(lantern).size(64f, 64f).padTop(10f).padBottom(12f).row()
        for (k in 1..4) {
            val r = Table()
            r.add(ui.icon("icon_ui_check")).padRight(6f)
            r.add(ui.label(t["supporter.line$k"], Color.WHITE, wrap = true, align = Align.left)).width(210f).left()
            body.add(r).padBottom(5f).row()
        }
        if (game.supporter) {
            body.add(ui.label(t["supporter.owned"], UiKit.GOLD, wrap = true)).width(240f).padTop(12f).row()
        } else {
            val price = game.platform.supporterPrice()
            val label = if (price != null) t.f("supporter.buy", price) else t["supporter.buy_noprice"]
            val buy = ui.button(label, primaryStyle = true) {
                game.platform.buySupporter { ok ->
                    if (ok) {
                        game.save.data.supporterCached = true
                        game.save.save()
                        game.audio.play(Sfx.EVOLVE)
                        build()
                    } else {
                        stage.addActor(ConfirmOverlayOk(game, t["supporter.failed"]))
                    }
                }
            }
            buy.label.setFontScale(2f)
            body.add(buy).width(220f).height(34f).padTop(14f).row()
            body.add(ui.button(t["settings.restore"]) {
                game.platform.restorePurchases { ok ->
                    if (ok) {
                        game.save.data.supporterCached = true
                        game.save.save()
                    }
                    build()
                }
            }).width(220f).padTop(8f).row()
        }
    }
}
