package com.lastlantern.ui

import com.badlogic.gdx.Gdx
import com.badlogic.gdx.graphics.Color
import com.badlogic.gdx.graphics.g2d.Batch
import com.badlogic.gdx.graphics.g2d.GlyphLayout
import com.badlogic.gdx.scenes.scene2d.Actor
import com.badlogic.gdx.scenes.scene2d.InputEvent
import com.badlogic.gdx.scenes.scene2d.InputListener
import com.badlogic.gdx.scenes.scene2d.Touchable
import com.lastlantern.LastLanternGame
import com.lastlantern.assets.I18n
import com.lastlantern.content.Content
import com.lastlantern.sim.World
import kotlin.math.max
import kotlin.math.min
import kotlin.math.sin
import kotlin.math.sqrt

/**
 * Oyun ici gosterge: XP cubugu, sure, oldurme, altin, silah/pasif yuvalari,
 * boss cubugu, duyurular ve egitim ipuclari. Tek bir Actor olarak cizilir
 * (onlarca Label yerine): her kare yeniden duzen hesabi yapilmaz.
 */
class Hud(private val game: LastLanternGame, private val world: World) : Actor() {
    private val a = game.assets
    private val font = a.fontOutline
    private val layout = GlyphLayout()
    private var time = 0f

    private var banner: String? = null
    private var bannerColor = Color.WHITE
    private var bannerT = 0f
    private var bannerDur = 0f

    var hint: String? = null
    private var hintAlpha = 0f
    var bossName: String? = null
    private var shownXp = 0f
    private val sb = StringBuilder(32)

    init {
        touchable = Touchable.disabled
    }

    fun announce(text: String, color: Color = Color.WHITE, duration: Float = 2.6f) {
        banner = text
        bannerColor = color
        bannerT = 0f
        bannerDur = duration
    }

    override fun act(delta: Float) {
        super.act(delta)
        time += delta
        if (banner != null) {
            bannerT += delta
            if (bannerT > bannerDur) banner = null
        }
        hintAlpha = if (hint != null) min(1f, hintAlpha + delta * 3f) else max(0f, hintAlpha - delta * 3f)
        val target = world.player.xp / world.player.xpToNext
        shownXp = if (target < shownXp) target else shownXp + (target - shownXp) * min(1f, delta * 10f)
    }

    private fun insetTop() = Gdx.graphics.safeInsetTop / game.uiScale.toFloat()
    private fun insetBottom() = Gdx.graphics.safeInsetBottom / game.uiScale.toFloat()

