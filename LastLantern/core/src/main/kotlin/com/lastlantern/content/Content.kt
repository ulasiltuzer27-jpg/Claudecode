package com.lastlantern.content

/*
 * Oyunun butun icerik tanimlari. Tip guvenli Kotlin: bir silah ya da dusman
 * adi yanlis yazilirsa derleme degil ama ContentTest bunu yakalar (her referans
 * gercek bir tanima cozulmeli).
 *
 * Denge birimleri: mesafe = sanal piksel (ekran 360 piksel genisliginde),
 * zaman = saniye, hiz = piksel/sn.
 */

enum class Behavior { CHASE, ERRATIC, RANGED, DASH, MOTH_MATRON, MIRE_MOTHER, FROST_ALPHA, STAG, BELLKEEPER, WYRM }

enum class Tier { NORMAL, MINIBOSS, BOSS }

class EnemyDef(
    val id: String,
    val sprite: String,
    val hp: Float,
    val speed: Float,
    val damage: Float,
    val radius: Float,
    val xp: Int,
    val behavior: Behavior,
    val eyeColor: Int,
    val knockbackResist: Float = 0f,
    val drawScale: Int = 1,
    val frameTime: Float = 0.28f,
    val splitInto: String? = null,
    val shotInterval: Float = 0f,
    val shotSpeed: Float = 0f,
    val shotDamage: Float = 0f,
    val shotSprite: String = "proj_bolt",
    val tier: Tier = Tier.NORMAL,
    val coinChance: Float = 0.02f,
)

enum class WeaponKind { SPARK, MOTHS, RING, CHAIN, BELL, DAGGER, FLAME, SICKLE }

/** Bir silah seviyesinin (ya da seviye artisinin) istatistikleri. */
class WStats(
    val damage: Float = 0f,
    val cooldown: Float = 0f,
    val amount: Int = 0,
    val pierce: Int = 0,
    val area: Float = 0f,
    val speed: Float = 0f,
    val duration: Float = 0f,
    val knockback: Float = 0f,
)

class WeaponDef(
    val id: String,
    val icon: String,
    val kind: WeaponKind,
    val base: WStats,
    /** levels[i] = seviye (i+2)'ye cikarken eklenen fark. */
    val levels: List<WStats>,
    val evolvePassive: String? = null,
    val evolvesTo: String? = null,
    val evolved: Boolean = false,
    val sfx: String,
) {
    val maxLevel get() = levels.size + 1
}

class PassiveDef(val id: String, val icon: String, val perLevel: List<StatMod>, val maxLevel: Int = 5)

class CharacterDef(
    val id: String,
    val sprite: String,
    val startWeapon: String,
    val mods: List<StatMod>,
    val price: Int,
    val unlockAchievement: String?,
)

class MetaDef(val id: String, val icon: String, val maxRank: Int, val baseCost: Int, val perRank: StatMod) {
    fun cost(rank: Int): Int = (baseCost * (1f + rank * 0.9f)).toInt() / 5 * 5
}

class Wave(val start: Float, val interval: Float, val batch: Int, val maxAlive: Int, val mix: List<Pair<String, Int>>)

enum class EventType { RING, SWARM, ELITE, MINIBOSS, BOSS }

class StageEvent(val time: Float, val type: EventType, val enemy: String, val count: Int = 1)

class StageDef(
    val id: String,
    val index: Int,
    val tileset: String,
    val decals: List<String>,
    val props: List<String>,
    val music: String,
    val hpMult: Float,
    val dmgMult: Float,
    val waves: List<Wave>,
    val events: List<StageEvent>,
    val enemyTint: Int = 0xFFFFFFFF.toInt(),
    val ambient: Int,
    val dawnAmbient: Int,
)

object Content {
    const val RUN_LENGTH = 600f     // 10 dk: gunes dogar
    const val BOSS_TIME = 540f      // 9. dakika: safak bossu
    const val MAX_WEAPONS = 6
    const val MAX_PASSIVES = 6

