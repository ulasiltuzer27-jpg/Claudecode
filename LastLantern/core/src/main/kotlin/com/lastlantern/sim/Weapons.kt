package com.lastlantern.sim

import com.lastlantern.content.WeaponDef
import com.lastlantern.content.WeaponKind
import kotlin.math.atan2
import kotlin.math.cos
import kotlin.math.max
import kotlin.math.sin
import kotlin.math.sqrt

/**
 * Bir silah ornegi: tanim + seviye + bekleme. Istatistikler (temel + seviye
 * farklari) [recompute] ile toplanir, oyuncu carpanlari getter'larda uygulanir.
 */
abstract class Weapon(protected val w: World, var def: WeaponDef, val slot: Int) {
    var level = 1
    protected var timer = 0.4f
    var totalDamage = 0f

    var baseDamage = 0f; private set
    var baseCooldown = 0f; private set
    var baseAmount = 0; private set
    var basePierce = 0; private set
    var baseArea = 0f; private set
    var baseSpeed = 0f; private set
    var baseDuration = 0f; private set
    var baseKnock = 0f; private set

    init {
        recompute()
    }

    fun recompute() {
        val b = def.base
        baseDamage = b.damage; baseCooldown = b.cooldown; baseAmount = b.amount; basePierce = b.pierce
        baseArea = b.area; baseSpeed = b.speed; baseDuration = b.duration; baseKnock = b.knockback
        for (k in 0 until level - 1) {
            val d = def.levels[k]
            baseDamage += d.damage; baseCooldown += d.cooldown; baseAmount += d.amount; basePierce += d.pierce
            baseArea += d.area; baseSpeed += d.speed; baseDuration += d.duration; baseKnock += d.knockback
        }
    }

    val damage get() = baseDamage * w.stats.might
    val cooldown get() = max(0.08f, baseCooldown * w.stats.cooldown)
    val amount get() = baseAmount + w.stats.amount
    val area get() = baseArea * w.stats.area
    val duration get() = baseDuration * w.stats.duration
    val projSpeed get() = baseSpeed * w.stats.projSpeed
    val evolved get() = def.evolved
    val maxed get() = level >= def.maxLevel

    fun levelUp() {
        if (level < def.maxLevel) {
            level++
            recompute()
            onChanged()
        }
    }

    fun evolveInto(evo: WeaponDef) {
        def = evo
        level = 1
        recompute()
        onChanged()
    }

    protected open fun onChanged() {}

    abstract fun update(dt: Float)

    protected fun sfx() {
        w.events.emit(Ev.SFX, w.player.x, w.player.y, 1f, sfxId)
    }

    private val sfxId = Sfx.of(def.sfx).ordinal

    // ---- Hedefleme ---------------------------------------------------------
    private val tmpIdx = IntArray(16)
    private val tmpDist = FloatArray(16)

    /** Isik menzilindeki en yakin [k] dusmani bulur; bulunan sayiyi dondurur. */
    protected fun nearest(k: Int, out: IntArray): Int {
        val e = w.enemies
        val px = w.player.x
        val py = w.player.y
        val range = w.targetRange
        val r2 = range * range
        var n = 0
        val kk = minOf(k, tmpIdx.size)
        for (i in 0 until e.high) {
            if (!e.active[i]) continue
            val dx = e.x[i] - px
            val dy = e.y[i] - py
            val d2 = dx * dx + dy * dy
            if (d2 > r2) continue
            if (n < kk) {
                tmpIdx[n] = i; tmpDist[n] = d2; n++
            } else {
                var worst = 0
                for (q in 1 until kk) if (tmpDist[q] > tmpDist[worst]) worst = q
                if (d2 < tmpDist[worst]) {
                    tmpIdx[worst] = i; tmpDist[worst] = d2
                }
            }
        }
        // Yakindan uzaga sirala (kucuk n icin ekleme siralamasi)
        for (a in 1 until n) {
            var b = a
            while (b > 0 && tmpDist[b - 1] > tmpDist[b]) {
                val ti = tmpIdx[b]; tmpIdx[b] = tmpIdx[b - 1]; tmpIdx[b - 1] = ti
                val td = tmpDist[b]; tmpDist[b] = tmpDist[b - 1]; tmpDist[b - 1] = td
                b--
            }
        }
        for (a in 0 until n) out[a] = tmpIdx[a]
        return n
    }

