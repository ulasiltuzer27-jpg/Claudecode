package com.lastlantern.sim

import com.lastlantern.content.EnemyDef

/*
 * Simulasyonun veri yapilari. Hepsi sabit kapasiteli ve dizi tabanli (SoA):
 * oyun dongusunde hic nesne ayrilmaz, boylece Android'de GC duraklamasi
 * olmaz. Bir slotun canli olup olmadigi `active` ile tutulur; bos slotlar
 * bir yigin (free list) uzerinden geri verilir.
 */

/** Sabit kapasiteli slot yoneticisi. */
open class SlotPool(val cap: Int) {
    val active = BooleanArray(cap)
    private val free = IntArray(cap) { cap - 1 - it }
    private var freeTop = cap
    var count = 0
        private set
    /** Kullanilmis en yuksek slot + 1; donguler buraya kadar gider. */
    var high = 0
        private set

    fun alloc(): Int {
        if (freeTop == 0) return -1
        val i = free[--freeTop]
        active[i] = true
        count++
        if (i >= high) high = i + 1
        return i
    }

    fun release(i: Int) {
        if (!active[i]) return
        active[i] = false
        free[freeTop++] = i
        count--
        while (high > 0 && !active[high - 1]) high--
    }

    open fun clear() {
        for (i in 0 until high) if (active[i]) release(i)
    }
}

object EState {
    const val NORMAL = 0
    const val WINDUP = 1   // saldiri oncesi uyari (telegraf)
    const val DASH = 2
    const val SWARM = 3    // tek yonde akan surudeki uye
    const val COOLDOWN = 4
}

class EnemyPool(cap: Int, val weaponSlots: Int) : SlotPool(cap) {
    val uid = IntArray(cap)
    val def = arrayOfNulls<EnemyDef>(cap)
    val defIndex = IntArray(cap)
    val x = FloatArray(cap)
    val y = FloatArray(cap)
    val kbx = FloatArray(cap)      // geri tepme hizi
    val kby = FloatArray(cap)
    val hp = FloatArray(cap)
    val maxHp = FloatArray(cap)
    val radius = FloatArray(cap)
    val speed = FloatArray(cap)
    val damage = FloatArray(cap)
    val scale = IntArray(cap)
    val elite = BooleanArray(cap)
    val state = IntArray(cap)
    val timer = FloatArray(cap)    // davranis zamanlayicisi
    val timer2 = FloatArray(cap)   // ikinci zamanlayici (ates, cagirma)
    val ax = FloatArray(cap)       // davranis yonu (hucum, suru)
    val ay = FloatArray(cap)
    val flash = FloatArray(cap)    // vurulunca beyaz parlama
    val slow = FloatArray(cap)
    val stun = FloatArray(cap)
    val anim = FloatArray(cap)
    val facing = IntArray(cap)
    /**
     * Son vuran can dalgalarinin kimlikleri (4'luk halka tampon). Tek bir
     * "son halka" alani yetmiyordu: ayni anda iki halka varken birbirinin
     * kaydini ezip ayni dusmana her karede yeniden vuruyorlardi.
     */
    val ringHits = IntArray(cap * 4)
    val ringNext = IntArray(cap)
    val phase = IntArray(cap)      // boss asamasi (can esikleri)
    val hitCd = FloatArray(cap * weaponSlots)
    private var nextUid = 1

    fun ringHit(i: Int, ringId: Int): Boolean {
        val b = i * 4
        return ringHits[b] == ringId || ringHits[b + 1] == ringId || ringHits[b + 2] == ringId || ringHits[b + 3] == ringId
    }

    fun markRing(i: Int, ringId: Int) {
        ringHits[i * 4 + ringNext[i]] = ringId
        ringNext[i] = (ringNext[i] + 1) and 3
    }

    fun spawn(d: EnemyDef, index: Int, px: Float, py: Float, hpMul: Float, dmgMul: Float): Int {
        val i = alloc()
        if (i < 0) return -1
        uid[i] = nextUid++
        def[i] = d
        defIndex[i] = index
        x[i] = px; y[i] = py
        kbx[i] = 0f; kby[i] = 0f
        hp[i] = d.hp * hpMul; maxHp[i] = hp[i]
        radius[i] = d.radius
        speed[i] = d.speed
        damage[i] = d.damage * dmgMul
        scale[i] = d.drawScale
        elite[i] = false
        state[i] = EState.NORMAL
        timer[i] = 0f; timer2[i] = 0f
        ax[i] = 0f; ay[i] = 0f
        flash[i] = -1f; slow[i] = 0f; stun[i] = 0f
        anim[i] = (uid[i] % 7) * 0.13f
        facing[i] = 1
        for (k in 0 until 4) ringHits[i * 4 + k] = 0
        ringNext[i] = 0
        phase[i] = 0
        val base = i * weaponSlots
        for (k in 0 until weaponSlots) hitCd[base + k] = 0f
        return i
    }
}

