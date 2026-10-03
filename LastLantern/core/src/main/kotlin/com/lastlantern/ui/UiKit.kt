package com.lastlantern.ui

import com.badlogic.gdx.graphics.Color
import com.badlogic.gdx.graphics.g2d.BitmapFont
import com.badlogic.gdx.graphics.g2d.NinePatch
import com.badlogic.gdx.graphics.g2d.TextureRegion
import com.badlogic.gdx.scenes.scene2d.Actor
import com.badlogic.gdx.scenes.scene2d.InputEvent
import com.badlogic.gdx.scenes.scene2d.Touchable
import com.badlogic.gdx.scenes.scene2d.ui.Image
import com.badlogic.gdx.scenes.scene2d.ui.Label
import com.badlogic.gdx.scenes.scene2d.ui.Table
import com.badlogic.gdx.scenes.scene2d.ui.TextButton
import com.badlogic.gdx.scenes.scene2d.utils.ClickListener
import com.badlogic.gdx.scenes.scene2d.utils.Drawable
import com.badlogic.gdx.scenes.scene2d.utils.NinePatchDrawable
import com.badlogic.gdx.scenes.scene2d.utils.TextureRegionDrawable
import com.badlogic.gdx.utils.Align
import com.lastlantern.assets.Assets
import com.lastlantern.assets.Audio
import com.lastlantern.sim.Sfx

/** Piksel arayuz stilleri: fontlar, 9-patch'ler ve hazir bilesenler. */
class UiKit(val a: Assets, private val audio: Audio) {
    val font: BitmapFont = a.font
    val fontOutline: BitmapFont = a.fontOutline

    private fun nine(name: String, split: Int = 4) = NinePatchDrawable(NinePatch(a.region(name), split, split, split, split))

    val panel: Drawable = nine("ui_panel")
    val panelInner: Drawable = nine("ui_panel_inner", 3)
    val card: NinePatchDrawable = nine("ui_card")
    val slot: Drawable = nine("ui_slot", 3)
    val bar: Drawable = nine("ui_bar", 2)
    val dim: Drawable = TextureRegionDrawable(a.pixel).tint(Color(0.03f, 0.02f, 0.06f, 0.78f))
    val white: TextureRegion = a.pixel

    val labelStyle = Label.LabelStyle(fontOutline, Color.WHITE)
    val labelPlain = Label.LabelStyle(font, Color.WHITE)

    private fun buttonStyle(up: String, down: String, color: Color = Color.WHITE) = TextButton.TextButtonStyle().apply {
        this.up = nine(up)
        this.down = nine(down)
        this.disabled = nine("ui_button_disabled")
        font = fontOutline
        fontColor = color
        downFontColor = Color(0.85f, 0.85f, 0.85f, 1f)
        disabledFontColor = Color(0.5f, 0.48f, 0.55f, 1f)
        pressedOffsetY = -1f
    }

    val button = buttonStyle("ui_button", "ui_button_down")
    val primary = buttonStyle("ui_button_primary", "ui_button_primary_down")

    // --------------------------------------------------------------- yardimcilar
    fun label(text: CharSequence, color: Color = Color.WHITE, scale: Int = 1, wrap: Boolean = false,
              align: Int = Align.center): Label = Label(text, labelStyle).apply {
        this.color = color
        setFontScale(scale.toFloat())
        this.wrap = wrap
        setAlignment(align)
    }

    fun button(text: String, primaryStyle: Boolean = false, sound: Sfx = Sfx.UI_CLICK, onClick: () -> Unit): TextButton =
        TextButton(text, if (primaryStyle) primary else button).apply {
            label.setAlignment(Align.center)
            pad(4f, 10f, 5f, 10f)
            addListener(object : ClickListener() {
                override fun clicked(event: InputEvent?, x: Float, y: Float) {
                    if (isDisabled) return
                    audio.play(sound)
                    onClick()
                }
            })
        }

    fun icon(name: String, scale: Int = 1): Image = Image(a.region(name)).apply {
        setSize(prefWidth * scale, prefHeight * scale)
        touchable = Touchable.disabled
    }

    fun iconCell(table: Table, name: String, scale: Int = 1) =
        table.add(icon(name, scale)).size(a.region(name).regionWidth * scale.toFloat(), a.region(name).regionHeight * scale.toFloat())

    fun onClick(actor: Actor, sound: Sfx? = Sfx.UI_CLICK, fn: () -> Unit) {
        actor.touchable = Touchable.enabled
        actor.addListener(object : ClickListener() {
            override fun clicked(event: InputEvent?, x: Float, y: Float) {
                sound?.let { audio.play(it) }
                fn()
            }
        })
    }

    companion object {
        val GOLD = Color(1f, 0.83f, 0.35f, 1f)
        val EMBER = Color(1f, 0.6f, 0.25f, 1f)
        val DIM = Color(0.65f, 0.6f, 0.75f, 1f)
        val MUTED = Color(0.48f, 0.44f, 0.58f, 1f)
        val GREEN = Color(0.55f, 0.95f, 0.55f, 1f)
        val RED = Color(1f, 0.42f, 0.4f, 1f)
        val CYAN = Color(0.45f, 0.85f, 1f, 1f)
        val VIOLET = Color(0.73f, 0.55f, 1f, 1f)

        // Kart cerceve renkleri (seviye kartlari)
        val CARD_NEW = Color(0.45f, 0.75f, 1f, 1f)
        val CARD_UP = Color(0.9f, 0.88f, 0.95f, 1f)
        val CARD_PASSIVE = Color(0.55f, 0.95f, 0.6f, 1f)
        val CARD_EVO = Color(1f, 0.8f, 0.3f, 1f)
    }
}
