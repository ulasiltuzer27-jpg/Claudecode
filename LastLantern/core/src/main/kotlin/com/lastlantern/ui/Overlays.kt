package com.lastlantern.ui

import com.badlogic.gdx.graphics.Color
import com.badlogic.gdx.scenes.scene2d.Touchable
import com.badlogic.gdx.scenes.scene2d.actions.Actions
import com.badlogic.gdx.scenes.scene2d.ui.Image
import com.badlogic.gdx.scenes.scene2d.ui.Table
import com.badlogic.gdx.scenes.scene2d.utils.NinePatchDrawable
import com.badlogic.gdx.utils.Align
import com.lastlantern.LastLanternGame
import com.lastlantern.content.Content
import com.lastlantern.content.WStats
import com.lastlantern.content.WeaponDef
import com.lastlantern.sim.Offer
import com.lastlantern.sim.Sfx
import com.lastlantern.sim.World

/** Kart/odul metinleri: ad, etiket (YENI / Sv 3 / EVRIM), aciklama satirlari. */
class OfferText(private val game: LastLanternGame) {
    private val t get() = game.i18n

    fun title(o: Offer): String = when (o) {
        is Offer.NewWeapon -> t["weapon.${o.def.id}"]
        is Offer.WeaponUp -> t["weapon.${o.weapon.def.id}"]
        is Offer.NewPassive -> t["passive.${o.def.id}"]
        is Offer.PassiveUp -> t["passive.${o.def.id}"]
        is Offer.Evolve -> t["weapon.${o.into.id}"]
        is Offer.Gold -> t.f("offer.gold", o.amount)
        is Offer.Heal -> t.f("offer.heal", o.amount)
    }

    fun tag(o: Offer): String = when (o) {
        is Offer.NewWeapon, is Offer.NewPassive -> t["offer.new"]
        is Offer.WeaponUp -> t.f("common.level", o.weapon.level + 1)
        is Offer.PassiveUp -> t.f("common.level", o.toLevel)
        is Offer.Evolve -> t["offer.evolve"]
        else -> ""
    }

    fun lines(o: Offer): List<String> = when (o) {
        is Offer.NewWeapon -> listOf(t["weapon.${o.def.id}.desc"])
        is Offer.WeaponUp -> delta(o.weapon.def.levels[o.weapon.level - 1])
        is Offer.NewPassive -> listOf(t["passive.${o.def.id}.desc"])
        is Offer.PassiveUp -> listOf(t["passive.${o.def.id}.desc"])
        is Offer.Evolve -> listOf(t["weapon.${o.into.id}.desc"])
        else -> emptyList()
    }

    fun evoHint(o: Offer): String? {
        val def: WeaponDef = when (o) {
            is Offer.NewWeapon -> o.def
            is Offer.WeaponUp -> o.weapon.def
            else -> return null
        }
        val p = def.evolvePassive ?: return null
        return t.f("upg.evolve_hint", t["passive.$p"])
    }

    fun color(o: Offer): Color = when (o) {
        is Offer.NewWeapon -> UiKit.CARD_NEW
        is Offer.NewPassive, is Offer.PassiveUp -> UiKit.CARD_PASSIVE
        is Offer.Evolve -> UiKit.CARD_EVO
        else -> UiKit.CARD_UP
    }

    private fun delta(d: WStats): List<String> {
        val out = ArrayList<String>()
        if (d.damage > 0f) out.add(t.f("upg.damage", t.num(d.damage, 0)))
        if (d.cooldown < 0f) out.add(t.f("upg.cooldown", t.num(-d.cooldown, 2)))
        if (d.amount > 0) out.add(t.f("upg.amount", d.amount))
        if (d.pierce > 0) out.add(t.f("upg.pierce", d.pierce))
        if (d.area > 0f) out.add(t.f("upg.area", (d.area * 100).toInt()))
        if (d.speed > 0f) out.add(t.f("upg.speed", t.num(d.speed, 1)))
        if (d.duration > 0f) out.add(t.f("upg.duration", t.num(d.duration, 1)))
        return out
    }
}

/** Ortak: ekrani kaplayan karartma + ortalanmis icerik. */
abstract class Overlay(protected val game: LastLanternGame) : Table() {
    protected val ui get() = game.ui
    protected val t get() = game.i18n

    init {
        setFillParent(true)
        background = game.ui.dim
        touchable = Touchable.enabled
        color.a = 0f
        addAction(Actions.fadeIn(0.15f))
    }

    fun close() {
        touchable = Touchable.disabled
        addAction(Actions.sequence(Actions.fadeOut(0.12f), Actions.removeActor()))
    }

    /** Kart secimi: yanlislikla secmeyi onlemek icin kisa kilit. */
    protected fun guard(seconds: Float) {
        touchable = Touchable.disabled
        addAction(Actions.sequence(Actions.delay(seconds), Actions.run { touchable = Touchable.enabled }))
    }
}

