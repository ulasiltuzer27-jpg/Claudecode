package com.lastlantern.sim

import com.lastlantern.content.CharacterDef
import com.lastlantern.content.Content
import com.lastlantern.content.EnemyDef
import com.lastlantern.content.StageDef
import com.lastlantern.content.Stats
import com.lastlantern.content.Tier
import kotlin.math.abs
import kotlin.math.exp
import kotlin.math.max
import kotlin.math.min
import kotlin.math.sqrt

class RunConfig(
    val stage: StageDef,
    val character: CharacterDef,
    val metaRanks: Map<String, Int> = emptyMap(),
    val seed: Long = System.nanoTime(),
    val endless: Boolean = false,
    val supporter: Boolean = false,
)

enum class RunState { PLAYING, LEVEL_UP, CHEST, DEAD, VICTORY, DEFEAT }

/**
 * Bir run'in tum durumu ve kurallari. Cizimden ve platformdan bagimsizdir:
 * testler bunu baslarsiz (headless) calistirir, oyun ekrani her karede
 * [update] cagirip [events]'i bosaltir.
 */
class World(val config: RunConfig) {
    val rng = Rng(config.seed)
    val stage: StageDef = config.stage
    val baseStats = Stats()
    val stats = Stats()
    val enemies = EnemyPool(1024, Content.MAX_WEAPONS)
    val shots = ProjectilePool(768)
    val enemyShots = ProjectilePool(320)
    val pickups = PickupPool(900)
    val areas = AreaPool(320)
    val beams = BeamPool(160)
    val grid = SpatialHash(1024)
    val events = EventBuffer()
    val player = Player()
    val weapons = ArrayList<Weapon>()
    val passives = LinkedHashMap<String, Int>()
    val director = Director(this)
    val upgrades = Upgrades(this)

    var time = 0f
        private set
    var state = RunState.PLAYING
    var viewHalfW = 180f
    var viewHalfH = 340f

    var kills = 0
    var eliteKills = 0
    var gold = 0
    var chestsOpened = 0
    var bossSlot = -1
    var bossDefeated = false
    var minibossDefeated = false
    var pendingLevelUps = 0
    var adReviveUsed = false
    var revivesLeft = 0
    var rerollsLeft = 0
    var lightBoost = 0f
    var hitStop = 0f
    var damageTaken = 0f
    var endTimer = -1f
    var nextRingId = 1
    var overflowEmber = -1
    var dawnReached = false
    var evolutions = 0

    val enemyDefs: List<EnemyDef> = Content.enemies.values.toList()
    private val defIndex = HashMap<String, Int>().apply { enemyDefs.forEachIndexed { i, d -> put(d.id, i) } }

    init {
        // Kalici (kamp) yukseltmeleri + karakter
        for (m in Content.meta) {
            val r = config.metaRanks[m.id] ?: 0
            repeat(r) { baseStats.apply(m.perRank.stat, m.perRank.value) }
        }
        for (mod in config.character.mods) baseStats.apply(mod.stat, mod.value)
        if (config.supporter) baseStats.greed += 0.25f
        recomputeStats()
        player.hp = stats.maxHp
        revivesLeft = stats.revivals
        rerollsLeft = stats.rerolls
        addWeapon(config.character.startWeapon)
    }

    fun defIndexOf(id: String) = defIndex.getValue(id)

    // ------------------------------------------------------------- Istatistik
    fun recomputeStats() {
        val oldMax = stats.maxHp
        stats.copyFrom(baseStats)
        for ((id, lvl) in passives) {
            val d = Content.passives.getValue(id)
            repeat(lvl) { for (m in d.perLevel) stats.apply(m.stat, m.value) }
        }
        stats.cooldown = max(0.35f, stats.cooldown)
        stats.moveSpeed = max(0.4f, stats.moveSpeed)
        if (oldMax > 0f && stats.maxHp > oldMax) player.hp += stats.maxHp - oldMax
        player.hp = min(player.hp, stats.maxHp)
    }

    val lightRadius: Float
        get() = 92f * stats.light * (if (lightBoost > 0f) 1.5f else 1f) + dawnFactor * 140f

    /** Silahlarin hedef alabildigi menzil: isik nereye kadar uzaniyorsa. */
    val targetRange: Float get() = lightRadius * 1.15f + 12f

