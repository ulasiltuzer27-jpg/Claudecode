package com.lastlantern.render

import com.badlogic.gdx.graphics.Color
import com.badlogic.gdx.graphics.g2d.BitmapFont
import com.badlogic.gdx.graphics.g2d.SpriteBatch
import com.badlogic.gdx.graphics.g2d.TextureRegion
import com.lastlantern.sim.Rng
import com.lastlantern.sim.SlotPool
import kotlin.math.cos
import kotlin.math.sin

/**
 * Basit parcacik sistemi. Iki katman: "lit" parcaciklar (duman, toz) sahneyle
 * birlikte isiktan etkilenir; "glow" parcaciklar (kivilcim, koz) karanlikta da
 * parlar ve overlay'e cizilir.
 */
class Particles(cap: Int) : SlotPool(cap) {
    val x = FloatArray(cap)
    val y = FloatArray(cap)
    val vx = FloatArray(cap)
    val vy = FloatArray(cap)
    val life = FloatArray(cap)
    val maxLife = FloatArray(cap)
    val size = FloatArray(cap)
    val color = IntArray(cap)
    val gravity = FloatArray(cap)
    val drag = FloatArray(cap)
    val glow = BooleanArray(cap)
    private val rng = Rng(77)
    private val tmp = Color()

    fun emit(px: Float, py: Float, n: Int, rgba: Int, speed: Float, life0: Float, size0: Float,
             glowing: Boolean = true, grav: Float = 0f, spread: Float = Rng.TAU, dir: Float = 0f) {
        repeat(n) {
            val i = alloc()
            if (i < 0) return
            val a = dir + (rng.nextFloat() - 0.5f) * spread
            val s = speed * (0.4f + rng.nextFloat() * 0.8f)
            x[i] = px; y[i] = py
            vx[i] = cos(a) * s; vy[i] = sin(a) * s
            life[i] = life0 * (0.6f + rng.nextFloat() * 0.6f); maxLife[i] = life[i]
            size[i] = size0
            color[i] = rgba
            gravity[i] = grav
            drag[i] = 3f
            glow[i] = glowing
        }
    }

    fun update(dt: Float) {
        for (i in 0 until high) {
            if (!active[i]) continue
            life[i] -= dt
            if (life[i] <= 0f) {
                release(i)
                continue
            }
            val k = 1f - (drag[i] * dt).coerceAtMost(0.9f)
            vx[i] *= k
            vy[i] = vy[i] * k - gravity[i] * dt
            x[i] += vx[i] * dt
            y[i] += vy[i] * dt
        }
    }

    fun draw(batch: SpriteBatch, px: TextureRegion, glowLayer: Boolean) {
        for (i in 0 until high) {
            if (!active[i] || glow[i] != glowLayer) continue
            val t = life[i] / maxLife[i]
            Color.rgba8888ToColor(tmp, color[i])
            tmp.a *= t.coerceAtMost(1f)
            batch.color = tmp
            val s = (size[i] * (0.5f + 0.5f * t)).coerceAtLeast(1f)
            batch.draw(px, (x[i] - s / 2f).toInt().toFloat(), (y[i] - s / 2f).toInt().toFloat(), s.toInt().toFloat(), s.toInt().toFloat())
        }
        batch.setColor(1f, 1f, 1f, 1f)
    }
}

/** Yukari suzulen hasar sayilari (overlay katmaninda). */
class DamageNumbers(cap: Int) : SlotPool(cap) {
    val x = FloatArray(cap)
    val y = FloatArray(cap)
    val value = IntArray(cap)
    val life = FloatArray(cap)
    val crit = BooleanArray(cap)
    val color = IntArray(cap)
    private val tmp = Color()
    private val text = Array(cap) { StringBuilder(8) }
    private var seq = 0

    fun add(px: Float, py: Float, v: Float, isCrit: Boolean, rgba: Int = 0xFFFFFFFF.toInt()) {
        // Ekranda cok fazla sayi okunmaz; en eskisinin yerini al
        var i = alloc()
        if (i < 0) {
            var oldest = 0
            for (k in 0 until high) if (life[k] < life[oldest]) oldest = k
            i = oldest
        }
        x[i] = px + (seq++ % 5 - 2) * 2
        y[i] = py
        value[i] = v.toInt().coerceAtLeast(1)
        life[i] = 0.7f
        crit[i] = isCrit
        color[i] = rgba
        text[i].setLength(0)
        text[i].append(value[i])
    }

    fun update(dt: Float) {
        for (i in 0 until high) {
            if (!active[i]) continue
            life[i] -= dt
            y[i] += dt * 22f
            if (life[i] <= 0f) release(i)
        }
    }

    fun draw(batch: SpriteBatch, font: BitmapFont) {
        for (i in 0 until high) {
            if (!active[i]) continue
            Color.rgba8888ToColor(tmp, if (crit[i]) 0xFFD45AFF.toInt() else color[i])
            tmp.a = (life[i] / 0.25f).coerceAtMost(1f)
            font.color = tmp
            font.draw(batch, text[i], (x[i] - text[i].length * 2.5f).toInt().toFloat(), y[i].toInt().toFloat())
        }
        font.color = Color.WHITE
    }
}

/** Ekran sarsintisi: travma modeli (karesi alinan siddet, hizla soner). */
class Shake {
    private var trauma = 0f
    private var t = 0f
    var offX = 0
        private set
    var offY = 0
        private set

    fun add(amount: Float) {
        trauma = (trauma + amount / 10f).coerceAtMost(1f)
    }

    fun update(dt: Float, enabled: Boolean) {
        t += dt
        trauma = (trauma - dt * 1.6f).coerceAtLeast(0f)
        if (!enabled || trauma <= 0f) {
            offX = 0; offY = 0
            return
        }
        val m = trauma * trauma * 6f
        offX = (sin(t * 71f) * m).toInt()
        offY = (cos(t * 59f) * m).toInt()
    }
}