    // ---------------------------------------------------------------- Dusmanlar
    val enemies: Map<String, EnemyDef> = listOf(
        EnemyDef("shade", "enemy_shade", hp = 10f, speed = 30f, damage = 6f, radius = 5f, xp = 1,
            behavior = Behavior.CHASE, eyeColor = 0xFFD45AFF.toInt()),
        EnemyDef("bat", "enemy_bat", hp = 6f, speed = 52f, damage = 4f, radius = 4f, xp = 1,
            behavior = Behavior.ERRATIC, eyeColor = 0xFF5A4AFF.toInt(), frameTime = 0.12f),
        EnemyDef("husk", "enemy_husk", hp = 40f, speed = 19f, damage = 10f, radius = 6f, xp = 3,
            behavior = Behavior.CHASE, eyeColor = 0xFF9A2AFF.toInt(), knockbackResist = 0.6f, frameTime = 0.4f,
            coinChance = 0.05f),
        EnemyDef("wisp", "enemy_wisp", hp = 14f, speed = 26f, damage = 5f, radius = 4f, xp = 2,
            behavior = Behavior.RANGED, eyeColor = 0xFFFFFFFF.toInt(), frameTime = 0.18f,
            shotInterval = 2.8f, shotSpeed = 70f, shotDamage = 6f),
        EnemyDef("sporeling", "enemy_sporeling", hp = 16f, speed = 30f, damage = 6f, radius = 5f, xp = 1,
            behavior = Behavior.CHASE, eyeColor = 0xFFD45AFF.toInt(), splitInto = "sporelet"),
        EnemyDef("sporelet", "enemy_sporeling", hp = 5f, speed = 40f, damage = 4f, radius = 4f, xp = 1,
            behavior = Behavior.ERRATIC, eyeColor = 0xFFD45AFF.toInt(), frameTime = 0.16f, coinChance = 0f),
        EnemyDef("drowned", "enemy_drowned", hp = 28f, speed = 28f, damage = 9f, radius = 6f, xp = 2,
            behavior = Behavior.CHASE, eyeColor = 0xD9FF6AFF.toInt(), frameTime = 0.35f, coinChance = 0.03f),
        EnemyDef("slime", "enemy_slime", hp = 22f, speed = 22f, damage = 7f, radius = 5f, xp = 2,
            behavior = Behavior.CHASE, eyeColor = 0xA3D96AFF.toInt(), splitInto = "slimelet"),
        EnemyDef("slimelet", "enemy_slime", hp = 7f, speed = 30f, damage = 4f, radius = 4f, xp = 1,
            behavior = Behavior.ERRATIC, eyeColor = 0xA3D96AFF.toInt(), frameTime = 0.2f, coinChance = 0f),
        EnemyDef("ghostlamp", "enemy_ghostlamp", hp = 20f, speed = 24f, damage = 6f, radius = 5f, xp = 2,
            behavior = Behavior.RANGED, eyeColor = 0x5FD3E0FF.toInt(),
            shotInterval = 2.4f, shotSpeed = 80f, shotDamage = 7f),
        EnemyDef("wolf", "enemy_wolf", hp = 30f, speed = 42f, damage = 10f, radius = 6f, xp = 3,
            behavior = Behavior.DASH, eyeColor = 0x5FD3E0FF.toInt(), frameTime = 0.14f, coinChance = 0.04f),
        EnemyDef("golem", "enemy_golem", hp = 80f, speed = 15f, damage = 14f, radius = 7f, xp = 5,
            behavior = Behavior.CHASE, eyeColor = 0x5FD3E0FF.toInt(), knockbackResist = 0.85f, frameTime = 0.45f,
            coinChance = 0.08f),
        // Ara bosslar (24 px, 2x cizilir)
        EnemyDef("moth_matron", "miniboss_moth_matron", hp = 900f, speed = 34f, damage = 14f, radius = 16f, xp = 60,
            behavior = Behavior.MOTH_MATRON, eyeColor = 0xFFD45AFF.toInt(), knockbackResist = 0.9f, drawScale = 2,
            frameTime = 0.15f, tier = Tier.MINIBOSS, coinChance = 0f),
        EnemyDef("mire_mother", "miniboss_mire_mother", hp = 1300f, speed = 22f, damage = 16f, radius = 18f, xp = 80,
            behavior = Behavior.MIRE_MOTHER, eyeColor = 0xFF5A4AFF.toInt(), knockbackResist = 0.95f, drawScale = 2,
            frameTime = 0.4f, tier = Tier.MINIBOSS, coinChance = 0f),
        EnemyDef("frost_alpha", "miniboss_frost_alpha", hp = 1500f, speed = 40f, damage = 18f, radius = 16f, xp = 90,
            behavior = Behavior.FROST_ALPHA, eyeColor = 0xFF5A4AFF.toInt(), knockbackResist = 0.9f, drawScale = 2,
            frameTime = 0.16f, tier = Tier.MINIBOSS, coinChance = 0f),
        // Safak bosslari (32 px, 2x cizilir)
        EnemyDef("hollow_stag", "boss_hollow_stag", hp = 3400f, speed = 30f, damage = 20f, radius = 22f, xp = 0,
            behavior = Behavior.STAG, eyeColor = 0xFF8A2AFF.toInt(), knockbackResist = 1f, drawScale = 2,
            frameTime = 0.3f, tier = Tier.BOSS, coinChance = 0f, shotSpeed = 90f, shotDamage = 12f),
        EnemyDef("bellkeeper", "boss_bellkeeper", hp = 4200f, speed = 20f, damage = 24f, radius = 22f, xp = 0,
            behavior = Behavior.BELLKEEPER, eyeColor = 0xD9FF6AFF.toInt(), knockbackResist = 1f, drawScale = 2,
            frameTime = 0.45f, tier = Tier.BOSS, coinChance = 0f, shotSpeed = 75f, shotDamage = 14f),
        EnemyDef("frost_wyrm", "boss_frost_wyrm", hp = 5000f, speed = 36f, damage = 26f, radius = 22f, xp = 0,
            behavior = Behavior.WYRM, eyeColor = 0xFF5A4AFF.toInt(), knockbackResist = 1f, drawScale = 2,
            frameTime = 0.25f, tier = Tier.BOSS, coinChance = 0f, shotSpeed = 110f, shotDamage = 14f,
            shotSprite = "proj_icebolt"),
    ).associateBy { it.id }

