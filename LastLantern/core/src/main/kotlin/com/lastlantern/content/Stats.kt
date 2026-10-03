package com.lastlantern.content

/** Oyuncunun run icindeki toplam istatistikleri (karakter + kamp + pasifler). */
class Stats {
    var maxHp = 100f
    var regen = 0f            // HP / sn
    var armor = 0f            // her vurustan duz dusulur
    var moveSpeed = 1f        // carpan
    var might = 1f            // hasar carpani
    var area = 1f             // alan/boyut carpani
    var cooldown = 1f         // bekleme carpani (dusuk = hizli)
    var projSpeed = 1f
    var duration = 1f
    var amount = 0            // ek mermi
    var magnet = 1f           // toplama yaricapi carpani
    var luck = 1f
    var growth = 1f           // XP carpani
    var greed = 1f            // altin carpani
    var light = 1f            // isik yaricapi carpani
    var revivals = 0
    var rerolls = 0

    fun copyFrom(o: Stats) {
        maxHp = o.maxHp; regen = o.regen; armor = o.armor; moveSpeed = o.moveSpeed
        might = o.might; area = o.area; cooldown = o.cooldown; projSpeed = o.projSpeed
        duration = o.duration; amount = o.amount; magnet = o.magnet; luck = o.luck
        growth = o.growth; greed = o.greed; light = o.light; revivals = o.revivals; rerolls = o.rerolls
    }

    fun apply(stat: Stat, v: Float) {
        when (stat) {
            Stat.MAX_HP -> maxHp += v
            Stat.MAX_HP_PCT -> maxHp *= 1f + v
            Stat.REGEN -> regen += v
            Stat.ARMOR -> armor += v
            Stat.MOVE_SPEED -> moveSpeed += v
            Stat.MIGHT -> might += v
            Stat.AREA -> area += v
            Stat.COOLDOWN -> cooldown -= v
            Stat.PROJ_SPEED -> projSpeed += v
            Stat.DURATION -> duration += v
            Stat.AMOUNT -> amount += v.toInt()
            Stat.MAGNET -> magnet += v
            Stat.LUCK -> luck += v
            Stat.GROWTH -> growth += v
            Stat.GREED -> greed += v
            Stat.LIGHT -> light += v
            Stat.REVIVAL -> revivals += v.toInt()
            Stat.REROLL -> rerolls += v.toInt()
        }
    }
}

enum class Stat {
    MAX_HP, MAX_HP_PCT, REGEN, ARMOR, MOVE_SPEED, MIGHT, AREA, COOLDOWN, PROJ_SPEED, DURATION,
    AMOUNT, MAGNET, LUCK, GROWTH, GREED, LIGHT, REVIVAL, REROLL
}

class StatMod(val stat: Stat, val value: Float)
