package com.lastlantern.desktop

import com.badlogic.gdx.backends.lwjgl3.Lwjgl3Application
import com.badlogic.gdx.backends.lwjgl3.Lwjgl3ApplicationConfiguration
import com.lastlantern.LastLanternGame

fun main(args: Array<String>) {
    val config = Lwjgl3ApplicationConfiguration().apply {
        setTitle("Last Lantern")
        setWindowedMode(540, 960)
        useVsync(true)
        setForegroundFPS(60)
    }
    Lwjgl3Application(LastLanternGame(), config)
}
