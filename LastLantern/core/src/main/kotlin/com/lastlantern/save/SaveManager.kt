package com.lastlantern.save

import kotlinx.serialization.json.Json

/**
 * Kaydi bir anahtar-deger deposunda tutar (Android'de SharedPreferences,
 * masaustunde ~/.prefs). Iki kopya yazilir: ana ve yedek. Ana kopya bozuksa
 * (yazma sirasinda uygulama oldurulduyse) yedekten okunur.
 */
class SaveManager(private val store: KeyValueStore) {
    private val json = Json {
        ignoreUnknownKeys = true
        encodeDefaults = true
        coerceInputValues = true
    }

    var data: SaveData = SaveData()
        private set

    fun load(): SaveData {
        data = read(KEY) ?: read(KEY_BACKUP) ?: SaveData()
        data = migrate(data)
        return data
    }

    private fun read(key: String): SaveData? {
        val raw = store.get(key) ?: return null
        return try {
            json.decodeFromString(SaveData.serializer(), raw)
        } catch (e: Exception) {
            null
        }
    }

    fun save() {
        val raw = json.encodeToString(SaveData.serializer(), data)
        // Once yedek, sonra ana: ana yazilirken kesilirse yedek saglam kalir
        store.put(KEY_BACKUP, store.get(KEY) ?: raw)
        store.put(KEY, raw)
        store.flush()
    }

    fun reset() {
        data = SaveData()
        save()
    }

    fun encode(d: SaveData): String = json.encodeToString(SaveData.serializer(), d)

    fun decode(s: String): SaveData = json.decodeFromString(SaveData.serializer(), s)

    private fun migrate(d: SaveData): SaveData {
        // v1 tek surum; ileride: if (d.version < 2) { ... }
        d.version = SaveData.CURRENT_VERSION
        if (d.unlockedCharacters.isEmpty()) d.unlockedCharacters.add("keeper")
        return d
    }

    companion object {
        const val KEY = "save"
        const val KEY_BACKUP = "save_backup"
    }
}

interface KeyValueStore {
    fun get(key: String): String?
    fun put(key: String, value: String)
    fun flush()
}

class MemoryStore : KeyValueStore {
    val map = HashMap<String, String>()
    override fun get(key: String) = map[key]
    override fun put(key: String, value: String) {
        map[key] = value
    }
    override fun flush() {}
}