    val magnetRadius: Float get() = 28f * stats.magnet

    /** 0 = gece, 1 = gun dogdu. Son iki dakikada yavasca artar. */
    val dawnFactor: Float
        get() = if (config.endless) 0f else ((time - (Content.RUN_LENGTH - 120f)) / 120f).coerceIn(0f, 1f).let {
            if (dawnReached) 1f else it * 0.55f
        }

    val minute get() = time / 60f

    // ------------------------------------------------------------- Silahlar
    fun addWeapon(id: String): Weapon {
        val w = Weapon.create(this, Content.weapons.getValue(id), weapons.size)
        weapons.add(w)
        return w
    }

    fun ownsWeapon(id: String) = weapons.any { it.def.id == id || Content.weapons[id]?.evolvesTo == it.def.id }

    fun addPassive(id: String) {
        passives[id] = (passives[id] ?: 0) + 1
        recomputeStats()
    }

    // ------------------------------------------------------------- Ana dongu
    fun update(dt: Float, moveX: Float, moveY: Float) {
        events.clear()
        if (state != RunState.PLAYING) return
        if (hitStop > 0f) {
            hitStop -= dt
            return
        }
        time += dt
        if (endTimer >= 0f) {
            endTimer -= dt
            if (endTimer < 0f) {
                state = RunState.VICTORY
                return
            }
        }
        director.update(dt)
        updatePlayer(dt, moveX, moveY)
        updateEnemies(dt)
        for (w in weapons) w.update(dt)
        updateShots(dt)
        updateAreas(dt)
        updateEnemyShots(dt)
        contactDamage()
        updatePickups(dt)
        for (i in 0 until beams.high) if (beams.active[i]) {
            beams.life[i] -= dt
            if (beams.life[i] <= 0f) beams.release(i)
        }
        checkDawn()
        if (state == RunState.PLAYING && pendingLevelUps > 0 && endTimer < 0f) {
            state = RunState.LEVEL_UP
            upgrades.rollLevelUp()
            events.emit(Ev.LEVEL_UP, player.x, player.y)
            events.emit(Ev.SFX, bv = Sfx.LEVEL_UP.ordinal, av = 1f)
        }
    }

    private fun updatePlayer(dt: Float, mx: Float, my: Float) {
        val p = player
        var ix = mx
        var iy = my
        val len = sqrt(ix * ix + iy * iy)
        if (len > 1f) {
            ix /= len; iy /= len
        }
        p.moving = len > 0.05f
        if (p.moving) {
            val l = max(len, 0.0001f)
            p.faceX = mx / l
            p.faceY = my / l
            if (abs(ix) > 0.1f) p.flipX = ix < 0f
        }
        val spd = 72f * stats.moveSpeed
        val nx = p.x + ix * spd * dt
        val ny = p.y + iy * spd * dt
        p.distance += sqrt((nx - p.x) * (nx - p.x) + (ny - p.y) * (ny - p.y))
        p.x = nx
        p.y = ny
        p.anim += dt * (if (p.moving) 1f else 0.5f)
        if (p.iframes > 0f) p.iframes -= dt
        if (p.hurtFlash > 0f) p.hurtFlash -= dt
        if (lightBoost > 0f) lightBoost -= dt
        if (stats.regen > 0f && p.hp < stats.maxHp) p.hp = min(stats.maxHp, p.hp + stats.regen * dt)
    }