    protected fun randomVisible(): Int {
        val e = w.enemies
        val px = w.player.x
        val py = w.player.y
        val r2 = w.targetRange * w.targetRange
        var pickI = -1
        var seen = 0
        for (i in 0 until e.high) {
            if (!e.active[i]) continue
            val dx = e.x[i] - px
            val dy = e.y[i] - py
            if (dx * dx + dy * dy > r2) continue
            seen++
            if (w.rng.nextInt(seen) == 0) pickI = i
        }
        return pickI
    }

    companion object {
        fun create(w: World, def: WeaponDef, slot: Int): Weapon = when (def.kind) {
            WeaponKind.SPARK -> SparkWeapon(w, def, slot)
            WeaponKind.MOTHS -> MothWeapon(w, def, slot)
            WeaponKind.RING -> RingWeapon(w, def, slot)
            WeaponKind.CHAIN -> ChainWeapon(w, def, slot)
            WeaponKind.BELL -> BellWeapon(w, def, slot)
            WeaponKind.DAGGER -> DaggerWeapon(w, def, slot)
            WeaponKind.FLAME -> FlameWeapon(w, def, slot)
            WeaponKind.SICKLE -> SickleWeapon(w, def, slot)
        }
    }
}

/** Fener Kivilcimi / Gunes Mizragi: en yakin gorunen dusmanlara mermi. */
class SparkWeapon(w: World, def: WeaponDef, slot: Int) : Weapon(w, def, slot) {
    private val targets = IntArray(16)

    override fun update(dt: Float) {
        timer -= dt
        if (timer > 0f) return
        val n = amount
        val found = nearest(n, targets)
        if (found == 0) {
            timer = 0f
            return
        }
        timer = cooldown
        val p = w.player
        for (k in 0 until n) {
            val t = targets[k % found]
            var dx = w.enemies.x[t] - p.x
            var dy = w.enemies.y[t] - p.y
            val l = sqrt(dx * dx + dy * dy).coerceAtLeast(0.001f)
            dx /= l; dy /= l
            if (k >= found) {
                // Hedeften fazla mermi: hafif yelpaze
                val a = atan2(dy, dx) + (k - found + 1) * 0.18f * (if (k % 2 == 0) 1 else -1)
                dx = cos(a); dy = sin(a)
            }
            val i = w.shots.spawn(PKind.SPARK, PMove.STRAIGHT, p.x, p.y - 4f)
            if (i < 0) return
            val s = w.shots
            s.vx[i] = dx * projSpeed
            s.vy[i] = dy * projSpeed
            s.radius[i] = (if (evolved) 5f else 3f) * area
            s.damage[i] = damage
            s.pierce[i] = basePierce
            s.life[i] = duration; s.maxLife[i] = duration
            s.knock[i] = baseKnock
            s.slot[i] = slot
            s.scale[i] = area
            s.evolved[i] = evolved
            s.sprite[i] = if (evolved) "proj_solar" else "proj_spark"
        }
        sfx()
    }
}

/** Guve Surusu / Anka Guveleri: oyuncunun cevresinde donen guveler. */
class MothWeapon(w: World, def: WeaponDef, slot: Int) : Weapon(w, def, slot) {
    private val owned = IntArray(16) { -1 }

    override fun onChanged() {
        if (evolved) despawnAll()
    }

    private fun despawnAll() {
        for (k in owned.indices) {
            val i = owned[k]
            if (i >= 0 && w.shots.active[i] && w.shots.kind[i] == PKind.MOTH) w.shots.release(i)
            owned[k] = -1
        }
    }