    // ---------------------------------------------------------------- Silahlar
    val weapons: Map<String, WeaponDef> = listOf(
        WeaponDef("spark", "icon_w_spark", WeaponKind.SPARK,
            WStats(damage = 10f, cooldown = 1.2f, amount = 1, pierce = 1, area = 1f, speed = 190f, duration = 1.6f, knockback = 1f),
            listOf(WStats(amount = 1), WStats(damage = 5f), WStats(amount = 1, cooldown = -0.1f),
                WStats(pierce = 1), WStats(damage = 6f, amount = 1)),
            evolvePassive = "oil", evolvesTo = "solar", sfx = "spark"),
        WeaponDef("moths", "icon_w_moths", WeaponKind.MOTHS,
            WStats(damage = 9f, cooldown = 3.6f, amount = 2, pierce = 99, area = 1f, speed = 3.2f, duration = 3.2f, knockback = 0.5f),
            listOf(WStats(amount = 1), WStats(damage = 4f, speed = 0.4f), WStats(duration = 0.6f),
                WStats(amount = 1), WStats(damage = 5f, area = 0.25f)),
            evolvePassive = "swift", evolvesTo = "phoenix", sfx = "moth"),
        WeaponDef("ring", "icon_w_ring", WeaponKind.RING,
            WStats(damage = 5f, cooldown = 0.55f, area = 1f, knockback = 0.3f),
            listOf(WStats(area = 0.2f), WStats(damage = 3f), WStats(cooldown = -0.08f),
                WStats(area = 0.2f, damage = 2f), WStats(damage = 4f)),
            evolvePassive = "vital", evolvesTo = "halo", sfx = "flame"),
        WeaponDef("chain", "icon_w_chain", WeaponKind.CHAIN,
            WStats(damage = 16f, cooldown = 2.4f, amount = 1, pierce = 2, area = 1f),
            listOf(WStats(pierce = 1), WStats(damage = 6f), WStats(amount = 1),
                WStats(cooldown = -0.3f, pierce = 1), WStats(damage = 8f)),
            evolvePassive = "haste", evolvesTo = "storm", sfx = "lightning"),
        WeaponDef("bell", "icon_w_bell", WeaponKind.BELL,
            WStats(damage = 13f, cooldown = 3.2f, amount = 1, area = 1f, speed = 160f, knockback = 6f),
            listOf(WStats(area = 0.2f), WStats(damage = 6f), WStats(cooldown = -0.4f),
                WStats(amount = 1), WStats(damage = 8f, area = 0.2f)),
            evolvePassive = "armor", evolvesTo = "dawnbell", sfx = "bell"),
        WeaponDef("dagger", "icon_w_dagger", WeaponKind.DAGGER,
            WStats(damage = 7f, cooldown = 0.75f, amount = 1, pierce = 1, area = 1f, speed = 280f, duration = 1f, knockback = 0.6f),
            listOf(WStats(amount = 1), WStats(damage = 3f), WStats(amount = 1, cooldown = -0.1f),
                WStats(pierce = 1), WStats(damage = 4f, amount = 1)),
            evolvePassive = "luck", evolvesTo = "starknife", sfx = "dagger"),
        WeaponDef("flame", "icon_w_flame", WeaponKind.FLAME,
            WStats(damage = 5f, cooldown = 0.32f, area = 1f, duration = 1.8f),
            listOf(WStats(duration = 0.5f), WStats(damage = 2f), WStats(area = 0.25f),
                WStats(cooldown = -0.06f, damage = 2f), WStats(duration = 0.6f, area = 0.2f)),
            evolvePassive = "reach", evolvesTo = "inferno", sfx = "flame"),
        WeaponDef("sickle", "icon_w_sickle", WeaponKind.SICKLE,
            WStats(damage = 14f, cooldown = 2.2f, amount = 1, pierce = 99, area = 1f, speed = 200f, duration = 1f, knockback = 2f),
            listOf(WStats(amount = 1), WStats(damage = 6f), WStats(area = 0.25f),
                WStats(amount = 1, cooldown = -0.2f), WStats(damage = 8f)),
            evolvePassive = "might", evolvesTo = "reaper", sfx = "sickle"),
        // Evrimler: tek seviye, kendi temel istatistikleri
        WeaponDef("solar", "icon_e_solar", WeaponKind.SPARK,
            WStats(damage = 26f, cooldown = 0.8f, amount = 4, pierce = 99, area = 1.5f, speed = 210f, duration = 1.8f, knockback = 2f),
            emptyList(), evolved = true, sfx = "spark"),
        WeaponDef("phoenix", "icon_e_phoenix", WeaponKind.MOTHS,
            WStats(damage = 22f, cooldown = 0f, amount = 6, pierce = 99, area = 1.35f, speed = 4.2f, duration = 0f, knockback = 1f),
            emptyList(), evolved = true, sfx = "moth"),
        WeaponDef("halo", "icon_e_halo", WeaponKind.RING,
            WStats(damage = 16f, cooldown = 0.4f, area = 1.9f, knockback = 0.6f),
            emptyList(), evolved = true, sfx = "flame"),
        WeaponDef("storm", "icon_e_storm", WeaponKind.CHAIN,
            WStats(damage = 30f, cooldown = 1.1f, amount = 3, pierce = 6, area = 1.4f),
            emptyList(), evolved = true, sfx = "lightning"),
        WeaponDef("dawnbell", "icon_e_dawnbell", WeaponKind.BELL,
            WStats(damage = 34f, cooldown = 2.4f, amount = 2, area = 1.9f, speed = 200f, knockback = 8f),
            emptyList(), evolved = true, sfx = "bell"),
        WeaponDef("starknife", "icon_e_starknife", WeaponKind.DAGGER,
            WStats(damage = 16f, cooldown = 0.35f, amount = 4, pierce = 3, area = 1f, speed = 320f, duration = 1f, knockback = 0.8f),
            emptyList(), evolved = true, sfx = "dagger"),
        WeaponDef("inferno", "icon_e_inferno", WeaponKind.FLAME,
            WStats(damage = 12f, cooldown = 0.2f, area = 1.6f, duration = 3f),
            emptyList(), evolved = true, sfx = "flame"),
        WeaponDef("reaper", "icon_e_reaper", WeaponKind.SICKLE,
            WStats(damage = 34f, cooldown = 1.6f, amount = 4, pierce = 99, area = 1.6f, speed = 220f, duration = 1.2f, knockback = 3f),
            emptyList(), evolved = true, sfx = "sickle"),
    ).associateBy { it.id }