    // ------------------------------------------------------------- Dusmanlar
    fun spawnEnemy(id: String, px: Float, py: Float, elite: Boolean = false): Int {
        val d = Content.enemies.getValue(id)
        val t = minute
        val hpMul: Float
        val dmgMul: Float
        when (d.tier) {
            Tier.NORMAL -> {
                hpMul = stage.hpMult * (1f + 0.12f * t) * (if (elite) 8f else 1f) * endlessCurse()
                dmgMul = stage.dmgMult * (1f + 0.04f * t) * (if (elite) 1.5f else 1f) * endlessCurse()
            }
            else -> {
                hpMul = stage.hpMult * endlessCurse()
                dmgMul = stage.dmgMult
            }
        }
        val i = enemies.spawn(d, defIndexOf(id), px, py, hpMul, dmgMul)
        // Gece ilerledikce siradan dusmanlar hizlanir: sonsuza kadar kacmak olmaz
        if (i >= 0 && d.tier == Tier.NORMAL) enemies.speed[i] = d.speed * (1f + 0.025f * t)
        if (i >= 0 && elite) {
            enemies.elite[i] = true
            enemies.scale[i] = 2
            enemies.radius[i] = d.radius * 2f
            enemies.speed[i] = d.speed * 0.85f
        }
        if (i >= 0 && d.tier == Tier.BOSS) {
            bossSlot = i
            events.emit(Ev.BOSS_SPAWN, px, py, bv = i)
            events.emit(Ev.SFX, bv = Sfx.BOSS_ROAR.ordinal, av = 1f)
        }
        if (i >= 0 && d.tier == Tier.MINIBOSS) {
            events.emit(Ev.MINIBOSS, px, py, bv = i)
            events.emit(Ev.SFX, bv = Sfx.BOSS_ROAR.ordinal, av = 1.3f)
        }
        return i
    }

    private fun endlessCurse(): Float = if (config.endless && time > Content.RUN_LENGTH) 1f + (time - Content.RUN_LENGTH) / 120f else 1f

    private fun updateEnemies(dt: Float) {
        val e = enemies
        val px = player.x
        val py = player.y
        val despawn = sqrt(viewHalfW * viewHalfW + viewHalfH * viewHalfH) * 1.7f
        val kbDecay = exp(-9f * dt)
        for (i in 0 until e.high) {
            if (!e.active[i]) continue
            val d = e.def[i]!!
            e.anim[i] += dt
            if (e.flash[i] > 0f) e.flash[i] -= dt
            if (e.slow[i] > 0f) e.slow[i] -= dt
            var dx = px - e.x[i]
            var dy = py - e.y[i]
            val dist = sqrt(dx * dx + dy * dy)
            if (dist > 0.001f) {
                dx /= dist; dy /= dist
            }
            if (d.tier == Tier.NORMAL && e.state[i] != EState.SWARM && dist > despawn) {
                relocate(i)
                continue
            }
            var vx = 0f
            var vy = 0f
            if (e.stun[i] > 0f) {
                e.stun[i] -= dt
            } else {
                val spd = e.speed[i] * (if (e.slow[i] > 0f) 0.55f else 1f)
                when (e.state[i]) {
                    EState.SWARM -> {
                        vx = e.ax[i] * spd * 1.4f
                        vy = e.ay[i] * spd * 1.4f
                        e.timer[i] += dt
                        if (e.timer[i] > 12f) {
                            e.release(i)
                            continue
                        }
                    }
                    else -> when (d.tier) {
                        Tier.NORMAL -> {
                            val v = Behaviors.normal(this, i, d, dt, dx, dy, dist, spd)
                            vx = Behaviors.outX; vy = Behaviors.outY
                            if (!v) continue
                        }
                        else -> {
                            Bosses.update(this, i, d, dt, dx, dy, dist, spd)
                            vx = Behaviors.outX; vy = Behaviors.outY
                        }
                    }
                }
            }
            if (!e.active[i]) continue
            e.x[i] += (vx + e.kbx[i]) * dt
            e.y[i] += (vy + e.kby[i]) * dt
            e.kbx[i] *= kbDecay
            e.kby[i] *= kbDecay
            if (abs(vx) > 1f) e.facing[i] = if (vx < 0f) -1 else 1
            val base = i * enemies.weaponSlots
            for (k in 0 until enemies.weaponSlots) if (e.hitCd[base + k] > 0f) e.hitCd[base + k] -= dt
        }
        // Izgara + ayrisma (kalabalik ust uste binmesin)
        grid.clear()
        for (i in 0 until e.high) if (e.active[i]) grid.insert(i, e.x[i], e.y[i])
        for (i in 0 until e.high) {
            if (!e.active[i] || e.def[i]!!.tier != Tier.NORMAL) continue
            val xi = e.x[i]
            val yi = e.y[i]
            val ri = e.radius[i]
            var pushX = 0f
            var pushY = 0f
            grid.query(xi, yi, ri * 2f) { j ->
                if (j != i && e.active[j]) {
                    val ox = xi - e.x[j]
                    val oy = yi - e.y[j]
                    val rr = ri + e.radius[j]
                    val d2 = ox * ox + oy * oy
                    if (d2 < rr * rr && d2 > 0.0001f) {
                        val dd = sqrt(d2)
                        val overlap = (rr - dd) / dd
                        pushX += ox * overlap
                        pushY += oy * overlap
                    }
                }
            }
            e.x[i] += pushX * 0.35f
            e.y[i] += pushY * 0.35f
        }
    }