    private fun alive(): Int {
        var n = 0
        for (k in owned.indices) {
            val i = owned[k]
            if (i >= 0 && w.shots.active[i] && w.shots.kind[i] == PKind.MOTH && w.shots.slot[i] == slot) n++
            else owned[k] = -1
        }
        return n
    }

    override fun update(dt: Float) {
        val n = minOf(amount, owned.size)
        if (evolved) {
            if (alive() < n) {
                despawnAll()
                launch(n, life = 1e9f)
            }
            // Kalici yorunge: istatistik degisikliklerini canli uygula
            for (k in owned.indices) {
                val i = owned[k]
                if (i >= 0) {
                    w.shots.orbitR[i] = 46f * area
                    w.shots.damage[i] = damage
                }
            }
            return
        }
        timer -= dt
        if (timer > 0f) return
        if (alive() > 0) return
        timer = cooldown + duration
        launch(n, duration)
        sfx()
    }

    private fun launch(n: Int, life: Float) {
        val p = w.player
        val base = w.time * 1.3f
        for (k in 0 until n) {
            val i = w.shots.spawn(PKind.MOTH, PMove.ORBIT, p.x, p.y)
            if (i < 0) return
            val s = w.shots
            s.angle[i] = base + k * Rng.TAU / n
            s.angVel[i] = projSpeed
            s.orbitR[i] = (if (evolved) 46f else 40f) * area
            s.radius[i] = 5f * area
            s.damage[i] = damage
            s.pierce[i] = 999
            s.life[i] = life; s.maxLife[i] = life
            s.knock[i] = baseKnock
            s.slot[i] = slot
            s.rehit[i] = 0.6f
            s.rehitTimer[i] = 0.6f
            s.evolved[i] = evolved
            s.sprite[i] = if (evolved) "proj_phoenix" else "proj_moth"
            owned[k] = i
        }
    }
}

/** Kor Halkasi / Gunes Halesi: oyuncunun etrafinda yakici alan. */
class RingWeapon(w: World, def: WeaponDef, slot: Int) : Weapon(w, def, slot) {
    val radius get() = (if (evolved) 34f else 30f) * area
    var pulse = 0f
        private set

    override fun update(dt: Float) {
        pulse += dt
        timer -= dt
        if (timer > 0f) return
        timer = cooldown
        val p = w.player
        val r = radius
        var touched = 0
        w.grid.query(p.x, p.y, r + 24f) { j ->
            val e = w.enemies
            if (e.active[j]) {
                val dx = e.x[j] - p.x
                val dy = e.y[j] - p.y
                val rr = r + e.radius[j]
                if (dx * dx + dy * dy < rr * rr) {
                    touched++
                    w.hitEnemy(j, damage, p.x, p.y, baseKnock, slot)
                }
            }
        }
        if (evolved && touched > 0) w.heal(0.8f * cooldown)
    }
}

/** Zincir Simsek / Firtina Taci: gorunen dusmana dusup yakindakilere sicrar. */
class ChainWeapon(w: World, def: WeaponDef, slot: Int) : Weapon(w, def, slot) {
    private val hitUids = IntArray(32)

    override fun update(dt: Float) {
        timer -= dt
        if (timer > 0f) return
        val first = randomVisible()
        if (first < 0) {
            timer = 0f
            return
        }
        timer = cooldown
        for (strike in 0 until amount) {
            var cur = if (strike == 0) first else randomVisible()
            if (cur < 0) break
            val e = w.enemies
            var fromX = e.x[cur] + w.rng.range(-10f, 10f)
            var fromY = e.y[cur] - 90f
            var hitN = 0
            val chainRange = 64f * area
            for (link in 0..basePierce) {
                val tx = e.x[cur]
                val ty = e.y[cur]
                beam(fromX, fromY, tx, ty)
                if (hitN < hitUids.size) hitUids[hitN++] = e.uid[cur]
                w.hitEnemy(cur, damage, tx, ty - 1f, 0.2f, slot)
                fromX = tx; fromY = ty
                // Sonraki halka: henuz vurulmamis en yakin dusman
                var best = -1
                var bestD = chainRange * chainRange
                w.grid.query(tx, ty, chainRange) { j ->
                    if (e.active[j]) {
                        var seen = false
                        for (q in 0 until hitN) if (hitUids[q] == e.uid[j]) seen = true
                        if (!seen) {
                            val dx = e.x[j] - tx
                            val dy = e.y[j] - ty
                            val d2 = dx * dx + dy * dy
                            if (d2 < bestD) {
                                bestD = d2; best = j
                            }
                        }
                    }
                }
                if (best < 0) break
                cur = best
            }
        }
        sfx()
        w.events.emit(Ev.SHAKE, av = 1.2f)
    }