    override fun draw(batch: Batch, parentAlpha: Float) {
        val w = stage.width
        val h = stage.height
        val top = h - insetTop()
        val px = a.pixel
        lowHpVignette(batch, w, h)

        // ---- XP cubugu
        val barY = top - 9f
        batch.setColor(0.07f, 0.05f, 0.11f, 0.92f)
        batch.draw(px, 3f, barY - 1f, w - 6f, 8f)
        batch.setColor(0.25f, 0.16f, 0.32f, 1f)
        batch.draw(px, 4f, barY, w - 8f, 6f)
        val fill = ((w - 8f) * shownXp.coerceIn(0f, 1f)).toInt().toFloat()
        batch.setColor(1f, 0.55f, 0.18f, 1f)
        batch.draw(px, 4f, barY, fill, 6f)
        batch.setColor(1f, 0.85f, 0.45f, 1f)
        batch.draw(px, 4f, barY + 4f, fill, 2f)
        batch.setColor(1f, 1f, 1f, 1f)
        sb.setLength(0)
        sb.append(game.i18n.f("hud.level", world.player.level))
        text(batch, sb, w / 2f, barY + 9f, Color.WHITE, center = true)

        // ---- Sure (ortada, buyuk)
        val rowY = barY - 4f
        val remaining = if (world.config.endless) world.time else max(0f, Content.RUN_LENGTH - world.time)
        sb.setLength(0)
        sb.append(I18n.time(remaining))
        font.data.setScale(2f)
        val timeColor = if (!world.config.endless && remaining < 60f) UiKit.GOLD else Color.WHITE
        text(batch, sb, w / 2f, rowY, timeColor, center = true)
        font.data.setScale(1f)

        // ---- Oldurme (sol) ve altin (sag)
        val skull = a.region("icon_ui_skull")
        batch.draw(skull, 6f, rowY - 9f)
        sb.setLength(0)
        sb.append(world.kills)
        text(batch, sb, 16f, rowY - 1f, Color.WHITE)
        val coin = a.region("pick_coin")
        sb.setLength(0)
        sb.append(world.gold)
        layout.setText(font, sb)
        val gx = w - 30f - layout.width
        batch.draw(coin, gx - 10f, rowY - 9f)
        text(batch, sb, gx, rowY - 1f, UiKit.GOLD)

        // ---- Silah ve pasif yuvalari
        var sx = 5f
        val sy = rowY - 30f
        for (wp in world.weapons) {
            slot(batch, sx, sy, wp.def.icon, if (wp.evolved) -1 else wp.level, wp.def.maxLevel, wp.evolved)
            sx += 19f
        }
        sx = 5f
        for ((id, lvl) in world.passives) {
            slot(batch, sx, sy - 19f, Content.passives.getValue(id).icon, lvl, 5, false)
            sx += 19f
        }

        // ---- Boss cubugu (altta)
        val bi = world.bossSlot
        if (bi >= 0 && world.enemies.active[bi]) {
            val e = world.enemies
            if (bossName == null) bossName = e.def[bi]?.let { game.i18n["hud.boss_name.${it.id}"] }
            val frac = (e.hp[bi] / e.maxHp[bi]).coerceIn(0f, 1f)
            val bw = min(w - 30f, 240f)
            val bx = (w - bw) / 2f
            val by = insetBottom() + 14f
            batch.setColor(0.07f, 0.05f, 0.11f, 0.92f)
            batch.draw(px, bx - 2f, by - 2f, bw + 4f, 9f)
            batch.setColor(0.35f, 0.08f, 0.12f, 1f)
            batch.draw(px, bx, by, bw, 5f)
            batch.setColor(0.95f, 0.25f, 0.25f, 1f)
            batch.draw(px, bx, by, (bw * frac).toInt().toFloat(), 5f)
            batch.setColor(1f, 0.6f, 0.55f, 1f)
            batch.draw(px, bx, by + 3f, (bw * frac).toInt().toFloat(), 2f)
            batch.setColor(1f, 1f, 1f, 1f)
            bossName?.let { text(batch, it, w / 2f, by + 17f, UiKit.RED, center = true) }
        }

        // ---- Ekran disi gostergeler: sandiklar (altin) ve boss (kirmizi)
        offscreen(batch, w, h)

        // ---- Duyuru
        banner?.let {
            val t = bannerT
            val alpha = min(1f, t * 4f) * min(1f, (bannerDur - t) * 2f)
            font.data.setScale(2f)
            val c = Color(bannerColor).also { col -> col.a = alpha }
            text(batch, it, w / 2f, h * 0.66f, c, center = true, wrapWidth = w - 20f)
            font.data.setScale(1f)
        }

        // ---- Egitim ipucu
        val hintText = hint
        if (hintAlpha > 0f && hintText != null) {
            layout.setText(font, hintText)
            val pw = layout.width + 16f
            val py = h * 0.26f
            batch.setColor(0.05f, 0.03f, 0.09f, 0.75f * hintAlpha)
            batch.draw(px, (w - pw) / 2f, py - 13f, pw, 17f)
            val bob = sin(time * 3f) * 1f
            text(batch, hintText, w / 2f, py + bob, Color(1f, 0.92f, 0.75f, hintAlpha), center = true)
        }
        batch.setColor(1f, 1f, 1f, 1f)
    }