    val baseWeapons = listOf("spark", "moths", "ring", "chain", "bell", "dagger", "flame", "sickle")

    // ---------------------------------------------------------------- Pasifler
    val passives: Map<String, PassiveDef> = listOf(
        PassiveDef("might", "icon_p_might", listOf(StatMod(Stat.MIGHT, 0.10f))),
        PassiveDef("swift", "icon_p_swift", listOf(StatMod(Stat.MOVE_SPEED, 0.08f))),
        PassiveDef("haste", "icon_p_haste", listOf(StatMod(Stat.COOLDOWN, 0.07f))),
        PassiveDef("reach", "icon_p_reach", listOf(StatMod(Stat.AREA, 0.10f))),
        PassiveDef("magnet", "icon_p_magnet", listOf(StatMod(Stat.MAGNET, 0.35f))),
        PassiveDef("vital", "icon_p_vital", listOf(StatMod(Stat.MAX_HP, 20f))),
        PassiveDef("armor", "icon_p_armor", listOf(StatMod(Stat.ARMOR, 1f))),
        PassiveDef("luck", "icon_p_luck", listOf(StatMod(Stat.LUCK, 0.12f))),
        PassiveDef("oil", "icon_p_oil", listOf(StatMod(Stat.LIGHT, 0.15f), StatMod(Stat.DURATION, 0.06f))),
        PassiveDef("recover", "icon_p_recover", listOf(StatMod(Stat.REGEN, 0.25f))),
    ).associateBy { it.id }

