package com.lastlantern

import com.badlogic.gdx.Game
import com.badlogic.gdx.Gdx
import com.badlogic.gdx.graphics.GL20

class LastLanternGame : Game() {
    override fun create() {}

    override fun render() {
        Gdx.gl.glClearColor(0.05f, 0.04f, 0.08f, 1f)
        Gdx.gl.glClear(GL20.GL_COLOR_BUFFER_BIT)
        super.render()
    }
}
