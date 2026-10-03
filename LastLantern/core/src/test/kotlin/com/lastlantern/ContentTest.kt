package com.lastlantern

import com.lastlantern.content.Achievements
import com.lastlantern.content.Content
import com.lastlantern.content.Tier
import com.lastlantern.save.MemoryStore
import com.lastlantern.save.SaveData
import com.lastlantern.save.SaveManager
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import java.io.File
import java.util.Properties

/**
 * Icerik butunlugu: kodda adi gecen her sprite atlas'ta, her metin anahtari
 * iki dilde de, metinlerdeki her karakter fontta olmali. Bu hatalar derlemede
 * gorunmez; oyunda ilk kez o ekrana girildiginde patlar.
 *
 * Testler assets/ klasorunde calisir (core/build.gradle.kts > workingDir).
 */
class ContentTest {
    private val atlas: Set<String> by lazy {
        val names = HashSet<String>()
        val lines = File("atlas/game.atlas").readLines()
        var i = 0
        while (i < lines.size) {
            val l = lines[i]
            if (l.isNotBlank() && !l.startsWith(" ") && !l.contains(":") && !l.endsWith(".png")) {
                // Sonraki satirlarda index varsa "ad_N" olarak da kaydet
                val idx = lines.drop(i + 1).takeWhile { it.startsWith(" ") }.firstOrNull { it.trim().startsWith("index:") }
                    ?.substringAfter(":")?.trim()?.toIntOrNull() ?: -1
                names.add(l)
                if (idx >= 0) names.add("${l}_$idx")
            }
            i++
        }
        names
    }

    private fun props(lang: String): Properties = Properties().apply {
        val f = if (lang == "en") "i18n/strings.properties" else "i18n/strings_$lang.properties"
        File(f).reader(Charsets.UTF_8).use { load(it) }
    }

    private fun hasSprite(name: String) = name in atlas

    @Test
    fun spritesExist() {
        val missing = ArrayList<String>()
        fun need(n: String) {
            if (!hasSprite(n)) missing.add(n)
        }
        for (e in Content.enemies.values) {
            need(e.sprite); need(e.sprite + "_white"); need(e.sprite + "_eyes")
        }
        for (w in Content.weapons.values) need(w.icon)
        for (p in Content.passives.values) need(p.icon)
        for (m in Content.meta) need(m.icon)
        for (a in Achievements.all) need(a.icon)
        for (c in Content.characters) {
            need("${c.sprite}_walk"); need("${c.sprite}_idle"); need("portrait_${c.id}")
        }
        for (s in Content.stages) {
            for (k in 0..3) need("${s.tileset}_$k")
            s.decals.forEach { need(it) }
            s.props.forEach { need(it) }
        }
        listOf("proj_spark", "proj_solar", "proj_moth", "proj_phoenix", "proj_dagger", "proj_starknife", "proj_sickle",
            "proj_reaper", "proj_flame", "proj_blueflame", "proj_bolt", "proj_icebolt", "pick_ember_s", "pick_ember_m",
            "pick_ember_l", "pick_coin", "pick_chest", "pick_simit", "pick_magnet", "pick_flare", "pick_oil", "pixel",
            "circle8", "circle16", "circle32", "ring48", "shadow", "shadow_big", "fx_glow8", "fx_glow16", "font",
            "font_outline", "ui_panel", "ui_button", "ui_button_primary", "ui_card", "ui_slot", "ui_bar",
            "icon_ui_pause", "icon_ui_skull", "icon_ui_lock", "icon_ui_check", "icon_ui_heart").forEach { need(it) }
        assertTrue(missing.isEmpty(), "atlas'ta olmayan sprite'lar: $missing")
    }