class LevelUpOverlay(game: LastLanternGame, private val world: World, private val onChosen: () -> Unit) : Overlay(game) {
    private val text = OfferText(game)

    init {
        guard(0.4f)
    }

    private fun build() {
        clearChildren()
        val w = (stage?.width ?: 270f).coerceAtMost(300f)
        add(ui.label(t["levelup.title"], UiKit.GOLD, scale = 2)).padBottom(8f).row()
        val offers = world.upgrades.offers
        offers.forEachIndexed { i, o ->
            val c = card(o, w - 20f)
            add(c).width(w - 20f).padBottom(5f).row()
            c.color.a = 0f
            c.addAction(Actions.sequence(Actions.delay(0.05f + i * 0.07f),
                Actions.parallel(Actions.fadeIn(0.15f), Actions.sequence(Actions.moveBy(0f, -6f), Actions.moveBy(0f, 6f, 0.15f)))))
        }
        if (world.rerollsLeft > 0) {
            add(ui.button(t.f("levelup.reroll", world.rerollsLeft)) {
                if (world.upgrades.reroll()) build()
            }).padTop(4f).row()
        }
    }

    override fun setStage(stage: com.badlogic.gdx.scenes.scene2d.Stage?) {
        val first = this.stage == null && stage != null
        super.setStage(stage)
        if (first) build()
    }

    private fun card(o: Offer, width: Float): Table {
        val c = Table()
        val bg = NinePatchDrawable(ui.card).tint(text.color(o))
        c.background = bg
        c.pad(5f, 6f, 6f, 6f)
        val iconBox = Table()
        iconBox.background = ui.slot
        ui.iconCell(iconBox, o.icon, 2).pad(2f)
        c.add(iconBox).top().padRight(6f)
        val col = Table()
        val head = Table()
        head.add(ui.label(text.title(o), Color.WHITE, align = Align.left)).expandX().left()
        val tag = text.tag(o)
        if (tag.isNotEmpty()) head.add(ui.label(tag, text.color(o))).right()
        col.add(head).growX().row()
        for (line in text.lines(o)) col.add(ui.label(line, UiKit.DIM, wrap = true, align = Align.left)).growX().padTop(2f).row()
        text.evoHint(o)?.let { col.add(ui.label(it, UiKit.MUTED, wrap = true, align = Align.left)).growX().padTop(2f).row() }
        c.add(col).growX().top()
        ui.onClick(c) {
            game.audio.play(Sfx.LEVEL_UP, 1.4f, 0.5f)
            world.upgrades.choose(o)
            if (world.state == com.lastlantern.sim.RunState.LEVEL_UP) {
                build()
                guard(0.25f)
            } else {
                close()
                onChosen()
            }
        }
        return c
    }
}

class ChestOverlay(game: LastLanternGame, private val world: World, private val onDone: () -> Unit) : Overlay(game) {
    private val text = OfferText(game)

    init {
        val chest = Image(game.assets.frames("pick_chest")[0])
        val box = Table()
        add(ui.label(t["chest.title"], UiKit.GOLD, scale = 2)).padBottom(10f).row()
        add(chest).size(42f, 30f).padBottom(10f).row()
        add(box).row()
        chest.setOrigin(Align.center)
        chest.addAction(Actions.sequence(
            Actions.repeat(6, Actions.sequence(Actions.rotateBy(6f, 0.04f), Actions.rotateBy(-6f, 0.04f))),
            Actions.run {
                chest.drawable = com.badlogic.gdx.scenes.scene2d.utils.TextureRegionDrawable(game.assets.frames("pick_chest")[1])
                game.audio.play(Sfx.COIN)
                showRewards(box)
            },
        ))
        guard(0.6f)
    }

    private fun showRewards(box: Table) {
        val rewards = world.upgrades.chestRewards
        rewards.forEachIndexed { i, o ->
            val row = Table()
            row.background = NinePatchDrawable(ui.card).tint(text.color(o))
            row.pad(4f, 6f, 5f, 6f)
            ui.iconCell(row, o.icon, 1).padRight(6f)
            row.add(ui.label(text.title(o), Color.WHITE, align = Align.left)).expandX().left()
            val tag = if (o is Offer.Evolve) t["chest.evolved"] else text.tag(o)
            row.add(ui.label(tag, text.color(o))).padLeft(6f)
            box.add(row).width(220f).padBottom(4f).row()
            row.color.a = 0f
            row.addAction(Actions.sequence(Actions.delay(0.15f + i * 0.2f), Actions.fadeIn(0.2f),
                Actions.run { game.audio.play(if (o is Offer.Evolve) Sfx.EVOLVE else Sfx.LEVEL_UP, 1.2f + i * 0.1f, 0.6f) }))
        }
        val gold = Table()
        gold.add(ui.icon("pick_coin")).padRight(4f)
        gold.add(ui.label("+${world.upgrades.chestGold}", UiKit.GOLD))
        box.add(gold).padTop(6f).row()
        val cont = ui.button(t["common.continue"], primaryStyle = true) {
            close()
            onDone()
        }
        box.add(cont).padTop(10f).width(140f).row()
        cont.color.a = 0f
        cont.addAction(Actions.sequence(Actions.delay(0.3f + rewards.size * 0.2f), Actions.fadeIn(0.2f)))
    }
}

