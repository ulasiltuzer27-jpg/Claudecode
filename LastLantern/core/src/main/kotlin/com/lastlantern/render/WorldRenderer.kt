package com.lastlantern.render

import com.badlogic.gdx.graphics.Color
import com.badlogic.gdx.graphics.GL20
import com.badlogic.gdx.graphics.g2d.SpriteBatch
import com.badlogic.gdx.graphics.g2d.TextureRegion
import com.badlogic.gdx.utils.Array as GdxArray
import com.lastlantern.assets.Assets
import com.lastlantern.content.Tier
import com.lastlantern.save.Settings
import com.lastlantern.sim.AKind
import com.lastlantern.sim.EState
import com.lastlantern.sim.Ev
import com.lastlantern.sim.PKind
import com.lastlantern.sim.Pick
import com.lastlantern.sim.RingWeapon
import com.lastlantern.sim.Rng
import com.lastlantern.sim.World
import kotlin.math.atan2
import kotlin.math.cos
import kotlin.math.min
import kotlin.math.sin
import kotlin.math.sqrt

/**
 * Bir run'i cizer: zemin + dekor + varliklar (sahne), isiklar (isik haritasi),
 * gozler + parlayan parcaciklar + hasar sayilari (overlay).
 */
class WorldRenderer(private val a: Assets, private val world: World, private val settings: () -> Settings,
                    private val supporter: Boolean) {
    val particles = Particles(1600)
    val numbers = DamageNumbers(80)
    val shake = Shake()
    private val flashes = Flashes(48)
    var time = 0f
        private set

    private val stage = world.stage
    private val night = Color(stage.ambient)
    private val dawn = Color(stage.dawnAmbient)
    private val ambient = Color()
    private val enemyTint = Color(stage.enemyTint)
    private val tmp = Color()

    private val tiles = Array(4) { a.region("${stage.tileset}_$it") }
    private val decals: List<GdxArray<com.badlogic.gdx.graphics.g2d.TextureAtlas.AtlasRegion>> = stage.decals.map { a.frames(it) }
    private val props: List<GdxArray<com.badlogic.gdx.graphics.g2d.TextureAtlas.AtlasRegion>> = stage.props.map { a.frames(it) }
    private val px = a.pixel
    private val shadow = a.region("shadow")
    private val shadowBig = a.region("shadow_big")
    private val circle32 = a.region("circle32")
    private val glow8 = a.region("fx_glow8")
    private val glow16 = a.region("fx_glow16")

    private var sortKeys = LongArray(1100)
    private var camX = 0f
    private var camY = 0f
    private var vw = 360
    private var vh = 640

    // ------------------------------------------------------------------ olaylar
    fun onEvent(type: Int, x: Float, y: Float, av: Float, bv: Int) {
        when (type) {
            Ev.HIT -> {
                if (settings().damageNumbers) numbers.add(x, y + 2f, av, bv == 1)
                particles.emit(x, y, 2, 0xFFF6CFFF.toInt(), 40f, 0.18f, 1f)
            }
            Ev.KILL -> {
                val d = world.enemyDefs[bv]
                val elite = av > 0f
                particles.emit(x, y, if (elite) 18 else 6, 0x6B5B85E0, 30f, 0.45f, 2f, glowing = false, grav = -8f)
                particles.emit(x, y, if (elite) 14 else 3, 0xFFA040FF.toInt(), 55f, 0.4f, 1f, grav = 20f)
                if (d.tier != Tier.NORMAL || elite) {
                    shake.add(if (d.tier == Tier.BOSS) 8f else 4f)
                    flashes.add(x, y, 90f, 0.5f, 0xFFC070FF.toInt())
                }
            }
            Ev.PLAYER_HURT -> {
                particles.emit(x, y, 6, 0xFF4A4AFF.toInt(), 50f, 0.3f, 1f)
                if (settings().damageNumbers) numbers.add(x, y + 10f, av, false, 0xFF6A6AFF.toInt())
            }
            Ev.HEAL -> {
                particles.emit(x, y, 8, 0x8CFF8CFF.toInt(), 30f, 0.6f, 1f, grav = -30f)
                if (settings().damageNumbers) numbers.add(x, y + 12f, av, false, 0x8CFF8CFF.toInt())
            }
            Ev.PICK -> when (bv) {
                Pick.COIN -> particles.emit(x, y, 3, 0xFFE66AFF.toInt(), 30f, 0.3f, 1f)
                Pick.EMBER_S, Pick.EMBER_M, Pick.EMBER_L -> particles.emit(x, y, 2, 0xFFB347FF.toInt(), 25f, 0.25f, 1f)
                Pick.MAGNET, Pick.OIL -> flashes.add(x, y, 120f, 0.5f, 0xFFE0A0FF.toInt())
                else -> Unit
            }
            Ev.LEVEL_UP -> {
                flashes.add(x, y, 160f, 0.6f, 0xFFE6A0FF.toInt())
                particles.emit(x, y, 24, 0xFFD45AFF.toInt(), 110f, 0.6f, 2f)
            }
            Ev.SHAKE -> shake.add(av)
            Ev.FLARE -> {
                flashes.add(x, y, av * 1.4f, 0.8f, 0xFFFFE0FF.toInt())
                particles.emit(x, y, 40, 0xFFF6CFFF.toInt(), 220f, 0.6f, 2f)
            }
            Ev.REVIVE -> {
                flashes.add(x, y, 220f, 1f, 0xFFF6CFFF.toInt())
                particles.emit(x, y, 40, 0xFFD45AFF.toInt(), 160f, 0.8f, 2f)
            }
            Ev.EVOLVE -> {
                flashes.add(x, y, 220f, 1f, 0xFFD45AFF.toInt())
                particles.emit(x, y, 50, 0xFFF6CFFF.toInt(), 180f, 0.9f, 2f)
            }
            Ev.DAWN -> flashes.add(x, y, 600f, 2.5f, 0xFFE6B0FF.toInt())
            Ev.SPLASH -> particles.emit(x, y, 16, 0xB98CFFFF.toInt(), av * 2f, 0.4f, 1f)
            Ev.BOSS_SPAWN, Ev.MINIBOSS -> {
                shake.add(6f)
                particles.emit(x, y, 30, 0x6B5B85E0, 60f, 0.8f, 2f, glowing = false)
            }
            Ev.BOSS_DEATH -> {
                flashes.add(x, y, 300f, 1.5f, 0xFFE6B0FF.toInt())
                particles.emit(x, y, 80, 0xFFD45AFF.toInt(), 200f, 1.2f, 2f)
            }
        }
    }

    fun update(dt: Float) {
        time += dt
        particles.update(dt)
        numbers.update(dt)
        flashes.update(dt)
        shake.update(dt, settings().screenShake)
        // Kivilcim: fener alevinden yukselen ince parcaciklar
        if (world.state == com.lastlantern.sim.RunState.PLAYING && (time * 10f).toInt() != ((time - dt) * 10f).toInt()) {
            val p = world.player
            val lx = p.x + if (p.flipX) -5f else 5f
            particles.emit(lx, p.y - 2f, 1, if (supporter) 0xFFE680FF.toInt() else 0xFFB347FF.toInt(), 8f, 0.7f, 1f,
                grav = -14f, spread = 1.2f, dir = (Math.PI / 2).toFloat())
        }
    }

    // ------------------------------------------------------------------ ana cizim
    /** [lift]: kamerayi yukari kaydirir (oyuncu ekranin altinda gorunur; menu arka plani icin). */
    fun render(batch: SpriteBatch, view: PixelView, lift: Float = 0f) {
        vw = view.vw
        vh = view.vh
        val p = world.player
        camX = (p.x + shake.offX).toInt().toFloat()
        camY = (p.y + shake.offY + vh * lift).toInt().toFloat()
        view.camera.position.set(camX, camY, 0f)
        view.camera.update()
        batch.projectionMatrix = view.camera.combined

        // 1) Sahne
        view.beginScene()
        batch.begin()
        batch.setColor(1f, 1f, 1f, 1f)
        drawGround(batch)
        drawDecor(batch)
        drawAreas(batch)
        drawPickups(batch)
        drawEntities(batch)
        drawShots(batch)
        drawBellRings(batch)
        drawBeams(batch)
        drawEnemyShots(batch)
        particles.draw(batch, px, glowLayer = false)
        batch.end()
        view.endScene()

        // 2) Isik haritasi
        val df = world.dawnFactor
        ambient.set(night).lerp(dawn, df)
        view.beginLight(ambient)
        batch.begin()
        batch.setBlendFunction(GL20.GL_SRC_ALPHA, GL20.GL_ONE)
        drawLights(batch)
        batch.end()
        batch.setBlendFunction(GL20.GL_SRC_ALPHA, GL20.GL_ONE_MINUS_SRC_ALPHA)
        view.endLight()

        // 3) Overlay (karanlikta da gorunen)
        view.beginOverlay()
        batch.begin()
        drawEyes(batch)
        drawTelegraphs(batch)
        particles.draw(batch, px, glowLayer = true)
        drawPlayerBar(batch)
        if (settings().damageNumbers) numbers.draw(batch, a.fontOutline)
        batch.end()
        view.endOverlay()

        view.present(batch)
    }

    private fun visible(x: Float, y: Float, margin: Float): Boolean =
        x > camX - vw / 2f - margin && x < camX + vw / 2f + margin &&
            y > camY - vh / 2f - margin && y < camY + vh / 2f + margin

    // ------------------------------------------------------------------ zemin
    private fun hash(x: Int, y: Int, s: Int = 0): Int {
        var h = x * 374761393 + y * 668265263 + s * 982451653
        h = (h xor (h ushr 13)) * 1274126177
        return h xor (h ushr 16)
    }

    private fun drawGround(batch: SpriteBatch) {
        val x0 = Math.floorDiv((camX - vw / 2f).toInt(), 16) - 1
        val y0 = Math.floorDiv((camY - vh / 2f).toInt(), 16) - 1
        val x1 = x0 + vw / 16 + 3
        val y1 = y0 + vh / 16 + 3
        for (ty in y0..y1) for (tx in x0..x1) {
            val region = hash(Math.floorDiv(tx, 5), Math.floorDiv(ty, 5), 1) and 1
            val r = hash(tx, ty) and 0xFF
            val v = when {
                r < 18 -> 2 + (1 - region)
                r < 40 -> 1 + region * 2
                else -> region * 2
            }.coerceIn(0, 3)
            batch.draw(tiles[v], (tx * 16).toFloat(), (ty * 16).toFloat())
        }
    }

    private fun drawDecor(batch: SpriteBatch) {
        val cs = 128
        val x0 = Math.floorDiv((camX - vw / 2f - 40).toInt(), cs)
        val y0 = Math.floorDiv((camY - vh / 2f - 40).toInt(), cs)
        val x1 = Math.floorDiv((camX + vw / 2f + 40).toInt(), cs)
        val y1 = Math.floorDiv((camY + vh / 2f + 40).toInt(), cs)
        // Once susler (dusuk), sonra dekorlar
        for (pass in 0..1) for (cy in y1 downTo y0) for (cx in x0..x1) {
            dh = hash(cx, cy, 7 + world.config.seed.toInt())
            if (pass == 0) {
                val n = 3 + dnext() % 4
                repeat(n) {
                    // Ilk sus en sik (cimen tutamlari), sonuncusu en nadir
                    val r = dnext() % 100
                    val idx = when {
                        r < 40 -> 0
                        r < 85 -> 1 + (r % (decals.size - 2).coerceAtLeast(1))
                        else -> decals.size - 1
                    }.coerceIn(0, decals.size - 1)
                    val list = decals[idx]
                    val reg = list[dnext() % list.size]
                    batch.draw(reg, (cx * cs + dnext() % cs).toFloat(), (cy * cs + dnext() % cs).toFloat())
                }
            } else {
                // Baslangic noktasi bos kalsin: ilk saniyede karakter bir agacin altinda durmasin
                if ((cx == 0 || cx == -1) && (cy == 0 || cy == -1)) continue
                repeat(20) { dnext() }
                val count = when (dnext() % 10) {
                    0, 1, 2 -> 0
                    3, 4, 5, 6 -> 1
                    else -> 2
                }
                repeat(count) {
                    val list = props[dnext() % props.size]
                    val reg = list[dnext() % list.size]
                    val x = (cx * cs + dnext() % (cs - 24)).toFloat()
                    val y = (cy * cs + dnext() % (cs - 24)).toFloat()
                    batch.draw(reg, x, y)
                }
            }
        }
    }

    private var dh = 0

    private fun dnext(): Int {
        dh = hash(dh, 0x5bd1e995)
        return dh and 0x7FFFFFFF
    }

    // ------------------------------------------------------------------ zemin etkileri
    private fun drawAreas(batch: SpriteBatch) {
        val ar = world.areas
        for (i in 0 until ar.high) {
            if (!ar.active[i] || ar.kind[i] != AKind.FLAME) continue
            if (!visible(ar.x[i], ar.y[i], 20f)) continue
            val fade = (ar.life[i] / 0.4f).coerceAtMost(1f)
            val name = if (ar.evolved[i]) "proj_blueflame" else "proj_flame"
            val r = ar.radius[i]
            val n = if (r > 12f) 3 else 2
            for (k in 0 until n) {
                val ang = (i * 2.3f + k * 2.1f)
                val ox = cos(ang) * r * 0.45f
                val oy = sin(ang) * r * 0.3f
                val reg = a.frame(name, time + k * 0.13f + i * 0.07f, 0.11f)
                batch.setColor(1f, 1f, 1f, fade)
                batch.draw(reg, (ar.x[i] + ox - 4f).toInt().toFloat(), (ar.y[i] + oy - 3f).toInt().toFloat())
            }
        }
        batch.setColor(1f, 1f, 1f, 1f)
    }

    private fun drawPickups(batch: SpriteBatch) {
        val pk = world.pickups
        for (i in 0 until pk.high) {
            if (!pk.active[i]) continue
            val x = pk.x[i]
            val y = pk.y[i]
            if (!visible(x, y, 12f)) continue
            val bob = if (pk.pulled[i]) 0f else (sin(time * 4f + i) * 1.2f).toInt().toFloat()
            val reg: TextureRegion = when (pk.kind[i]) {
                Pick.EMBER_S -> a.region("pick_ember_s")
                Pick.EMBER_M -> a.region("pick_ember_m")
                Pick.EMBER_L -> a.region("pick_ember_l")
                Pick.COIN -> a.frame("pick_coin", time + i * 0.1f, 0.12f)
                Pick.CHEST -> a.frames("pick_chest")[0]
                Pick.SIMIT -> a.region("pick_simit")
                Pick.MAGNET -> a.region("pick_magnet")
                Pick.FLARE -> a.region("pick_flare")
                else -> a.region("pick_oil")
            }
            if (pk.kind[i] == Pick.CHEST) {
                batch.setColor(1f, 1f, 1f, 0.6f)
                batch.draw(shadow, (x - 6f).toInt().toFloat(), (y - 6f).toInt().toFloat())
                batch.setColor(1f, 1f, 1f, 1f)
            }
            batch.draw(reg, (x - reg.regionWidth / 2f).toInt().toFloat(), (y - reg.regionHeight / 2f + bob).toInt().toFloat())
        }
    }

    // ------------------------------------------------------------------ varliklar
    private fun drawEntities(batch: SpriteBatch) {
        val e = world.enemies
        if (sortKeys.size < e.cap + 1) sortKeys = LongArray(e.cap + 1)
        var n = 0
        for (i in 0 until e.high) {
            if (!e.active[i]) continue
            val m = e.radius[i] * e.scale[i] + 24f
            if (!visible(e.x[i], e.y[i], m)) continue
            sortKeys[n++] = key(e.y[i], i)
        }
        sortKeys[n++] = key(world.player.y, PLAYER)
        java.util.Arrays.sort(sortKeys, 0, n)
        // Golgeler once (hepsi varliklarin altinda)
        batch.setColor(1f, 1f, 1f, 1f)
        for (k in 0 until n) {
            val id = (sortKeys[k] and 0xFFFF).toInt()
            if (id == PLAYER) {
                batch.draw(shadow, world.player.x.toInt() - 6f, world.player.y.toInt() - 9f)
            } else {
                val big = e.scale[id] > 1
                val sr = if (big) shadowBig else shadow
                val sw = if (big) sr.regionWidth * (e.radius[id] / 20f).coerceIn(0.6f, 1.5f) else sr.regionWidth.toFloat()
                batch.draw(sr, (e.x[id] - sw / 2f).toInt().toFloat(), (e.y[id] - e.radius[id] * 0.9f - 3f).toInt().toFloat(), sw, sr.regionHeight.toFloat())
            }
        }
        for (k in 0 until n) {
            val id = (sortKeys[k] and 0xFFFF).toInt()
            if (id == PLAYER) drawPlayer(batch) else drawEnemy(batch, id)
        }
        batch.setColor(1f, 1f, 1f, 1f)
    }

    /** y'si buyuk olan (ekranda yukarida) once cizilir. */
    private fun key(y: Float, id: Int): Long = ((-(y * 8f).toLong() + (1L shl 40)) shl 16) or id.toLong()

    private fun drawEnemy(batch: SpriteBatch, i: Int) {
        val e = world.enemies
        val d = e.def[i]!!
        val flash = e.flash[i] > 0f && !settings().reduceFlashes
        val sprite = if (flash) d.sprite + "_white" else d.sprite
        val anim = if (e.stun[i] > 0f) 0f else e.anim[i]
        val reg = a.frame(sprite, anim, d.frameTime)
        val s = e.scale[i]
        val w = reg.regionWidth * s
        val h = reg.regionHeight * s
        val x = (e.x[i] - w / 2f).toInt().toFloat()
        val y = (e.y[i] - h / 2f).toInt().toFloat()
        if (e.elite[i]) {
            val pulse = 0.35f + 0.15f * sin(time * 6f)
            batch.setColor(1f, 0.4f, 0.25f, pulse)
            val r = w * 0.8f
            batch.draw(circle32, e.x[i] - r / 2f, e.y[i] - r / 2f, r, r)
        }
        when {
            flash -> batch.setColor(1f, 1f, 1f, 1f)
            e.state[i] == EState.WINDUP && (time * 14f).toInt() % 2 == 0 -> batch.setColor(1f, 0.45f, 0.45f, 1f)
            e.stun[i] > 0f -> batch.setColor(0.7f, 0.85f, 1f, 1f)
            d.tier == Tier.NORMAL -> batch.color = enemyTint
            else -> batch.setColor(1f, 1f, 1f, 1f)
        }
        if (e.facing[i] < 0) batch.draw(reg, x + w, y, -w.toFloat(), h.toFloat())
        else batch.draw(reg, x, y, w.toFloat(), h.toFloat())
    }

    private fun drawPlayer(batch: SpriteBatch) {
        val p = world.player
        val ch = world.config.character
        val reg = if (p.moving) a.frame("${ch.sprite}_walk", p.anim, 0.12f) else a.frame("${ch.sprite}_idle", p.anim, 0.5f)
        if (p.iframes > 0f && p.hurtFlash <= 0f && world.endTimer < 0f && (time * 16f).toInt() % 2 == 0) return
        if (p.hurtFlash > 0f) batch.setColor(1f, 0.35f, 0.35f, 1f) else batch.setColor(1f, 1f, 1f, 1f)
        val x = p.x.toInt() - 8f
        val y = p.y.toInt() - 7f
        if (p.flipX) batch.draw(reg, x + 16f, y, -16f, 16f) else batch.draw(reg, x, y, 16f, 16f)
        batch.setColor(1f, 1f, 1f, 1f)
    }

    private fun drawShots(batch: SpriteBatch) {
        val s = world.shots
        for (i in 0 until s.high) {
            if (!s.active[i]) continue
            if (!visible(s.x[i], s.y[i], 16f)) continue
            val name = s.sprite[i] ?: continue
            val reg = a.frame(name, time + i * 0.05f, if (s.kind[i] == PKind.MOTH) 0.08f else 0.1f)
            val w = reg.regionWidth.toFloat()
            val h = reg.regionHeight.toFloat()
            val sc = s.scale[i]
            val rot = when (s.kind[i]) {
                PKind.DAGGER, PKind.SICKLE -> s.rot[i] * 57.29578f
                else -> 0f
            }
            val fade = if (s.maxLife[i] < 1e8f) (s.life[i] / 0.15f).coerceAtMost(1f) else 1f
            batch.setColor(1f, 1f, 1f, fade)
            batch.draw(reg, (s.x[i] - w / 2f).toInt().toFloat(), (s.y[i] - h / 2f).toInt().toFloat(), w / 2f, h / 2f, w, h, sc, sc, rot)
        }
        batch.setColor(1f, 1f, 1f, 1f)
        // Kor halkasi (aura): oyuncunun cevresinde donen kozler
        for (wp in world.weapons) {
            if (wp !is RingWeapon) continue
            val r = wp.radius
            val n = (r / 4f).toInt().coerceIn(8, 28)
            val p = world.player
            val evo = wp.evolved
            for (k in 0 until n) {
                val ang = k * Rng.TAU / n + time * 1.5f
                val x = p.x + cos(ang) * r
                val y = p.y + sin(ang) * r * 0.9f
                val flick = 0.55f + 0.45f * sin(time * 9f + k * 1.7f)
                if (evo) batch.setColor(1f, 0.95f, 0.6f, flick) else batch.setColor(1f, 0.55f, 0.2f, flick)
                batch.draw(px, x.toInt().toFloat(), y.toInt().toFloat(), 2f, 2f)
            }
        }
        batch.setColor(1f, 1f, 1f, 1f)
    }

    private fun drawEnemyShots(batch: SpriteBatch) {
        val s = world.enemyShots
        for (i in 0 until s.high) {
            if (!s.active[i]) continue
            if (!visible(s.x[i], s.y[i], 10f)) continue
            val reg = a.frame(s.sprite[i] ?: "proj_bolt", time * 1.5f + i * 0.1f, 0.12f)
            batch.draw(reg, (s.x[i] - reg.regionWidth / 2f).toInt().toFloat(), (s.y[i] - reg.regionHeight / 2f).toInt().toFloat())
        }
    }

    private fun drawBellRings(batch: SpriteBatch) {
        val ar = world.areas
        for (i in 0 until ar.high) {
            if (!ar.active[i] || ar.kind[i] != AKind.BELL_RING) continue
            val r = ar.radius[i]
            val fade = min(1f, ar.life[i] / 0.12f) * (1f - 0.5f * r / ar.maxRadius[i])
            val n = (r * Rng.TAU / 4f).toInt().coerceIn(8, 200)
            if (ar.evolved[i]) batch.setColor(1f, 0.95f, 0.75f, fade) else batch.setColor(1f, 0.85f, 0.45f, fade)
            val size = if (ar.evolved[i]) 2f else 1f
            for (k in 0 until n) {
                val ang = k * Rng.TAU / n
                batch.draw(px, (ar.x[i] + cos(ang) * r).toInt().toFloat(), (ar.y[i] + sin(ang) * r).toInt().toFloat(), size, size)
            }
        }
        batch.setColor(1f, 1f, 1f, 1f)
    }

    private fun line(batch: SpriteBatch, x1: Float, y1: Float, x2: Float, y2: Float, width: Float) {
        val dx = x2 - x1
        val dy = y2 - y1
        val len = sqrt(dx * dx + dy * dy)
        val ang = atan2(dy, dx) * 57.29578f
        batch.draw(px, x1, y1 - width / 2f, 0f, width / 2f, len, width, 1f, 1f, ang)
    }

    private fun drawBeams(batch: SpriteBatch) {
        val b = world.beams
        for (i in 0 until b.high) {
            if (!b.active[i]) continue
            val alpha = (b.life[i] / 0.22f).coerceIn(0f, 1f)
            var h = b.seed[i]
            var lx = b.x1[i]
            var ly = b.y1[i]
            val segs = 5
            val dx = (b.x2[i] - b.x1[i])
            val dy = (b.y2[i] - b.y1[i])
            val len = sqrt(dx * dx + dy * dy).coerceAtLeast(1f)
            val nx = -dy / len
            val ny = dx / len
            for (k in 1..segs) {
                h = h * 1103515245 + 12345
                val t = k / segs.toFloat()
                val off = if (k == segs) 0f else ((h ushr 16) % 13 - 6).toFloat()
                val x = b.x1[i] + dx * t + nx * off
                val y = b.y1[i] + dy * t + ny * off
                if (b.evolved[i]) batch.setColor(1f, 0.9f, 0.4f, alpha * 0.5f) else batch.setColor(0.4f, 0.85f, 1f, alpha * 0.5f)
                line(batch, lx, ly, x, y, 3f)
                batch.setColor(1f, 1f, 1f, alpha)
                line(batch, lx, ly, x, y, 1f)
                lx = x; ly = y
            }
        }
        batch.setColor(1f, 1f, 1f, 1f)
    }

    // ------------------------------------------------------------------ isiklar
    private fun light(batch: SpriteBatch, x: Float, y: Float, radius: Float, rgba: Int, alpha: Float = 1f) {
        Color.rgba8888ToColor(tmp, rgba)
        tmp.a = alpha
        batch.color = tmp
        batch.draw(a.light, x - radius, y - radius, radius * 2f, radius * 2f)
    }

    private fun drawLights(batch: SpriteBatch) {
        val p = world.player
        val r = world.lightRadius * (1f + 0.025f * sin(time * 11f) + 0.015f * sin(time * 23f))
        val lantern = if (supporter) 0xFFE8A0FF.toInt() else 0xFFC890FF.toInt()
        light(batch, p.x, p.y, r, lantern, 0.95f)
        light(batch, p.x, p.y, r * 0.45f, 0xFFE0B0FF.toInt(), 0.5f)
        for (wp in world.weapons) if (wp is RingWeapon) light(batch, p.x, p.y, wp.radius * 1.7f, 0xFF8040FF.toInt(), 0.35f)

        val s = world.shots
        for (i in 0 until s.high) {
            if (!s.active[i] || !visible(s.x[i], s.y[i], 40f)) continue
            when (s.kind[i]) {
                PKind.SPARK -> light(batch, s.x[i], s.y[i], if (s.evolved[i]) 34f else 18f, 0xFFE070FF.toInt(), 0.8f)
                PKind.MOTH -> light(batch, s.x[i], s.y[i], if (s.evolved[i]) 24f else 14f, if (s.evolved[i]) 0xFF8030FF.toInt() else 0xC090FFFF.toInt(), 0.7f)
                PKind.DAGGER -> if (s.evolved[i]) light(batch, s.x[i], s.y[i], 12f, 0x70E0FFFF, 0.6f)
                PKind.SICKLE -> if (s.evolved[i]) light(batch, s.x[i], s.y[i], 18f, 0xFF4040FF.toInt(), 0.6f)
            }
        }
        val ar = world.areas
        for (i in 0 until ar.high) {
            if (!ar.active[i] || ar.kind[i] != AKind.FLAME || !visible(ar.x[i], ar.y[i], 40f)) continue
            val fade = (ar.life[i] / 0.4f).coerceAtMost(1f)
            light(batch, ar.x[i], ar.y[i], ar.radius[i] * 2.4f, if (ar.evolved[i]) 0x60C0FFFF else 0xFF8030FF.toInt(), 0.55f * fade)
        }
        val es = world.enemyShots
        for (i in 0 until es.high) {
            if (!es.active[i] || !visible(es.x[i], es.y[i], 30f)) continue
            light(batch, es.x[i], es.y[i], 16f, 0x60D0FFFF, 0.8f)
        }
        val b = world.beams
        for (i in 0 until b.high) {
            if (!b.active[i]) continue
            light(batch, b.x2[i], b.y2[i], 30f, if (b.evolved[i]) 0xFFE070FF.toInt() else 0x80D0FFFF.toInt(), b.life[i] / 0.22f)
        }
        val pk = world.pickups
        for (i in 0 until pk.high) {
            if (!pk.active[i] || !visible(pk.x[i], pk.y[i], 20f)) continue
            when (pk.kind[i]) {
                Pick.EMBER_S -> light(batch, pk.x[i], pk.y[i], 7f, 0xFF9030FF.toInt(), 0.5f)
                Pick.EMBER_M -> light(batch, pk.x[i], pk.y[i], 10f, 0xFF7030FF.toInt(), 0.6f)
                Pick.EMBER_L -> light(batch, pk.x[i], pk.y[i], 14f, 0xFF5030FF.toInt(), 0.7f)
                Pick.CHEST -> light(batch, pk.x[i], pk.y[i], 30f, 0xFFD060FF.toInt(), 0.7f)
                else -> light(batch, pk.x[i], pk.y[i], 10f, 0xFFE0A0FF.toInt(), 0.4f)
            }
        }
        // Isik kaynagi olan dusmanlar (ruhlar, hayalet fener) ve bosslar
        val e = world.enemies
        for (i in 0 until e.high) {
            if (!e.active[i] || !visible(e.x[i], e.y[i], 40f)) continue
            val d = e.def[i]!!
            when {
                d.id == "wisp" || d.id == "ghostlamp" -> light(batch, e.x[i], e.y[i], 18f, 0x60D0FFFF, 0.45f)
                d.tier != Tier.NORMAL -> light(batch, e.x[i], e.y[i], 26f * e.scale[i], d.eyeColor, 0.18f)
            }
        }
        for (i in 0 until flashes.high) {
            if (!flashes.active[i]) continue
            val t = flashes.life[i] / flashes.maxLife[i]
            light(batch, flashes.x[i], flashes.y[i], flashes.radius[i] * (1.2f - 0.2f * t), flashes.color[i], t)
        }
        batch.setColor(1f, 1f, 1f, 1f)
    }

    // ------------------------------------------------------------------ overlay
    private fun drawEyes(batch: SpriteBatch) {
        val e = world.enemies
        val p = world.player
        val r = world.lightRadius
        val inner = r * 0.7f
        val span = r * 0.45f
        if (world.dawnFactor >= 0.99f) return
        for (i in 0 until e.high) {
            if (!e.active[i]) continue
            val m = e.radius[i] * e.scale[i] + 24f
            if (!visible(e.x[i], e.y[i], m)) continue
            val dx = e.x[i] - p.x
            val dy = e.y[i] - p.y
            val dist = sqrt(dx * dx + dy * dy)
            if (dist < inner) continue
            val alpha = ((dist - inner) / span).coerceIn(0f, 1f) * (1f - world.dawnFactor)
            val d = e.def[i]!!
            val reg = a.region(d.sprite + "_eyes")
            val s = e.scale[i]
            val w = reg.regionWidth * s
            val h = reg.regionHeight * s
            val x = (e.x[i] - w / 2f).toInt().toFloat()
            val y = (e.y[i] - h / 2f).toInt().toFloat()
            Color.rgba8888ToColor(tmp, d.eyeColor)
            // Goz kirpma: her dusman kendi ritminde
            val blink = ((e.anim[i] + e.uid[i] * 0.37f) % 3.3f) < 0.12f
            tmp.a = if (blink) 0f else alpha
            batch.color = tmp
            if (e.facing[i] < 0) batch.draw(reg, x + w, y, -w.toFloat(), h.toFloat())
            else batch.draw(reg, x, y, w.toFloat(), h.toFloat())
        }
        batch.setColor(1f, 1f, 1f, 1f)
    }

    /** Boss/ara boss hucum uyarisi: atilacagi yon boyunca yanip sonen cizgi. */
    private fun drawTelegraphs(batch: SpriteBatch) {
        val e = world.enemies
        for (i in 0 until e.high) {
            if (!e.active[i] || e.state[i] != EState.WINDUP) continue
            val d = e.def[i]!!
            if (d.tier == Tier.NORMAL && !e.elite[i]) continue
            val blink = 0.25f + 0.3f * (0.5f + 0.5f * sin(time * 30f))
            batch.setColor(1f, 0.25f, 0.2f, blink)
            val len = 160f
            val w = e.radius[i] * 1.2f
            val ang = atan2(e.ay[i], e.ax[i]) * 57.29578f
            batch.draw(px, e.x[i], e.y[i] - w / 2f, 0f, w / 2f, len, w, 1f, 1f, ang)
        }
        batch.setColor(1f, 1f, 1f, 1f)
    }

    private fun drawPlayerBar(batch: SpriteBatch) {
        val p = world.player
        val frac = (p.hp / world.stats.maxHp).coerceIn(0f, 1f)
        if (frac >= 0.999f || world.endTimer >= 0f) return
        val x = p.x.toInt() - 8f
        val y = p.y.toInt() - 13f
        batch.setColor(0.08f, 0.05f, 0.12f, 0.9f)
        batch.draw(px, x - 1f, y - 1f, 18f, 4f)
        batch.setColor(0.35f, 0.1f, 0.12f, 1f)
        batch.draw(px, x, y, 16f, 2f)
        batch.setColor(if (frac < 0.3f) 1f else 0.9f, if (frac < 0.3f) 0.3f else 0.25f, 0.25f, 1f)
        batch.draw(px, x, y, (16f * frac).toInt().coerceAtLeast(1).toFloat(), 2f)
        batch.setColor(1f, 1f, 1f, 1f)
    }

    companion object {
        private const val PLAYER = 0xFFFF
    }
}

/** Kisa sureli buyuk isiklar (patlama, seviye, safak). */
class Flashes(cap: Int) : com.lastlantern.sim.SlotPool(cap) {
    val x = FloatArray(cap)
    val y = FloatArray(cap)
    val radius = FloatArray(cap)
    val life = FloatArray(cap)
    val maxLife = FloatArray(cap)
    val color = IntArray(cap)

    fun add(px: Float, py: Float, r: Float, l: Float, rgba: Int) {
        val i = alloc()
        if (i < 0) return
        x[i] = px; y[i] = py; radius[i] = r; life[i] = l; maxLife[i] = l; color[i] = rgba
    }

    fun update(dt: Float) {
        for (i in 0 until high) {
            if (!active[i]) continue
            life[i] -= dt
            if (life[i] <= 0f) release(i)
        }
    }
}
