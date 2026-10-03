package com.lastlantern.desktop

import com.badlogic.gdx.ApplicationListener
import com.badlogic.gdx.Gdx
import com.badlogic.gdx.graphics.Pixmap
import com.badlogic.gdx.graphics.PixmapIO
import com.badlogic.gdx.backends.lwjgl3.Lwjgl3Application
import com.badlogic.gdx.backends.lwjgl3.Lwjgl3ApplicationConfiguration
import com.badlogic.gdx.utils.BufferUtils
import com.badlogic.gdx.utils.ScreenUtils
import com.lastlantern.LastLanternGame
import com.lastlantern.LaunchOptions
import com.lastlantern.platform.FakePlatform
import com.lastlantern.save.MemoryStore

/**
 * Masaustu baslatici (gelistirme ve ekran goruntusu icin; magazaya gitmez).
 *
 *   --size 1080x1920      pencere boyutu (telefon cozunurlugu)
 *   --capture <senaryo>   Capture.scenarios'tan biri; --out dosyasina PNG yazar ve cikar
 *   --frames N            ekran goruntusunden once kac kare cizilsin (varsayilan 150)
 *   --autoplay            bot oynar
 *   --seed N, --lang tr|en
 */
fun main(args: Array<String>) {
    val a = args.toList()
    fun opt(name: String): String? = a.indexOf(name).takeIf { it >= 0 }?.let { a.getOrNull(it + 1) }
    val size = (opt("--size") ?: "540x960").split("x").map { it.toInt() }
    val capture = opt("--capture")
    val out = opt("--out") ?: "capture.png"
    val frames = opt("--frames")?.toInt() ?: 150
    val launch = LaunchOptions(
        capture = capture,
        seed = opt("--seed")?.toLong(),
        autoplay = "--autoplay" in a,
        startScreen = opt("--screen"),
        language = opt("--lang"),
    )
    val config = Lwjgl3ApplicationConfiguration().apply {
        setTitle("Last Lantern")
        setWindowedMode(size[0], size[1])
        useVsync(capture == null)
        setForegroundFPS(60)
        setResizable(capture == null)
    }
    val game = LastLanternGame(DesktopPlatform(), launch, if (capture != null) MemoryStore() else null)
    Lwjgl3Application(if (capture != null) CaptureListener(game, out, frames) else game, config)
}

class DesktopPlatform : FakePlatform() {
    override fun openUrl(url: String) {
        Gdx.net.openURI(url)
    }

    override fun exitApp() = Gdx.app.exit()
}

/** Oyunu N kare calistirir, ekrani PNG olarak kaydeder ve cikar. */
class CaptureListener(private val game: LastLanternGame, private val out: String, private val frames: Int) : ApplicationListener {
    private var n = 0

    override fun create() = game.create()
    override fun resize(width: Int, height: Int) = game.resize(width, height)
    override fun pause() = game.pause()
    override fun resume() = game.resume()
    override fun dispose() = game.dispose()

    override fun render() {
        game.render()
        n++
        if (n == frames) {
            val w = Gdx.graphics.backBufferWidth
            val h = Gdx.graphics.backBufferHeight
            val pixels = ScreenUtils.getFrameBufferPixels(0, 0, w, h, true)
            // Alfa kanalini tam opak yap (cerceve tamponu alfasi anlamsiz)
            for (i in 3 until pixels.size step 4) pixels[i] = 255.toByte()
            val pm = Pixmap(w, h, Pixmap.Format.RGBA8888)
            BufferUtils.copy(pixels, 0, pm.pixels, pixels.size)
            PixmapIO.writePNG(Gdx.files.absolute(java.io.File(out).absolutePath), pm, -1, false)
            pm.dispose()
            println("ekran goruntusu: $out (${w}x$h)")
            Gdx.app.exit()
        }
    }
}