    /** Oyuncudan cok uzaklasan dusmani ilerledigi yonun onune tasir (yogunluk korunur). */
    private fun relocate(i: Int) {
        val (sx, sy) = director.spawnPoint(ahead = true)
        enemies.x[i] = sx
        enemies.y[i] = sy
        enemies.kbx[i] = 0f
        enemies.kby[i] = 0f
    }

    // ------------------------------------------------------------- Hasar
    /** Dusmana hasar verir. true donerse dusman oldu. */
    fun hitEnemy(i: Int, rawDamage: Float, fromX: Float, fromY: Float, knock: Float, slot: Int, critBonus: Float = 0f): Boolean {
        val e = enemies
        if (!e.active[i]) return false
        val critChance = 0.03f + 0.1f * (stats.luck - 1f) + critBonus
        val crit = rng.chance(critChance)
        val dmg = rawDamage * (if (crit) 2f else 1f) * (if (e.elite[i]) 1f else 1f)
        e.hp[i] -= dmg
        e.flash[i] = 0.09f
        if (slot >= 0 && slot < weapons.size) weapons[slot].totalDamage += dmg
        val d = e.def[i]!!
        if (knock > 0f) {
            var kx = e.x[i] - fromX
            var ky = e.y[i] - fromY
            val l = sqrt(kx * kx + ky * ky)
            if (l > 0.001f) {
                kx /= l; ky /= l
            } else {
                kx = 0f; ky = -1f
            }
            val f = knock * 38f * (1f - d.knockbackResist) * (if (e.elite[i]) 0.4f else 1f)
            e.kbx[i] += kx * f
            e.kby[i] += ky * f
        }
        events.emit(Ev.HIT, e.x[i], e.y[i] - e.radius[i], dmg, if (crit) 1 else 0)
        if (e.hp[i] <= 0f) {
            killEnemy(i)
            return true
        }
        return false
    }

    fun killEnemy(i: Int, drops: Boolean = true) {
        val e = enemies
        val d = e.def[i]!!
        val ex = e.x[i]
        val ey = e.y[i]
        val elite = e.elite[i]
        kills++
        if (elite) eliteKills++
        events.emit(Ev.KILL, ex, ey, if (elite) 1f else 0f, e.defIndex[i])
        if (drops) {
            when (d.tier) {
                Tier.BOSS -> {
                    bossDefeated = true
                    bossSlot = -1
                    events.emit(Ev.BOSS_DEATH, ex, ey)
                    events.emit(Ev.SFX, bv = Sfx.EXPLOSION.ordinal, av = 0.7f)
                    events.emit(Ev.SHAKE, av = 10f)
                    hitStop = 0.25f
                    for (k in 0 until 12) dropCoin(ex + rng.range(-24f, 24f), ey + rng.range(-24f, 24f), 5)
                    if (!config.endless) win(dawn = false)
                    else pickups.spawn(Pick.CHEST, ex, ey, 0)
                }
                Tier.MINIBOSS -> {
                    minibossDefeated = true
                    events.emit(Ev.SFX, bv = Sfx.EXPLOSION.ordinal, av = 1f)
                    events.emit(Ev.SHAKE, av = 7f)
                    hitStop = 0.12f
                    pickups.spawn(Pick.CHEST, ex, ey, 0)
                    for (k in 0 until 4) dropEmber(ex + rng.range(-20f, 20f), ey + rng.range(-20f, 20f), d.xp / 4)
                    for (k in 0 until 6) dropCoin(ex + rng.range(-20f, 20f), ey + rng.range(-20f, 20f), 3)
                }
                Tier.NORMAL -> {
                    if (elite) {
                        pickups.spawn(Pick.CHEST, ex, ey, 0)
                        dropEmber(ex + 6f, ey, d.xp * 5)
                        if (rng.chance(0.5f)) pickups.spawn(Pick.SIMIT, ex - 8f, ey + 4f, 0)
                    } else {
                        dropEmber(ex, ey, d.xp)
                        if (rng.chance(d.coinChance * stats.luck)) dropCoin(ex + 3f, ey + 2f, 1)
                        val r = rng.nextFloat()
                        val l = stats.luck
                        when {
                            r < 0.0025f * l -> pickups.spawn(Pick.SIMIT, ex, ey, 0)
                            r < 0.0035f * l -> pickups.spawn(Pick.MAGNET, ex, ey, 0)
                            r < 0.0042f * l -> pickups.spawn(Pick.FLARE, ex, ey, 0)
                            r < 0.0056f * l -> pickups.spawn(Pick.OIL, ex, ey, 0)
                        }
                    }
                    val child = d.splitInto
                    if (child != null) {
                        for (k in 0 until 2) {
                            val a = rng.angle()
                            val c = spawnEnemy(child, ex + kotlin.math.cos(a) * 6f, ey + kotlin.math.sin(a) * 6f)
                            if (c >= 0) {
                                enemies.kbx[c] = kotlin.math.cos(a) * 60f
                                enemies.kby[c] = kotlin.math.sin(a) * 60f
                            }
                        }
                    }
                }
            }
        }
        e.release(i)
    }