    // ---------------------------------------------------------------- Karakterler
    val characters: List<CharacterDef> = listOf(
        CharacterDef("keeper", "char_keeper", "spark", listOf(StatMod(Stat.LIGHT, 0.10f)), price = 0, unlockAchievement = null),
        CharacterDef("monk", "char_monk", "bell",
            listOf(StatMod(Stat.MAX_HP, 30f), StatMod(Stat.ARMOR, 1f), StatMod(Stat.MOVE_SPEED, -0.05f)),
            price = 600, unlockAchievement = "survive_5"),
        CharacterDef("witch", "char_witch", "moths", listOf(StatMod(Stat.AREA, 0.15f)),
            price = 900, unlockAchievement = "level_20"),
        CharacterDef("tinker", "char_tinker", "dagger", listOf(StatMod(Stat.COOLDOWN, 0.08f)),
            price = 1200, unlockAchievement = "kills_2500"),
    )

    fun character(id: String) = characters.first { it.id == id }

    // ---------------------------------------------------------------- Kamp (kalici)
    val meta: List<MetaDef> = listOf(
        MetaDef("might", "icon_p_might", 5, 120, StatMod(Stat.MIGHT, 0.05f)),
        MetaDef("vital", "icon_p_vital", 5, 100, StatMod(Stat.MAX_HP_PCT, 0.10f)),
        MetaDef("armor", "icon_p_armor", 3, 200, StatMod(Stat.ARMOR, 1f)),
        MetaDef("recover", "icon_p_recover", 5, 120, StatMod(Stat.REGEN, 0.1f)),
        MetaDef("haste", "icon_p_haste", 2, 300, StatMod(Stat.COOLDOWN, 0.025f)),
        MetaDef("reach", "icon_p_reach", 2, 260, StatMod(Stat.AREA, 0.05f)),
        MetaDef("swift", "icon_p_swift", 2, 200, StatMod(Stat.MOVE_SPEED, 0.05f)),
        MetaDef("magnet", "icon_p_magnet", 2, 150, StatMod(Stat.MAGNET, 0.25f)),
        MetaDef("growth", "icon_ui_arrow_up", 5, 160, StatMod(Stat.GROWTH, 0.03f)),
        MetaDef("greed", "icon_ui_crown", 5, 140, StatMod(Stat.GREED, 0.10f)),
        MetaDef("luck", "icon_p_luck", 3, 180, StatMod(Stat.LUCK, 0.10f)),
        MetaDef("oil", "icon_p_oil", 3, 140, StatMod(Stat.LIGHT, 0.05f)),
        MetaDef("reroll", "icon_ui_star", 3, 250, StatMod(Stat.REROLL, 1f)),
        MetaDef("revival", "icon_ui_heart", 1, 1500, StatMod(Stat.REVIVAL, 1f)),
    )