    /** Gorus alaninin disindaki sandik/boss icin ekran kenarinda yanip sonen isaret. */
    private fun offscreen(batch: Batch, w: Float, h: Float) {
        val p = world.player
        val hw = world.viewHalfW
        val hh = world.viewHalfH
        val pk = world.pickups
        val chest = a.region("pick_chest")
        val pulse = 0.6f + 0.4f * sin(time * 6f)
        for (i in 0 until pk.high) {
            if (!pk.active[i] || pk.kind[i] != com.lastlantern.sim.Pick.CHEST) continue
            marker(batch, pk.x[i] - p.x, pk.y[i] - p.y, hw, hh, w, h) { x, y ->
                batch.setColor(1f, 1f, 1f, pulse)
                batch.draw(chest, x - chest.regionWidth / 2f, y - chest.regionHeight / 2f)
            }
        }
        val bi = world.bossSlot
        if (bi >= 0 && world.enemies.active[bi]) {
            val e = world.enemies
            marker(batch, e.x[bi] - p.x, e.y[bi] - p.y, hw, hh, w, h) { x, y ->
                batch.setColor(1f, 0.3f, 0.3f, pulse)
                batch.draw(a.region("icon_ui_skull"), x - 3.5f, y - 3.5f)
            }
        }
        batch.setColor(1f, 1f, 1f, 1f)
    }

    private inline fun marker(batch: Batch, dx: Float, dy: Float, hw: Float, hh: Float, w: Float, h: Float,
                              draw: (Float, Float) -> Unit) {
        if (kotlin.math.abs(dx) < hw - 6f && kotlin.math.abs(dy) < hh - 6f) return
        // Dunya -> arayuz koordinati, kenar boslugu icinde sikistir
        val k = minOf((hw - 6f) / kotlin.math.abs(dx).coerceAtLeast(0.01f), (hh - 6f) / kotlin.math.abs(dy).coerceAtLeast(0.01f))
        val ux = w / 2f + dx * k / hw * (w / 2f)
        val top = h - insetTop() - 70f
        val bottom = insetBottom() + 28f
        val uy = (h / 2f + dy * k / hh * (h / 2f)).coerceIn(bottom, top)
        draw(ux.coerceIn(12f, w - 12f), uy)
    }

    private fun slot(batch: Batch, x: Float, y: Float, icon: String, level: Int, max: Int, evolved: Boolean) {
        val px = a.pixel
        batch.setColor(0.07f, 0.05f, 0.11f, 0.85f)
        batch.draw(px, x, y, 18f, 18f)
        if (evolved) batch.setColor(1f, 0.8f, 0.3f, 1f) else batch.setColor(0.35f, 0.28f, 0.48f, 1f)
        batch.draw(px, x, y, 18f, 1f)
        batch.draw(px, x, y + 17f, 18f, 1f)
        batch.draw(px, x, y, 1f, 18f)
        batch.draw(px, x + 17f, y, 1f, 18f)
        batch.setColor(1f, 1f, 1f, 1f)
        batch.draw(a.region(icon), x + 1f, y + 1f)
        if (level > 0) {
            // Seviye noktalari
            val n = level
            for (k in 0 until n) {
                if (level >= max) batch.setColor(1f, 0.8f, 0.3f, 1f) else batch.setColor(1f, 1f, 1f, 0.9f)
                batch.draw(px, x + 2f + k * 2.5f, y - 3f, 2f, 2f)
            }
            batch.setColor(1f, 1f, 1f, 1f)
        }
    }

    private fun lowHpVignette(batch: Batch, w: Float, h: Float) {
        val frac = world.player.hp / world.stats.maxHp
        if (frac > 0.3f || world.player.hp <= 0f) return
        val pulse = (0.3f - frac) / 0.3f * (0.35f + 0.15f * sin(time * 6f))
        val px = a.pixel
        val edge = 14f
        for (k in 0 until 7) {
            val al = pulse * (1f - k / 7f)
            batch.setColor(0.8f, 0.05f, 0.08f, al)
            val o = k * (edge / 7f)
            batch.draw(px, 0f, o, w, 2f)
            batch.draw(px, 0f, h - o - 2f, w, 2f)
            batch.draw(px, o, 0f, 2f, h)
            batch.draw(px, w - o - 2f, 0f, 2f, h)
        }
        batch.setColor(1f, 1f, 1f, 1f)
    }