    fun dropEmber(x: Float, y: Float, value: Int) {
        if (value <= 0) return
        // Yerde cok fazla koz varsa yenileri tek bir buyuk koza eklenir:
        // hem performans hem de ekran okunurlugu icin.
        if (pickups.count > 520) {
            val o = overflowEmber
            if (o >= 0 && pickups.active[o] && pickups.kind[o] == Pick.EMBER_L) {
                pickups.value[o] += value
                return
            }
            overflowEmber = pickups.spawn(Pick.EMBER_L, x, y, value)
            return
        }
        val kind = when {
            value >= 10 -> Pick.EMBER_L
            value >= 3 -> Pick.EMBER_M
            else -> Pick.EMBER_S
        }
        pickups.spawn(kind, x, y, value)
    }

    fun dropCoin(x: Float, y: Float, value: Int) {
        pickups.spawn(Pick.COIN, x, y, value)
    }

    fun hurtPlayer(raw: Float) {
        val p = player
        if (p.iframes > 0f || state != RunState.PLAYING || endTimer >= 0f) return
        val dmg = max(1f, raw - stats.armor)
        p.hp -= dmg
        damageTaken += dmg
        p.iframes = 0.45f
        p.hurtFlash = 0.2f
        events.emit(Ev.PLAYER_HURT, p.x, p.y, dmg)
        events.emit(Ev.SFX, bv = Sfx.PLAYER_HURT.ordinal, av = 1f)
        events.emit(Ev.SHAKE, av = 3f)
        if (p.hp <= 0f) {
            p.hp = 0f
            if (revivesLeft > 0) {
                revivesLeft--
                revive()
            } else {
                state = RunState.DEAD
            }
        }
    }

    fun heal(v: Float) {
        val before = player.hp
        player.hp = min(stats.maxHp, player.hp + v)
        if (player.hp > before) events.emit(Ev.HEAL, player.x, player.y, player.hp - before)
    }

    /** Olumden sonra dirilis (kamp yukseltmesi ya da odullu reklam). */
    fun revive() {
        player.hp = stats.maxHp * 0.5f
        player.iframes = 2.5f
        flare(clearRadius = 160f)
        state = RunState.PLAYING
        events.emit(Ev.REVIVE, player.x, player.y)
        events.emit(Ev.SFX, bv = Sfx.REVIVE.ordinal, av = 1f)
    }

    fun reviveByAd() {
        adReviveUsed = true
        revive()
    }

    fun giveUp() {
        state = RunState.DEFEAT
    }

