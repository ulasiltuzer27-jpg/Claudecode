package com.lastlantern.android

import android.os.Bundle
import com.badlogic.gdx.backends.android.AndroidApplication
import com.badlogic.gdx.backends.android.AndroidApplicationConfiguration
import com.lastlantern.LastLanternGame

class AndroidLauncher : AndroidApplication() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
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
        initialize(LastLanternGame(), config)
    }
}
