package com.lastlantern.android

import android.os.Build
import android.os.Bundle
import android.window.OnBackInvokedCallback
import android.window.OnBackInvokedDispatcher
import com.badlogic.gdx.Gdx
import com.badlogic.gdx.Input
import com.badlogic.gdx.backends.android.AndroidApplication
import com.badlogic.gdx.backends.android.AndroidApplicationConfiguration
import com.lastlantern.LastLanternGame

class AndroidLauncher : AndroidApplication() {
    private lateinit var game: LastLanternGame
    private lateinit var services: AndroidPlatform
    private var backCallback: Any? = null
    private var backRegistered = false

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        services = AndroidPlatform(this)
        game = LastLanternGame(services)
        val config = AndroidApplicationConfiguration().apply {
            useImmersiveMode = true
            renderUnderCutout = true
            useAccelerometer = false
            useCompass = false
            useGyroscope = false
            useRotationVectorSensor = false
            useGL30 = false
            numSamples = 0
        }
        initialize(game, config)
        services.start()
    }

    /**
     * Geri hareketi. Android 13+ (ve targetSdk 36 ile zorunlu olarak) geri tusu
     * KeyEvent olarak gelmez; OnBackInvokedCallback kaydedilir. Oyun geri
     * hareketini istemedigi ekranda (ana menu) geri cagri kaldirilir ve sistem
     * kendi davranisini (uygulamayi arka plana alma, ongoruculu geri animasyonu)
     * uygular.
     */
    fun setBackHandled(handled: Boolean) {
        runOnUiThread {
            if (Build.VERSION.SDK_INT >= 33) {
                val cb = (backCallback as? OnBackInvokedCallback) ?: OnBackInvokedCallback {
                    Gdx.app?.postRunnable { game.onBack() }
                }.also { backCallback = it }
                if (handled && !backRegistered) {
                    onBackInvokedDispatcher.registerOnBackInvokedCallback(OnBackInvokedDispatcher.PRIORITY_DEFAULT, cb)
                    backRegistered = true
                } else if (!handled && backRegistered) {
                    onBackInvokedDispatcher.unregisterOnBackInvokedCallback(cb)
                    backRegistered = false
                }
            } else {
                Gdx.input?.setCatchKey(Input.Keys.BACK, handled)
            }
        }
    }

    override fun onDestroy() {
        services.dispose()
        super.onDestroy()
    }
}