    /** Ekrandaki siradan dusmanlari yakar, bosslara agir hasar verir. */
    fun flare(clearRadius: Float = 0f) {
        val r = if (clearRadius > 0f) clearRadius else max(viewHalfW, viewHalfH) * 1.1f
        val px = player.x
        val py = player.y
        for (i in 0 until enemies.high) {
            if (!enemies.active[i]) continue
            val dx = enemies.x[i] - px
            val dy = enemies.y[i] - py
            if (dx * dx + dy * dy > r * r) continue
            val d = enemies.def[i]!!
            if (d.tier == Tier.NORMAL && !enemies.elite[i]) killEnemy(i)
            else hitEnemy(i, enemies.maxHp[i] * 0.08f, px, py, 4f, -1)
        }
        events.emit(Ev.FLARE, px, py, r)
        events.emit(Ev.SFX, bv = Sfx.EXPLOSION.ordinal, av = 1f)
        events.emit(Ev.SHAKE, av = 6f)
    }

    // ------------------------------------------------------------- Mermiler
    private fun updateShots(dt: Float) {
        val s = shots
        for (i in 0 until s.high) {
            if (!s.active[i]) continue
            s.life[i] -= dt
            if (s.life[i] <= 0f) {
                s.release(i)
                continue
            }
            if (s.rehit[i] > 0f) {
                s.rehitTimer[i] -= dt
                if (s.rehitTimer[i] <= 0f) {
                    s.rehitTimer[i] = s.rehit[i]
                    s.clearHits(i)
                }
            }
            s.rot[i] += s.spin[i] * dt
            when (s.move[i]) {
                PMove.STRAIGHT -> {
                    s.x[i] += s.vx[i] * dt
                    s.y[i] += s.vy[i] * dt
                }
                PMove.ORBIT -> {
                    s.angle[i] += s.angVel[i] * dt
                    s.x[i] = player.x + kotlin.math.cos(s.angle[i]) * s.orbitR[i]
                    s.y[i] = player.y + kotlin.math.sin(s.angle[i]) * s.orbitR[i]
                    s.rot[i] = s.angle[i]
                }
                PMove.SPIRAL -> {
                    val t = 1f - s.life[i] / s.maxLife[i]
                    s.angle[i] += s.angVel[i] * dt
                    val r = 10f + s.orbitR[i] * t
                    s.x[i] = player.x + kotlin.math.cos(s.angle[i]) * r
                    s.y[i] = player.y + kotlin.math.sin(s.angle[i]) * r
                }
                PMove.BOOMERANG -> {
                    if (!s.returning[i]) {
                        // Hiz, kalan menzile gore azalir; durunca geri doner
                        s.angle[i] -= dt   // angle = donmeye kalan sure
                        if (s.angle[i] <= 0f) {
                            s.returning[i] = true
                            s.clearHits(i)
                        } else {
                            val k = s.angle[i] / s.orbitR[i]
                            s.x[i] += s.vx[i] * k * dt
                            s.y[i] += s.vy[i] * k * dt
                        }
                    }
                    if (s.returning[i]) {
                        var dx = player.x - s.x[i]
                        var dy = player.y - s.y[i]
                        val l = sqrt(dx * dx + dy * dy)
                        if (l < 8f) {
                            s.release(i)
                            continue
                        }
                        dx /= l; dy /= l
                        val sp = sqrt(s.vx[i] * s.vx[i] + s.vy[i] * s.vy[i]) * 1.25f
                        s.x[i] += dx * sp * dt
                        s.y[i] += dy * sp * dt
                        s.life[i] = max(s.life[i], 0.5f)
                    }
                }
            }
            // Isabet
            val r = s.radius[i]
            val sx = s.x[i]
            val sy = s.y[i]
            var dead = false
            grid.query(sx, sy, r + 24f) { j ->
                if (!dead && enemies.active[j] && !s.hasHit(i, enemies.uid[j])) {
                    val dx = enemies.x[j] - sx
                    val dy = enemies.y[j] - sy
                    val rr = r + enemies.radius[j]
                    if (dx * dx + dy * dy < rr * rr) {
                        s.markHit(i, enemies.uid[j])
                        val crit = if (s.kind[i] == PKind.DAGGER && s.evolved[i]) 0.22f else 0f
                        hitEnemy(j, s.damage[i], sx - s.vx[i] * 0.05f, sy - s.vy[i] * 0.05f, s.knock[i], s.slot[i], crit)
                        if (s.move[i] == PMove.STRAIGHT) {
                            s.pierce[i]--
                            if (s.pierce[i] <= 0) dead = true
                        }
                    }
                }
            }
            if (dead) s.release(i)
        }
    }