    private fun beam(x1: Float, y1: Float, x2: Float, y2: Float) {
        val b = w.beams
        val i = b.alloc()
        if (i < 0) return
        b.x1[i] = x1; b.y1[i] = y1; b.x2[i] = x2; b.y2[i] = y2
        b.life[i] = 0.22f
        b.evolved[i] = evolved
        b.seed[i] = w.rng.nextInt(10000)
    }
}

/** Can Dalgasi / Safak Cani: genisleyen, iten halka. */
class BellWeapon(w: World, def: WeaponDef, slot: Int) : Weapon(w, def, slot) {
    private var pending = 0
    private var pendingTimer = 0f

    override fun update(dt: Float) {
        if (pending > 0) {
            pendingTimer -= dt
            if (pendingTimer <= 0f) {
                ring()
                pending--
                pendingTimer = 0.28f
            }
        }
        timer -= dt
        if (timer > 0f) return
        timer = cooldown
        pending = amount
        pendingTimer = 0f
        sfx()
    }

    private fun ring() {
        val a = w.areas
        val i = a.alloc()
        if (i < 0) return
        a.kind[i] = AKind.BELL_RING
        a.x[i] = w.player.x
        a.y[i] = w.player.y
        a.radius[i] = 4f
        a.maxRadius[i] = 64f * area
        a.tick[i] = projSpeed       // genisleme hizi
        a.life[i] = 3f; a.maxLife[i] = 3f
        a.damage[i] = damage
        a.knock[i] = baseKnock
        a.slot[i] = slot
        a.id[i] = w.nextRingId++
        a.evolved[i] = evolved
    }
}

/**
 * Hancer / Yildiz Bicaklari: en yakin dusmana yelpaze halinde hizli bicaklar.
 * Hedef yoksa hareket yonune atilir. (Yalnizca hareket yonune atmak, kacarak
 * oynayan mobil oyuncuda bicaklari dusmanin tersine gonderiyordu.)
 */
class DaggerWeapon(w: World, def: WeaponDef, slot: Int) : Weapon(w, def, slot) {
    private val target = IntArray(1)

    override fun update(dt: Float) {
        timer -= dt
        if (timer > 0f) return
        timer = cooldown
        val p = w.player
        val base = if (nearest(1, target) > 0) {
            atan2(w.enemies.y[target[0]] - p.y, w.enemies.x[target[0]] - p.x)
        } else {
            atan2(p.faceY, p.faceX)
        }
        val n = amount
        for (k in 0 until n) {
            val off = (k - (n - 1) / 2f) * 0.13f
            val a = base + off
            val i = w.shots.spawn(PKind.DAGGER, PMove.STRAIGHT, p.x + cos(a) * 4f, p.y - 3f + sin(a) * 4f)
            if (i < 0) return
            val s = w.shots
            s.vx[i] = cos(a) * projSpeed
            s.vy[i] = sin(a) * projSpeed
            s.rot[i] = a
            s.radius[i] = 3f * area
            s.damage[i] = damage
            s.pierce[i] = basePierce
            s.life[i] = duration; s.maxLife[i] = duration
            s.knock[i] = baseKnock
            s.slot[i] = slot
            s.evolved[i] = evolved
            s.scale[i] = area
            s.sprite[i] = if (evolved) "proj_starknife" else "proj_dagger"
        }
        sfx()
    }
}