    @Test
    fun contentReferencesResolve() {
        for (w in Content.weapons.values) {
            w.evolvesTo?.let { assertTrue(it in Content.weapons, "evrim yok: $it") }
            w.evolvePassive?.let { assertTrue(it in Content.passives, "pasif yok: $it") }
            if (!w.evolved) assertEquals(6, w.maxLevel, "${w.id} seviye sayisi")
        }
        for (c in Content.characters) assertTrue(c.startWeapon in Content.weapons)
        for (s in Content.stages) {
            for (wv in s.waves) for ((id, _) in wv.mix) assertTrue(id in Content.enemies, "${s.id}: $id")
            for (ev in s.events) assertTrue(ev.enemy in Content.enemies, "${s.id}: ${ev.enemy}")
            assertEquals(1, s.events.count { Content.enemies.getValue(it.enemy).tier == Tier.BOSS }, "${s.id}: tek boss")
        }
        for (e in Content.enemies.values) e.splitInto?.let { assertTrue(it in Content.enemies) }
        // Her temel silahin evrim pasifi farkli olmali (oyuncu karistirmasin)
        val evoPassives = Content.baseWeapons.map { Content.weapons.getValue(it).evolvePassive }
        assertEquals(evoPassives.size, evoPassives.toSet().size)
    }

    @Test
    fun translationsComplete() {
        val en = props("en")
        val tr = props("tr")
        assertEquals(en.keys, tr.keys, "EN ve TR anahtarlari farkli: " +
            "yalniz EN=${en.keys - tr.keys}, yalniz TR=${tr.keys - en.keys}")
        val needed = ArrayList<String>()
        for (w in Content.weapons.values) needed += listOf("weapon.${w.id}", "weapon.${w.id}.desc")
        for (p in Content.passives.values) needed += listOf("passive.${p.id}", "passive.${p.id}.desc")
        for (m in Content.meta) needed += listOf("meta.${m.id}", "meta.${m.id}.desc")
        for (a in Achievements.all) needed += listOf("ach.${a.id}", "ach.${a.id}.desc")
        for (c in Content.characters) needed += listOf("char.${c.id}", "char.${c.id}.desc")
        for (s in Content.stages) needed += "stage.${s.id}"
        for (e in Content.enemies.values.filter { it.tier == Tier.BOSS }) needed += "hud.boss_name.${e.id}"
        // Kaynak kodda t["..."] / t.f("...") ile gecen anahtarlar
        val src = File("../core/src/main/kotlin").walkTopDown().filter { it.extension == "kt" }.joinToString("\n") { it.readText() }
        Regex("""t(?:\.f)?\(?\[?"([a-z_]+\.[a-z0-9_.]+)"""").findAll(src).forEach { needed += it.groupValues[1] }
        val missing = needed.filter { !en.containsKey(it) }.distinct()
        assertTrue(missing.isEmpty(), "ceviride olmayan anahtarlar: $missing")
    }

    @Test
    fun fontCoversAllText() {
        val glyphs = HashSet<Int>()
        File("fonts/font.fnt").readLines().filter { it.startsWith("char ") }.forEach {
            glyphs += it.substringAfter("id=").substringBefore(" ").toInt()
        }
        val missing = HashSet<String>()
        for (lang in listOf("en", "tr")) {
            for (v in props(lang).values) {
                (v as String).codePoints().forEach { cp ->
                    if (cp !in glyphs && cp != '\n'.code) missing += "${String(Character.toChars(cp))} (U+%04X)".format(cp)
                }
            }
        }
        // Dil adlari ayarlarda kodda gecer
        "English Türkçe ₺€".codePoints().forEach { if (it !in glyphs) missing += String(Character.toChars(it)) }
        assertTrue(missing.isEmpty(), "fontta olmayan karakterler: $missing")
    }

    @Test
    fun saveRoundTripAndRecovery() {
        val store = MemoryStore()
        val sm = SaveManager(store)
        sm.load()
        sm.data.gold = 777
        sm.data.meta["might"] = 3
        sm.data.achievements.add("first_steps")
        sm.save()
        sm.data.gold = 778
        sm.save()
        // Ana kayit bozulursa yedekten donulur
        store.put(SaveManager.KEY, "{bozuk json")
        val again = SaveManager(store).load()
        assertEquals(777, again.gold)
        assertEquals(3, again.meta["might"])
        // Bilinmeyen alanlar (gelecek surum) eski surumu bozmaz
        val withExtra = SaveManager(store).encode(SaveData(gold = 5)).replaceFirst("{", "{\"yeniAlan\":1,")
        assertEquals(5, SaveManager(store).decode(withExtra).gold)
    }

    @Test
    fun metaCostsGrow() {
        for (m in Content.meta) {
            var prev = 0
            for (r in 0 until m.maxRank) {
                val c = m.cost(r)
                assertTrue(c > prev, "${m.id} rank $r maliyet artmali")
                prev = c
            }
        }
    }
}