    private fun text(batch: Batch, s: CharSequence, x: Float, y: Float, color: Color, center: Boolean = false,
                     wrapWidth: Float = 0f) {
        font.color = color
        if (wrapWidth > 0f) {
            layout.setText(font, s, color, wrapWidth, com.badlogic.gdx.utils.Align.center, true)
            font.draw(batch, layout, (x - wrapWidth / 2f).toInt().toFloat(), y.toInt().toFloat())
        } else {
            layout.setText(font, s)
            val dx = if (center) layout.width / 2f else 0f
            font.draw(batch, layout, (x - dx).toInt().toFloat(), y.toInt().toFloat())
        }
        font.color = Color.WHITE
    }
}

/**
 * Ekranin bos herhangi bir yerine dokununca beliren joystick. Dugmeler
 * sahnede ustte oldugu icin once onlar dokunusu alir.
 */
class Joystick(private val game: LastLanternGame) : Actor() {
    var active = false
        private set
    private var ox = 0f
    private var oy = 0f
    private var kx = 0f
    private var ky = 0f
    private var pointer = -1
    var outX = 0f
        private set
    var outY = 0f
        private set
    var enabledInput = true
    var movedDistance = 0f
        private set

    private val maxR = 20f
    private val dead = 2f

    init {
        touchable = Touchable.enabled
        addListener(object : InputListener() {
            override fun touchDown(event: InputEvent, x: Float, y: Float, p: Int, button: Int): Boolean {
                if (!enabledInput || pointer >= 0) return false
                pointer = p
                active = true
                if (game.settings.fixedJoystick) {
                    ox = width / 2f
                    oy = 54f + Gdx.graphics.safeInsetBottom / game.uiScale.toFloat()
                } else {
                    ox = x
                    oy = y
                }
                drag(x, y)
                return true
            }

            override fun touchDragged(event: InputEvent, x: Float, y: Float, p: Int) {
                if (p == pointer) drag(x, y)
            }

            override fun touchUp(event: InputEvent, x: Float, y: Float, p: Int, button: Int) {
                if (p == pointer) release()
            }
        })
    }

    private fun drag(x: Float, y: Float) {
        var dx = x - ox
        var dy = y - oy
        val d = sqrt(dx * dx + dy * dy)
        if (d > maxR) {
            // Taban parmagi takip eder: uzun surukleme sonrasi ters yone donmek kolay olsun
            if (!game.settings.fixedJoystick) {
                ox += dx / d * (d - maxR)
                oy += dy / d * (d - maxR)
            }
            dx = dx / d * maxR
            dy = dy / d * maxR
        }
        kx = dx
        ky = dy
        val mag = ((min(d, maxR) - dead) / (maxR - dead)).coerceIn(0f, 1f)
        if (d > 0.001f) {
            outX = dx / sqrt(dx * dx + dy * dy).coerceAtLeast(0.001f) * mag
            outY = dy / sqrt(dx * dx + dy * dy).coerceAtLeast(0.001f) * mag
        }
        movedDistance += mag
    }

    fun release() {
        pointer = -1
        active = false
        outX = 0f
        outY = 0f
        kx = 0f
        ky = 0f
    }

    override fun draw(batch: Batch, parentAlpha: Float) {
        val a = game.assets
        val fixed = game.settings.fixedJoystick
        if (!active && !fixed) return
        val cx = if (active) ox else width / 2f
        val cy = if (active) oy else 54f + Gdx.graphics.safeInsetBottom / game.uiScale.toFloat()
        val ring = a.region("ring48")
        batch.setColor(1f, 0.9f, 0.75f, if (active) 0.35f else 0.18f)
        batch.draw(ring, cx - 24f, cy - 24f)
        val knob = a.region("circle16")
        batch.setColor(1f, 0.85f, 0.6f, if (active) 0.55f else 0.25f)
        batch.draw(knob, (cx + kx - 8f).toInt().toFloat(), (cy + ky - 8f).toInt().toFloat())
        batch.setColor(1f, 1f, 1f, 1f)
    }
}