/** Alev Izi / Mavi Cehennem: yuruyus yolunda yanan alanlar. */
class FlameWeapon(w: World, def: WeaponDef, slot: Int) : Weapon(w, def, slot) {
    private var lastX = Float.NaN
    private var lastY = Float.NaN
    private var sfxTimer = 0f

    override fun update(dt: Float) {
        timer -= dt
        sfxTimer -= dt
        if (timer > 0f) return
        val p = w.player
        val moved = lastX.isNaN() || (p.x - lastX) * (p.x - lastX) + (p.y - lastY) * (p.y - lastY) > 36f
        // Dururken daha seyrek birakir (ayni noktaya yigilmasin)
        timer = if (moved) cooldown else cooldown * 2.5f
        lastX = p.x
        lastY = p.y
        val a = w.areas
        val i = a.alloc()
        if (i < 0) return
        a.kind[i] = AKind.FLAME
        a.x[i] = p.x + w.rng.range(-2f, 2f)
        a.y[i] = p.y + 4f
        a.radius[i] = 9f * area
        a.maxRadius[i] = a.radius[i]
        a.life[i] = duration; a.maxLife[i] = duration
        a.damage[i] = damage
        a.tick[i] = 0f
        a.slot[i] = slot
        a.evolved[i] = evolved
        if (sfxTimer <= 0f) {
            sfx()
            sfxTimer = 1.2f
        }
    }
}

/** Orak / Olum Tirpani: gidip donen ya da sarmal cizen biçaklar. */
class SickleWeapon(w: World, def: WeaponDef, slot: Int) : Weapon(w, def, slot) {
    private val targets = IntArray(16)

    override fun update(dt: Float) {
        timer -= dt
        if (timer > 0f) return
        timer = cooldown
        val p = w.player
        val n = amount
        if (evolved) {
            for (k in 0 until n) {
                val i = w.shots.spawn(PKind.SICKLE, PMove.SPIRAL, p.x, p.y)
                if (i < 0) return
                val s = w.shots
                s.angle[i] = w.time * 2f + k * Rng.TAU / n
                s.angVel[i] = 4.6f
                s.orbitR[i] = 110f * area
                common(i, life = 1.7f * baseDuration)
            }
            sfx()
            return
        }
        val found = nearest(n, targets)
        for (k in 0 until n) {
            var dx: Float
            var dy: Float
            if (found > 0) {
                val t = targets[k % found]
                dx = w.enemies.x[t] - p.x
                dy = w.enemies.y[t] - p.y
                val l = sqrt(dx * dx + dy * dy).coerceAtLeast(0.001f)
                dx /= l; dy /= l
                if (k >= found) {
                    val a = atan2(dy, dx) + 0.5f * (k - found + 1)
                    dx = cos(a); dy = sin(a)
                }
            } else {
                val a = atan2(p.faceY, p.faceX) + k * Rng.TAU / n
                dx = cos(a); dy = sin(a)
            }
            val i = w.shots.spawn(PKind.SICKLE, PMove.BOOMERANG, p.x, p.y)
            if (i < 0) return
            val s = w.shots
            s.vx[i] = dx * projSpeed
            s.vy[i] = dy * projSpeed
            // angle = donmeye kalan sure; orbitR = ilk sure (hiz azalma orani icin)
            val out = 0.55f * duration
            s.angle[i] = out
            s.orbitR[i] = out
            common(i, life = out + 3f)
        }
        sfx()
    }

    private fun common(i: Int, life: Float) {
        val s = w.shots
        s.radius[i] = 6f * area
        s.damage[i] = damage
        s.pierce[i] = 999
        s.life[i] = life; s.maxLife[i] = life
        s.knock[i] = baseKnock
        s.slot[i] = slot
        s.spin[i] = 14f
        s.scale[i] = area
        s.evolved[i] = evolved
        s.rehit[i] = if (evolved) 0.8f else 0f
        s.rehitTimer[i] = 0.5f
        s.sprite[i] = if (evolved) "proj_reaper" else "proj_sickle"
    }
}