object PKind {
    const val SPARK = 0
    const val MOTH = 1
    const val DAGGER = 2
    const val SICKLE = 3
    const val ENEMY_BOLT = 10
}

object PMove {
    const val STRAIGHT = 0
    const val ORBIT = 1
    const val BOOMERANG = 2
    const val SPIRAL = 3
}

class ProjectilePool(cap: Int) : SlotPool(cap) {
    val kind = IntArray(cap)
    val move = IntArray(cap)
    val x = FloatArray(cap)
    val y = FloatArray(cap)
    val vx = FloatArray(cap)
    val vy = FloatArray(cap)
    val angle = FloatArray(cap)     // yorunge acisi
    val angVel = FloatArray(cap)
    val orbitR = FloatArray(cap)
    val radius = FloatArray(cap)
    val damage = FloatArray(cap)
    val pierce = IntArray(cap)
    val life = FloatArray(cap)
    val maxLife = FloatArray(cap)
    val knock = FloatArray(cap)
    val slot = IntArray(cap)        // sahibi silahin yuvasi
    val rot = FloatArray(cap)       // cizim donusu
    val spin = FloatArray(cap)
    val scale = FloatArray(cap)
    val evolved = BooleanArray(cap)
    val returning = BooleanArray(cap)
    val sprite = arrayOfNulls<String>(cap)
    /** >0 ise isabet listesi bu araliklarla temizlenir (yorungedeki guveler ayni dusmana tekrar vurur). */
    val rehit = FloatArray(cap)
    val rehitTimer = FloatArray(cap)
    val hits = IntArray(cap * MAX_HITS)
    val hitCount = IntArray(cap)

    fun spawn(k: Int, m: Int, px: Float, py: Float): Int {
        val i = alloc()
        if (i < 0) return -1
        kind[i] = k; move[i] = m
        x[i] = px; y[i] = py
        vx[i] = 0f; vy[i] = 0f
        angle[i] = 0f; angVel[i] = 0f; orbitR[i] = 0f
        radius[i] = 3f; damage[i] = 0f; pierce[i] = 1
        life[i] = 1f; maxLife[i] = 1f; knock[i] = 0f
        slot[i] = -1; rot[i] = 0f; spin[i] = 0f; scale[i] = 1f
        evolved[i] = false; returning[i] = false
        sprite[i] = null
        rehit[i] = 0f; rehitTimer[i] = 0f
        hitCount[i] = 0
        return i
    }

    fun hasHit(i: Int, enemyUid: Int): Boolean {
        val base = i * MAX_HITS
        val n = minOf(hitCount[i], MAX_HITS)
        for (k in 0 until n) if (hits[base + k] == enemyUid) return true
        return false
    }

    fun markHit(i: Int, enemyUid: Int) {
        hits[i * MAX_HITS + (hitCount[i] % MAX_HITS)] = enemyUid
        hitCount[i]++
    }

    fun clearHits(i: Int) {
        hitCount[i] = 0
    }

    companion object {
        const val MAX_HITS = 24
    }
}

object Pick {
    const val EMBER_S = 0
    const val EMBER_M = 1
    const val EMBER_L = 2
    const val COIN = 3
    const val CHEST = 4
    const val SIMIT = 5
    const val MAGNET = 6
    const val FLARE = 7
    const val OIL = 8
}

class PickupPool(cap: Int) : SlotPool(cap) {
    val kind = IntArray(cap)
    val x = FloatArray(cap)
    val y = FloatArray(cap)
    val value = IntArray(cap)
    val pulled = BooleanArray(cap)
    val speed = FloatArray(cap)
    val age = FloatArray(cap)

    fun spawn(k: Int, px: Float, py: Float, v: Int): Int {
        val i = alloc()
        if (i < 0) return -1
        kind[i] = k; x[i] = px; y[i] = py; value[i] = v
        pulled[i] = false; speed[i] = 0f; age[i] = 0f
        return i
    }
}

object AKind {
    const val FLAME = 0
    const val BELL_RING = 1
    const val BOSS_WARN = 2
}

/** Zemin etkileri: alev izi, can dalgasi halkasi. */
class AreaPool(cap: Int) : SlotPool(cap) {
    val kind = IntArray(cap)
    val x = FloatArray(cap)
    val y = FloatArray(cap)
    val radius = FloatArray(cap)
    val maxRadius = FloatArray(cap)
    val life = FloatArray(cap)
    val maxLife = FloatArray(cap)
    val damage = FloatArray(cap)
    val tick = FloatArray(cap)
    val slot = IntArray(cap)
    val id = IntArray(cap)
    val evolved = BooleanArray(cap)
    val knock = FloatArray(cap)
}

