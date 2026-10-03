package com.lastlantern.render

import com.badlogic.gdx.Gdx
import com.badlogic.gdx.graphics.Color
import com.badlogic.gdx.graphics.GL20
import com.badlogic.gdx.graphics.OrthographicCamera
import com.badlogic.gdx.graphics.Pixmap
import com.badlogic.gdx.graphics.Texture
import com.badlogic.gdx.graphics.g2d.SpriteBatch
import com.badlogic.gdx.graphics.glutils.FrameBuffer
import com.badlogic.gdx.graphics.glutils.ShaderProgram
import com.badlogic.gdx.math.Matrix4
import com.badlogic.gdx.utils.Disposable
import kotlin.math.ceil
import kotlin.math.max
import kotlin.math.roundToInt

/**
 * Piksel-mukemmel goruntu hatti.
 *
 * Dunya sanal cozunurlukte (kisa kenar ~360 piksel) uc ayri tampona cizilir:
 *   scene   : zemin, dusmanlar, mermiler (isiktan etkilenir)
 *   light   : ortam karanligi + toplamsal isiklar
 *   overlay : karanlikta parlayan gozler, hasar sayilari (isiktan etkilenmez)
 * Sonra ekrana TAM SAYI olcekle, en yakin komsu filtresiyle buyutulur:
 * pikseller her cihazda keskin ve esit boyutlu kalir.
 *
 * Birlestirme shader'i isigi kademelere boler ve 4x4 Bayer ile titrestirir;
 * fener isigi yumusak bir gradyan degil, piksel sanata yakisan halkalar olur.
 */
class PixelView : Disposable {
    var scale = 3
        private set
    var vw = 360
        private set
    var vh = 640
        private set
    var screenW = 1
        private set
    var screenH = 1
        private set

    var scene: FrameBuffer? = null
        private set
    var light: FrameBuffer? = null
        private set
    var overlay: FrameBuffer? = null
        private set

    val camera = OrthographicCamera()
    private val screenMatrix = Matrix4()
    private val composite: ShaderProgram
    var lightSteps = 7f
    var ditherAmount = 0.85f
    var exposure = 1.15f

    init {
        ShaderProgram.pedantic = false
        composite = ShaderProgram(VERT, FRAG)
        if (!composite.isCompiled) Gdx.app.error("PixelView", composite.log)
    }

    fun resize(w: Int, h: Int) {
        screenW = max(1, w)
        screenH = max(1, h)
        // Kisa kenar 360 sanal piksele en yakin tam sayi olcek
        scale = max(1, (minOf(screenW, screenH) / 360f).roundToInt())
        vw = ceil(screenW / scale.toFloat()).toInt()
        vh = ceil(screenH / scale.toFloat()).toInt()
        scene?.dispose(); light?.dispose(); overlay?.dispose()
        scene = make(vw, vh)
        light = make(vw, vh)
        overlay = make(vw, vh)
        camera.setToOrtho(false, vw.toFloat(), vh.toFloat())
        screenMatrix.setToOrtho2D(0f, 0f, screenW.toFloat(), screenH.toFloat())
    }

    private fun make(w: Int, h: Int): FrameBuffer {
        val fb = try {
            FrameBuffer(Pixmap.Format.RGBA8888, w, h, false)
        } catch (e: Exception) {
            FrameBuffer(Pixmap.Format.RGBA4444, w, h, false)
        }
        fb.colorBufferTexture.setFilter(Texture.TextureFilter.Nearest, Texture.TextureFilter.Nearest)
        return fb
    }

    fun beginScene() {
        scene!!.begin()
        Gdx.gl.glClearColor(0f, 0f, 0f, 1f)
        Gdx.gl.glClear(GL20.GL_COLOR_BUFFER_BIT)
    }

    fun endScene() = scene!!.end()

    fun beginLight(ambient: Color) {
        light!!.begin()
        Gdx.gl.glClearColor(ambient.r, ambient.g, ambient.b, 1f)
        Gdx.gl.glClear(GL20.GL_COLOR_BUFFER_BIT)
    }

    fun endLight() = light!!.end()

    fun beginOverlay() {
        overlay!!.begin()
        Gdx.gl.glClearColor(0f, 0f, 0f, 0f)
        Gdx.gl.glClear(GL20.GL_COLOR_BUFFER_BIT)
    }

    fun endOverlay() = overlay!!.end()

    /** Sahneyi isikla birlestirip ekrana, ardindan overlay'i ustune cizer. */
    fun present(batch: SpriteBatch) {
        Gdx.gl.glViewport(0, 0, screenW, screenH)
        Gdx.gl.glClearColor(0f, 0f, 0f, 1f)
        Gdx.gl.glClear(GL20.GL_COLOR_BUFFER_BIT)
        val w = (vw * scale).toFloat()
        val h = (vh * scale).toFloat()
        val x = ((screenW - w) / 2f).toInt().toFloat()
        val y = ((screenH - h) / 2f).toInt().toFloat()
        batch.projectionMatrix = screenMatrix
        batch.shader = composite
        batch.begin()
        light!!.colorBufferTexture.bind(1)
        composite.setUniformi("u_light", 1)
        composite.setUniformf("u_virtual", vw.toFloat(), vh.toFloat())
        composite.setUniformf("u_steps", lightSteps)
        composite.setUniformf("u_dither", ditherAmount)
        composite.setUniformf("u_exposure", exposure)
        Gdx.gl.glActiveTexture(GL20.GL_TEXTURE0)
        batch.disableBlending()
        batch.draw(scene!!.colorBufferTexture, x, y, w, h, 0f, 0f, 1f, 1f)
        batch.end()
        batch.shader = null
        batch.enableBlending()
        batch.begin()
        batch.setColor(1f, 1f, 1f, 1f)
        batch.draw(overlay!!.colorBufferTexture, x, y, w, h, 0f, 0f, 1f, 1f)
        batch.end()
    }

    override fun dispose() {
        scene?.dispose(); light?.dispose(); overlay?.dispose()
        composite.dispose()
    }

    companion object {
        private const val VERT = """
attribute vec4 a_position;
attribute vec4 a_color;
attribute vec2 a_texCoord0;
uniform mat4 u_projTrans;
varying vec4 v_color;
varying vec2 v_texCoords;
void main() {
    v_color = a_color;
    v_texCoords = a_texCoord0;
    gl_Position = u_projTrans * a_position;
}
"""

        private const val FRAG = """
#ifdef GL_ES
precision mediump float;
#endif
varying vec4 v_color;
varying vec2 v_texCoords;
uniform sampler2D u_texture;
uniform sampler2D u_light;
uniform vec2 u_virtual;
uniform float u_steps;
uniform float u_dither;
uniform float u_exposure;

float bayer2(vec2 a) {
    a = floor(a);
    return fract(a.x / 2.0 + a.y * a.y * 0.75);
}
float bayer4(vec2 a) {
    return bayer2(0.5 * a) * 0.25 + bayer2(a);
}

void main() {
    vec3 scene = texture2D(u_texture, v_texCoords).rgb;
    vec3 light = texture2D(u_light, v_texCoords).rgb;
    vec2 p = floor(v_texCoords * u_virtual);
    float b = (bayer4(p) - 0.5) * u_dither;
    // Kademeler parlaklikta (karekok) esit: karanlik tarafta daha ince adim
    vec3 g = sqrt(clamp(light, 0.0, 1.0));
    vec3 q = floor(g * u_steps + 0.5 + b) / u_steps;
    q = q * q;
    gl_FragColor = vec4(min(scene * q * u_exposure, vec3(1.0)), 1.0);
}
"""
    }
}