class PauseOverlay(game: LastLanternGame, private val world: World, onResume: () -> Unit, onGiveUp: () -> Unit) : Overlay(game) {
    init {
        add(ui.label(t["pause.title"], Color.WHITE, scale = 2)).padBottom(10f).row()
        val build = Table()
        build.background = ui.panel
        build.pad(6f)
        val wrow = Table()
        for (wp in world.weapons) {
            val cell = Table()
            ui.iconCell(cell, wp.def.icon, 1).row()
            cell.add(ui.label(if (wp.evolved) t["common.max"] else t.f("common.level", wp.level), if (wp.evolved) UiKit.GOLD else UiKit.DIM))
            wrow.add(cell).pad(2f)
        }
        build.add(wrow).row()
        val prow = Table()
        for ((id, lvl) in world.passives) {
            val cell = Table()
            ui.iconCell(cell, Content.passives.getValue(id).icon, 1).row()
            cell.add(ui.label(t.f("common.level", lvl), UiKit.DIM))
            prow.add(cell).pad(2f)
        }
        build.add(prow).padTop(4f).row()
        add(build).padBottom(12f).row()
        add(ui.button(t["pause.resume"], primaryStyle = true) {
            close()
            onResume()
        }).width(160f).padBottom(6f).row()
        val sound = ui.button(soundLabel()) {}
        sound.clearListeners()
        ui.onClick(sound) {
            val s = game.settings
            val on = s.music > 0f || s.sfx > 0f
            s.music = if (on) 0f else 0.7f
            s.sfx = if (on) 0f else 0.8f
            sound.setText(soundLabel())
            game.save.save()
        }
        add(sound).width(160f).padBottom(6f).row()
        add(ui.button(t["pause.giveup"], sound = Sfx.UI_BACK) {
            val confirm = ConfirmOverlay(game, t["pause.confirm"]) {
                close()
                onGiveUp()
            }
            stage.addActor(confirm)
        }).width(160f).row()
    }

    private fun soundLabel(): String {
        val s = game.settings
        val on = s.music > 0f || s.sfx > 0f
        return "${t["settings.sfx"]}: ${if (on) t["settings.on"] else t["settings.off"]}"
    }
}

class ConfirmOverlay(game: LastLanternGame, message: String, onYes: () -> Unit) : Overlay(game) {
    init {
        val box = Table()
        box.background = ui.panel
        box.pad(10f)
        box.add(ui.label(message, Color.WHITE, wrap = true)).width(200f).colspan(2).padBottom(10f).row()
        box.add(ui.button(t["common.no"]) { close() }).width(90f).padRight(6f)
        box.add(ui.button(t["common.yes"], primaryStyle = true) {
            close()
            onYes()
        }).width(90f)
        add(box)
        guard(0.2f)
    }
}

class ReviveOverlay(game: LastLanternGame, onRevive: () -> Unit, onDecline: () -> Unit) : Overlay(game) {
    init {
        add(ui.label(t["revive.title"], UiKit.EMBER, scale = 2, wrap = true)).width(260f).padBottom(14f).row()
        add(ui.icon("icon_ui_heart", 3)).size(21f, 18f).padBottom(14f).row()
        val supporter = game.supporter
        val ready = supporter || game.platform.rewardedReady()
        val label = if (supporter) t["revive.supporter"] else t["revive.ad"]
        val btn = ui.button(label, primaryStyle = true) {
            if (supporter) {
                close()
                onRevive()
            } else {
                touchable = Touchable.disabled
                game.audio.pause()
                game.platform.showRewarded("revive", onReward = {
                    game.audio.resume()
                    close()
                    onRevive()
                }, onClosed = {
                    game.audio.resume()
                    touchable = Touchable.enabled
                })
            }
        }
        if (!ready) btn.isDisabled = true
        add(btn).width(190f).padBottom(6f).row()
        if (!ready) add(ui.label(t["ad.unavailable"], UiKit.MUTED, wrap = true)).width(200f).padBottom(6f).row()
        add(ui.button(t["revive.no"], sound = Sfx.UI_BACK) {
            close()
            onDecline()
        }).width(190f).row()
        guard(0.8f)
    }
}