/** Yalnizca gorsel: simsek parcalari. */
class BeamPool(cap: Int) : SlotPool(cap) {
    val x1 = FloatArray(cap)
    val y1 = FloatArray(cap)
    val x2 = FloatArray(cap)
    val y2 = FloatArray(cap)
    val life = FloatArray(cap)
    val evolved = BooleanArray(cap)
    val seed = IntArray(cap)
}

/**
 * Sabit hucreli uzamsal karma. Her tick dusmanlar yeniden eklenir; sorgu
 * yaricapin kapsadigi hucreleri gezer. Dunya sinirsiz oldugu icin hucre
 * koordinatlari karma ile sabit sayida kovaya katlanir.
 */
class SpatialHash(cap: Int, val cell: Float = 32f) {
    private val buckets = 4096
    private val mask = buckets - 1
    private val head = IntArray(buckets) { -1 }
    private val next = IntArray(cap)
    private val stamp = IntArray(buckets)
    private var epoch = 1

    fun clear() {
        epoch++
    }

    private fun key(cx: Int, cy: Int): Int = ((cx * 73856093) xor (cy * 19349663)) and mask

    fun insert(i: Int, x: Float, y: Float) {
        val k = key(Math.floorDiv(x.toInt(), cell.toInt()), Math.floorDiv(y.toInt(), cell.toInt()))
        if (stamp[k] != epoch) {
            stamp[k] = epoch
            head[k] = -1
        }
        next[i] = head[k]
        head[k] = i
    }

    /** Yaricap icindeki hucrelerde bulunan her slot icin [fn] cagrilir (mesafe filtresi cagirana ait). */
    inline fun query(x: Float, y: Float, r: Float, fn: (Int) -> Unit) {
        val c = cell.toInt()
        val x0 = Math.floorDiv((x - r).toInt(), c)
        val x1 = Math.floorDiv((x + r).toInt(), c)
        val y0 = Math.floorDiv((y - r).toInt(), c)
        val y1 = Math.floorDiv((y + r).toInt(), c)
        for (cy in y0..y1) for (cx in x0..x1) {
            var i = first(cx, cy)
            while (i >= 0) {
                fn(i)
                i = nextOf(i)
            }
        }
    }

    fun first(cx: Int, cy: Int): Int {
        val k = key(cx, cy)
        return if (stamp[k] == epoch) head[k] else -1
    }

    fun nextOf(i: Int): Int = next[i]
}

object Ev {
    const val HIT = 1           // a = hasar, b = 1 ise kritik
    const val KILL = 2          // b = dusman def indeksi, a = 1 ise elit
    const val PLAYER_HURT = 3   // a = hasar
    const val HEAL = 4          // a = miktar
    const val PICK = 5          // b = Pick.*
    const val LEVEL_UP = 6
    const val CHEST = 7
    const val SFX = 8           // b = Sfx.ordinal, a = perde carpani
    const val SHAKE = 9         // a = siddet
    const val BOSS_WARNING = 10
    const val BOSS_SPAWN = 11   // b = dusman slotu
    const val BOSS_DEATH = 12
    const val FLARE = 13
    const val REVIVE = 14
    const val EVOLVE = 15
    const val DAWN = 16
    const val MINIBOSS = 17
    const val SPLASH = 18       // a = yaricap (gorsel halka)
}

/** Simulasyondan cizim/ses katmanina giden olaylar. Her karede bosaltilir. */
class EventBuffer(val cap: Int = 2048) {
    val type = IntArray(cap)
    val x = FloatArray(cap)
    val y = FloatArray(cap)
    val a = FloatArray(cap)
    val b = IntArray(cap)
    var count = 0

    fun emit(t: Int, px: Float = 0f, py: Float = 0f, av: Float = 0f, bv: Int = 0) {
        if (count >= cap) return
        type[count] = t; x[count] = px; y[count] = py; a[count] = av; b[count] = bv
        count++
    }

    fun clear() {
        count = 0
    }
}

enum class Sfx {
    HIT, ENEMY_DIE, PLAYER_HURT, EMBER, COIN, LEVEL_UP, CHEST, EVOLVE, SPARK, MOTH, BELL, LIGHTNING,
    DAGGER, FLAME, SICKLE, EXPLOSION, BOSS_ROAR, WARNING, UI_CLICK, UI_BACK, HEAL, MAGNET, REVIVE,
    VICTORY, DEFEAT, ENEMY_SHOOT, DAWN;

    val file: String get() = "sfx/${name.lowercase()}.ogg"

    companion object {
        fun of(id: String): Sfx = valueOf(id.uppercase())
    }
}