    private fun updateEnemyShots(dt: Float) {
        val s = enemyShots
        val p = player
        for (i in 0 until s.high) {
            if (!s.active[i]) continue
            s.life[i] -= dt
            if (s.life[i] <= 0f) {
                s.release(i)
                continue
            }
            s.x[i] += s.vx[i] * dt
            s.y[i] += s.vy[i] * dt
            s.rot[i] += s.spin[i] * dt
            val dx = p.x - s.x[i]
            val dy = p.y - s.y[i]
            val rr = s.radius[i] + p.radius - 1f
            if (dx * dx + dy * dy < rr * rr) {
                hurtPlayer(s.damage[i])
                s.release(i)
            }
        }
    }

    fun enemyShoot(fromX: Float, fromY: Float, dirX: Float, dirY: Float, speed: Float, damage: Float, sprite: String) {
        val i = enemyShots.spawn(PKind.ENEMY_BOLT, PMove.STRAIGHT, fromX, fromY)
        if (i < 0) return
        enemyShots.vx[i] = dirX * speed
        enemyShots.vy[i] = dirY * speed
        enemyShots.damage[i] = damage
        enemyShots.radius[i] = 3f
        enemyShots.life[i] = 5f
        enemyShots.sprite[i] = sprite
        enemyShots.spin[i] = 6f
    }

    // ------------------------------------------------------------- Alanlar
    private fun updateAreas(dt: Float) {
        val a = areas
        for (i in 0 until a.high) {
            if (!a.active[i]) continue
            a.life[i] -= dt
            if (a.life[i] <= 0f) {
                a.release(i)
                continue
            }
            when (a.kind[i]) {
                AKind.FLAME -> {
                    a.tick[i] -= dt
                    if (a.tick[i] <= 0f) {
                        a.tick[i] = 0.35f
                        val r = a.radius[i]
                        val ax = a.x[i]
                        val ay = a.y[i]
                        val evo = a.evolved[i]
                        grid.query(ax, ay, r + 24f) { j ->
                            if (enemies.active[j]) {
                                val dx = enemies.x[j] - ax
                                val dy = enemies.y[j] - ay
                                val rr = r + enemies.radius[j]
                                if (dx * dx + dy * dy < rr * rr) {
                                    if (evo) enemies.slow[j] = 0.6f
                                    hitEnemy(j, a.damage[i], ax, ay, 0f, a.slot[i])
                                }
                            }
                        }
                    }
                }
                AKind.BELL_RING -> {
                    val prevR = a.radius[i]
                    a.radius[i] = min(a.maxRadius[i], prevR + a.tick[i] * dt)
                    val r = a.radius[i]
                    val ax = a.x[i]
                    val ay = a.y[i]
                    val ringId = a.id[i]
                    val stun = a.evolved[i]
                    grid.query(ax, ay, r + 24f) { j ->
                        if (enemies.active[j] && enemies.lastRing[j] != ringId) {
                            val dx = enemies.x[j] - ax
                            val dy = enemies.y[j] - ay
                            val d = sqrt(dx * dx + dy * dy)
                            if (d < r + enemies.radius[j]) {
                                enemies.lastRing[j] = ringId
                                if (stun && enemies.def[j]!!.tier == Tier.NORMAL) enemies.stun[j] = 0.9f
                                hitEnemy(j, a.damage[i], ax, ay, a.knock[i], a.slot[i])
                            }
                        }
                    }
                    if (r >= a.maxRadius[i]) a.life[i] = min(a.life[i], 0.12f)
                }
            }
        }
    }

    private fun contactDamage() {
        val p = player
        if (p.iframes > 0f) return
        var worst = 0f
        grid.query(p.x, p.y, p.radius + 48f) { j ->
            if (enemies.active[j] && enemies.stun[j] <= 0f) {
                val dx = enemies.x[j] - p.x
                val dy = enemies.y[j] - p.y
                val rr = p.radius + enemies.radius[j] - 2f
                if (dx * dx + dy * dy < rr * rr) worst = max(worst, enemies.damage[j])
            }
        }
        if (worst > 0f) hurtPlayer(worst)
    }

