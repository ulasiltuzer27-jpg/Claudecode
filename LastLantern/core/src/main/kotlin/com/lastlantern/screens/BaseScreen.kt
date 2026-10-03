package com.lastlantern.screens

import com.badlogic.gdx.Gdx
import com.badlogic.gdx.Input
import com.badlogic.gdx.InputAdapter
import com.badlogic.gdx.InputMultiplexer
import com.badlogic.gdx.InputProcessor
import com.badlogic.gdx.ScreenAdapter
import com.badlogic.gdx.scenes.scene2d.Actor
import com.badlogic.gdx.scenes.scene2d.Stage
import com.badlogic.gdx.scenes.scene2d.actions.Actions
import com.badlogic.gdx.scenes.scene2d.ui.Table
import com.badlogic.gdx.utils.viewport.ScreenViewport
import com.lastlantern.LastLanternGame
import com.lastlantern.assets.I18n
import com.lastlantern.ui.UiKit

/**
 * Ortak ekran: tam sayi olcekli Scene2D sahnesi, guvenli alan (centik)
 * boslugu, geri tusu ve kisa giris animasyonu.
 */
abstract class BaseScreen(val game: LastLanternGame) : ScreenAdapter() {
    protected val viewport = ScreenViewport().apply { unitsPerPixel = 1f / game.uiScale }
    val stage = Stage(viewport, game.batch)
    /** Guvenli alan icindeki kok tablo. */
    protected val root = Table().apply { setFillParent(true) }
    protected val ui: UiKit get() = game.ui
    protected val t: I18n get() = game.i18n

    /** false: geri hareketi sistem tarafindan ele alinir (ana menude uygulamadan cikis). */
    open val handlesBack: Boolean = true

    private val keys = object : InputAdapter() {
        override fun keyDown(keycode: Int): Boolean {
            if (keycode == Input.Keys.BACK || keycode == Input.Keys.ESCAPE) {
                onBack()
                return true
            }
            return onKey(keycode)
        }
    }

    init {
        stage.addActor(root)
    }

    open fun onBack() {}

    open fun onKey(keycode: Int): Boolean = false

    protected open fun extraInput(): InputProcessor? = null

    override fun show() {
        val mux = InputMultiplexer(keys, stage)
        extraInput()?.let { mux.addProcessor(it) }
        Gdx.input.inputProcessor = mux
    }

    override fun hide() {
        if (Gdx.input.inputProcessor != null) Gdx.input.inputProcessor = null
    }

    override fun resize(width: Int, height: Int) {
        game.computeUiScale(width, height)
        viewport.unitsPerPixel = 1f / game.uiScale
        viewport.update(width, height, true)
        applySafeArea()
    }

    protected fun applySafeArea() {
        val s = game.uiScale.toFloat()
        val g = Gdx.graphics
        root.pad(
            g.safeInsetTop / s + 4f,
            g.safeInsetLeft / s + 6f,
            g.safeInsetBottom / s + 6f,
            g.safeInsetRight / s + 6f,
        )
        root.invalidateHierarchy()
    }

    val uiW get() = stage.width
    val uiH get() = stage.height

    override fun render(delta: Float) {
        val dt = delta.coerceAtMost(1f / 20f)
        drawBackground(dt)
        stage.act(dt)
        stage.draw()
    }

    protected open fun drawBackground(dt: Float) {}

    /** Elemanlar asagidan kayarak ve sirayla belirir. */
    protected fun enter(vararg actors: Actor, delay: Float = 0f) {
        actors.forEachIndexed { i, a ->
            a.color.a = 0f
            a.addAction(Actions.sequence(
                Actions.delay(delay + i * 0.05f),
                Actions.parallel(Actions.fadeIn(0.2f), Actions.sequence(Actions.moveBy(0f, -4f), Actions.moveBy(0f, 4f, 0.2f))),
            ))
        }
    }

    override fun dispose() {
        stage.dispose()
    }

    protected fun time(s: Float) = I18n.time(s)
}
