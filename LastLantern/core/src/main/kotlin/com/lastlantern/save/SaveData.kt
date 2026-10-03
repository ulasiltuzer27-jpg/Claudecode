package com.lastlantern.save

import kotlinx.serialization.Serializable

/**
 * Oyuncunun kalici ilerlemesi. Surumlu: alan eklemek geriye uyumludur
 * (varsayilan degerler), alan anlamini degistirmek [SaveManager.migrate]
 * icinde yapilir.
 */
@Serializable
data class SaveData(
    var version: Int = CURRENT_VERSION,
    var gold: Int = 0,
    var totalGold: Int = 0,
    var totalKills: Int = 0,
    var runs: Int = 0,
    var wins: MutableMap<String, Int> = mutableMapOf(),
    var bossKills: MutableMap<String, Int> = mutableMapOf(),
    var bestTime: MutableMap<String, Float> = mutableMapOf(),
    var endlessBest: MutableMap<String, Float> = mutableMapOf(),
    var bestLevel: Int = 0,
    var unlockedCharacters: MutableSet<String> = mutableSetOf("keeper"),
    var meta: MutableMap<String, Int> = mutableMapOf(),
    var achievements: MutableSet<String> = mutableSetOf(),
    var evolutionsSeen: MutableSet<String> = mutableSetOf(),
    var selectedCharacter: String = "keeper",
    var selectedStage: String = "woods",
    var tutorialDone: Boolean = false,
    var reviewAsked: Boolean = false,
    var supporterCached: Boolean = false,
    var settings: Settings = Settings(),
) {
    companion object {
        const val CURRENT_VERSION = 1
    }
}

@Serializable
data class Settings(
    var music: Float = 0.7f,
    var sfx: Float = 0.8f,
    var vibration: Boolean = true,
    var screenShake: Boolean = true,
    var damageNumbers: Boolean = true,
    var language: String = "",     // "" = cihaz dili
    var fixedJoystick: Boolean = false,
    var reduceFlashes: Boolean = false,
)