    // ------------------------------------------------------------- Toplama
    private fun updatePickups(dt: Float) {
        val pk = pickups
        val p = player
        val mr = magnetRadius
        for (i in 0 until pk.high) {
            if (!pk.active[i]) continue
            pk.age[i] += dt
            val dx = p.x - pk.x[i]
            val dy = p.y - pk.y[i]
            val d2 = dx * dx + dy * dy
            val k = pk.kind[i]
            val magnetic = k != Pick.CHEST
            if (!pk.pulled[i] && magnetic && d2 < mr * mr) pk.pulled[i] = true
            if (pk.pulled[i]) {
                val d = sqrt(d2)
                pk.speed[i] = min(420f, pk.speed[i] + 520f * dt)
                if (d > 0.01f) {
                    pk.x[i] += dx / d * pk.speed[i] * dt
                    pk.y[i] += dy / d * pk.speed[i] * dt
                }
            }
            val reach = if (k == Pick.CHEST) 12f else 8f
            if (d2 < reach * reach) collect(i)
        }
    }

    private fun collect(i: Int) {
        val pk = pickups
        val k = pk.kind[i]
        val v = pk.value[i]
        val x = pk.x[i]
        val y = pk.y[i]
        pk.release(i)
        if (i == overflowEmber) overflowEmber = -1
        events.emit(Ev.PICK, x, y, v.toFloat(), k)
        when (k) {
            Pick.EMBER_S, Pick.EMBER_M, Pick.EMBER_L -> {
                gainXp(v.toFloat())
                events.emit(Ev.SFX, bv = Sfx.EMBER.ordinal, av = 1f + (player.level % 8) * 0.03f)
            }
            Pick.COIN -> {
                gold += max(1, Math.round(v * stats.greed))
                events.emit(Ev.SFX, bv = Sfx.COIN.ordinal, av = 1f)
            }
            Pick.CHEST -> {
                chestsOpened++
                upgrades.rollChest()
                state = RunState.CHEST
                events.emit(Ev.CHEST, x, y)
                events.emit(Ev.SFX, bv = Sfx.CHEST.ordinal, av = 1f)
            }
            Pick.SIMIT -> {
                heal(30f)
                events.emit(Ev.SFX, bv = Sfx.HEAL.ordinal, av = 1f)
            }
            Pick.MAGNET -> {
                for (j in 0 until pk.high) if (pk.active[j] && pk.kind[j] <= Pick.EMBER_L) pk.pulled[j] = true
                events.emit(Ev.SFX, bv = Sfx.MAGNET.ordinal, av = 1f)
            }
            Pick.FLARE -> flare()
            Pick.OIL -> {
                lightBoost = 15f
                events.emit(Ev.SFX, bv = Sfx.HEAL.ordinal, av = 0.8f)
            }
        }
    }

    fun gainXp(v: Float) {
        val p = player
        p.xp += v * stats.growth
        while (p.xp >= p.xpToNext) {
            p.xp -= p.xpToNext
            p.level++
            p.xpToNext = Content.xpForLevel(p.level)
            pendingLevelUps++
        }
    }

    // ------------------------------------------------------------- Bitis
    private fun checkDawn() {
        if (config.endless || endTimer >= 0f) return
        if (time >= Content.RUN_LENGTH) {
            dawnReached = true
            // Gunes dogar: geceye ait her sey yanar
            for (i in 0 until enemies.high) if (enemies.active[i]) killEnemy(i, drops = enemies.def[i]!!.tier == Tier.NORMAL)
            events.emit(Ev.DAWN, player.x, player.y)
            events.emit(Ev.SFX, bv = Sfx.DAWN.ordinal, av = 1f)
            win(dawn = true)
        }
    }

    private fun win(dawn: Boolean) {
        if (endTimer >= 0f) return
        dawnReached = true
        endTimer = if (dawn) 3f else 2.5f
        player.iframes = 99f
        enemyShots.clear()
    }

    /** Level-up ya da sandik ekrani kapandiginda. */
    fun resume() {
        if (state == RunState.LEVEL_UP || state == RunState.CHEST) state = RunState.PLAYING
    }

    val victory get() = state == RunState.VICTORY
}