    // ---------------------------------------------------------------- Bolgeler
    val stages: List<StageDef> = listOf(
        StageDef(
            "woods", 0, "tile_woods",
            decals = listOf("decal_woods_tuft", "decal_woods_flowers", "decal_woods_leaves", "decal_woods_stones", "decal_woods_mushrooms"),
            props = listOf("prop_woods_tree", "prop_woods_deadtree", "prop_woods_bush", "prop_woods_grave", "prop_woods_rock"),
            music = "woods", hpMult = 1f, dmgMult = 1f,
            waves = listOf(
                Wave(0f, 1.0f, 1, 30, listOf("shade" to 10)),
                Wave(45f, 0.8f, 1, 40, listOf("shade" to 8, "bat" to 3)),
                Wave(90f, 0.65f, 2, 60, listOf("shade" to 6, "bat" to 4, "husk" to 2)),
                Wave(150f, 0.6f, 2, 80, listOf("shade" to 4, "bat" to 3, "husk" to 2, "wisp" to 2)),
                Wave(210f, 0.55f, 2, 100, listOf("shade" to 3, "husk" to 3, "wisp" to 2, "sporeling" to 3)),
                Wave(300f, 0.5f, 3, 120, listOf("bat" to 5, "sporeling" to 3, "wisp" to 2)),
                Wave(390f, 0.45f, 3, 150, listOf("shade" to 4, "husk" to 3, "sporeling" to 3, "wisp" to 3, "bat" to 3)),
                Wave(480f, 0.4f, 3, 180, listOf("husk" to 4, "wisp" to 3, "sporeling" to 3, "bat" to 4)),
                Wave(BOSS_TIME, 0.6f, 2, 100, listOf("shade" to 5, "bat" to 5)),
            ),
            events = listOf(
                StageEvent(75f, EventType.RING, "bat", 24),
                StageEvent(120f, EventType.ELITE, "husk"),
                StageEvent(180f, EventType.SWARM, "bat", 30),
                StageEvent(240f, EventType.ELITE, "sporeling"),
                StageEvent(300f, EventType.MINIBOSS, "moth_matron"),
                StageEvent(360f, EventType.RING, "shade", 32),
                StageEvent(420f, EventType.ELITE, "wisp"),
                StageEvent(450f, EventType.SWARM, "bat", 40),
                StageEvent(500f, EventType.ELITE, "husk"),
                StageEvent(BOSS_TIME, EventType.BOSS, "hollow_stag"),
            ),
            ambient = 0x14102AFF, dawnAmbient = 0xB86A48FF.toInt(),
        ),
        StageDef(
            "drowned", 1, "tile_drowned",
            decals = listOf("decal_drowned_puddle", "decal_drowned_puddle_big", "decal_drowned_reeds", "decal_drowned_plank", "decal_drowned_bones"),
            props = listOf("prop_drowned_wall", "prop_drowned_lamp", "prop_drowned_boat", "prop_drowned_deadtree", "prop_drowned_bush"),
            music = "drowned", hpMult = 1.3f, dmgMult = 1.2f,
            waves = listOf(
                Wave(0f, 0.9f, 1, 35, listOf("shade" to 8, "drowned" to 2)),
                Wave(45f, 0.75f, 2, 45, listOf("drowned" to 6, "bat" to 3)),
                Wave(90f, 0.65f, 2, 60, listOf("drowned" to 5, "slime" to 3, "bat" to 2)),
                Wave(150f, 0.6f, 2, 80, listOf("drowned" to 4, "slime" to 3, "ghostlamp" to 2)),
                Wave(210f, 0.55f, 2, 100, listOf("drowned" to 4, "ghostlamp" to 3, "slime" to 3, "bat" to 2)),
                Wave(300f, 0.5f, 3, 120, listOf("slime" to 4, "bat" to 4, "ghostlamp" to 2)),
                Wave(390f, 0.45f, 3, 150, listOf("drowned" to 5, "slime" to 3, "ghostlamp" to 3, "shade" to 3)),
                Wave(480f, 0.4f, 3, 180, listOf("drowned" to 5, "ghostlamp" to 3, "slime" to 4, "bat" to 3)),
                Wave(BOSS_TIME, 0.6f, 2, 100, listOf("drowned" to 5, "bat" to 4)),
            ),
            events = listOf(
                StageEvent(75f, EventType.RING, "shade", 24),
                StageEvent(120f, EventType.ELITE, "slime"),
                StageEvent(180f, EventType.SWARM, "bat", 34),
                StageEvent(240f, EventType.ELITE, "drowned"),
                StageEvent(300f, EventType.MINIBOSS, "mire_mother"),
                StageEvent(360f, EventType.RING, "slime", 28),
                StageEvent(420f, EventType.ELITE, "ghostlamp"),
                StageEvent(450f, EventType.SWARM, "bat", 44),
                StageEvent(500f, EventType.ELITE, "drowned"),
                StageEvent(BOSS_TIME, EventType.BOSS, "bellkeeper"),
            ),
            enemyTint = 0xB8F0E0FF.toInt(),
            ambient = 0x0E1626FF, dawnAmbient = 0x9A7A6AFF.toInt(),
        ),
        StageDef(
            "frozen", 2, "tile_frozen",
            decals = listOf("decal_frozen_crack", "decal_frozen_stones", "decal_frozen_frostgrass", "decal_frozen_drift"),
            props = listOf("prop_frozen_pine", "prop_frozen_crystal", "prop_frozen_rock", "prop_frozen_statue"),
            music = "frozen", hpMult = 1.55f, dmgMult = 1.3f,
            waves = listOf(
                Wave(0f, 0.9f, 1, 35, listOf("shade" to 10)),
                Wave(45f, 0.75f, 2, 45, listOf("shade" to 6, "wolf" to 2, "wisp" to 2)),
                Wave(90f, 0.65f, 2, 60, listOf("wolf" to 4, "golem" to 2, "shade" to 3)),
                Wave(150f, 0.6f, 2, 80, listOf("wolf" to 4, "wisp" to 3, "golem" to 2)),
                Wave(210f, 0.55f, 2, 100, listOf("wolf" to 4, "golem" to 3, "wisp" to 3, "shade" to 3)),
                Wave(300f, 0.5f, 3, 120, listOf("wolf" to 6, "wisp" to 3)),
                Wave(390f, 0.45f, 3, 150, listOf("golem" to 4, "wolf" to 5, "wisp" to 3, "shade" to 3)),
                Wave(480f, 0.4f, 3, 180, listOf("golem" to 4, "wolf" to 5, "wisp" to 4)),
                Wave(BOSS_TIME, 0.6f, 2, 100, listOf("wolf" to 5, "shade" to 4)),
            ),
            events = listOf(
                StageEvent(75f, EventType.RING, "shade", 28),
                StageEvent(120f, EventType.ELITE, "golem"),
                StageEvent(180f, EventType.SWARM, "wolf", 24),
                StageEvent(240f, EventType.ELITE, "wolf"),
                StageEvent(300f, EventType.MINIBOSS, "frost_alpha"),
                StageEvent(360f, EventType.RING, "wolf", 26),
                StageEvent(420f, EventType.ELITE, "wisp"),
                StageEvent(450f, EventType.SWARM, "wolf", 32),
                StageEvent(500f, EventType.ELITE, "golem"),
                StageEvent(BOSS_TIME, EventType.BOSS, "frost_wyrm"),
            ),
            enemyTint = 0xD0E4FFFF.toInt(),
            ambient = 0x101A30FF, dawnAmbient = 0xC08A70FF.toInt(),
        ),
    )

    fun stage(id: String) = stages.first { it.id == id }

    /** Seviye n'den n+1'e gecmek icin gereken XP. */
    fun xpForLevel(level: Int): Int {
        val n = (level - 1).toFloat()
        return Math.round(5f + 5f * n + 0.25f * n * n)
    }
}
