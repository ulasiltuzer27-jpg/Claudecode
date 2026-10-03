package com.lastlantern

import com.badlogic.gdx.Game
import com.badlogic.gdx.Gdx
import com.badlogic.gdx.Input
import com.badlogic.gdx.Preferences
import com.badlogic.gdx.Screen
import com.badlogic.gdx.graphics.GL20
import com.badlogic.gdx.graphics.g2d.SpriteBatch
import com.badlogic.gdx.math.Matrix4
import com.lastlantern.assets.Assets
import com.lastlantern.assets.Audio
import com.lastlantern.assets.I18n
import com.lastlantern.platform.PlatformServices
import com.lastlantern.save.KeyValueStore
import com.lastlantern.save.SaveManager
import com.lastlantern.screens.BaseScreen
import com.lastlantern.screens.GameScreen
import com.lastlantern.screens.MenuScreen
import com.lastlantern.ui.UiKit
import kotlin.math.max

/** Masaustu baslaticisinin ekran goruntusu/otomatik oynatma secenekleri. */
class LaunchOptions(
    val capture: String? = null,
    val seed: Long? = null,
    val autoplay: Boolean = false,
    val startScreen: String? = null,
    val language: String? = null,
)

class LastLanternGame(
    val platform: PlatformServices,
    val launch: LaunchOptions = LaunchOptions(),
    private val storeOverride: KeyValueStore? = null,
) : Game() {
    lateinit var assets: Assets
        private set
    lateinit var audio: Audio
        private set
    lateinit var i18n: I18n
        private set
    lateinit var save: SaveManager
        private set
    lateinit var batch: SpriteBatch
        private set
    lateinit var ui: UiKit
        private set

    /** Arayuz olcegi: kisa kenar ~270 birim olacak sekilde tam sayi. */
    var uiScale = 3
        private set

    private var next: Screen? = null
    private var fade = 0f
    private var fadeDir = 0
    private val screenMatrix = Matrix4()

    val settings get() = save.data.settings
    val supporter get() = platform.isSupporter() || save.data.supporterCached

    override fun create() {
        save = SaveManager(storeOverride ?: PrefsStore(Gdx.app.getPreferences("lastlantern")))
        save.load()
        launch.language?.let { save.data.settings.language = it }
        assets = Assets().also { it.load() }
        audio = Audio { save.data.settings }.also { it.load() }
        i18n = I18n().also { it.load(save.data.settings.language) }
        batch = SpriteBatch(4000)
        ui = UiKit(assets, audio)
        Gdx.input.setCatchKey(Input.Keys.BACK, true)
        computeUiScale(Gdx.graphics.width, Gdx.graphics.height)
        val first = when {
            launch.capture != null || launch.startScreen != null -> Capture.firstScreen(this)
            !save.data.tutorialDone -> GameScreen.tutorial(this)
            else -> MenuScreen(this)
        }
        setScreen(first)
    }

    fun computeUiScale(w: Int, h: Int) {
        uiScale = max(1, minOf(w, h) / 270)
    }

    fun reloadLanguage() {
        i18n.load(save.data.settings.language)
    }

    /** Ekranlar arasi kisa kararma gecisi. */
    fun go(screen: Screen) {
        if (launch.capture != null) {
            setScreen(screen)
            return
        }
        next = screen
        fadeDir = 1
    }

    override fun setScreen(screen: Screen?) {
        val old = this.screen
        super.setScreen(screen)
        old?.dispose()
        platform.setBackHandled((screen as? BaseScreen)?.handlesBack ?: true)
    }

    /** Android geri tusu/hareketi (OnBackInvokedCallback) buraya gelir. */
    fun onBack() {
        (screen as? BaseScreen)?.onBack()
    }

    override fun render() {
        val dt = Gdx.graphics.deltaTime.coerceAtMost(1f / 20f)
        audio.update(dt)
        // Magaza okunduktan sonra yerel kopyayi gercek durumla esitle (iade dahil)
        if (platform.purchasesLoaded() && save.data.supporterCached != platform.isSupporter()) {
            save.data.supporterCached = platform.isSupporter()
            save.save()
        }
        Gdx.gl.glClearColor(0.05f, 0.04f, 0.08f, 1f)
        Gdx.gl.glClear(GL20.GL_COLOR_BUFFER_BIT)
        super.render()
        if (fadeDir != 0) {
            fade += dt * 6f * fadeDir
            if (fadeDir > 0 && fade >= 1f) {
                fade = 1f
                next?.let { setScreen(it) }
                next = null
                fadeDir = -1
            } else if (fadeDir < 0 && fade <= 0f) {
                fade = 0f
                fadeDir = 0
            }
            screenMatrix.setToOrtho2D(0f, 0f, Gdx.graphics.width.toFloat(), Gdx.graphics.height.toFloat())
            batch.projectionMatrix = screenMatrix
            batch.begin()
            batch.setColor(0.03f, 0.02f, 0.06f, fade)
            batch.draw(assets.pixel, 0f, 0f, Gdx.graphics.width.toFloat(), Gdx.graphics.height.toFloat())
            batch.setColor(1f, 1f, 1f, 1f)
            batch.end()
        }
    }

    override fun resize(width: Int, height: Int) {
        computeUiScale(width, height)
        super.resize(width, height)
    }

    override fun pause() {
        super.pause()
        save.save()
        audio.pause()
    }

    override fun resume() {
        super.resume()
        audio.resume()
    }

    override fun dispose() {
        screen?.dispose()
        save.save()
        assets.dispose()
        audio.dispose()
        batch.dispose()
    }
}

/** libGDX Preferences (Android: SharedPreferences) uzerinde kayit deposu. */
class PrefsStore(private val prefs: Preferences) : KeyValueStore {
    override fun get(key: String): String? = if (prefs.contains(key)) prefs.getString(key) else null
    override fun put(key: String, value: String) {
        prefs.putString(key, value)
    }
    override fun flush() = prefs.flush()
}
