package com.lastlantern.assets

import com.badlogic.gdx.Gdx
import com.badlogic.gdx.audio.Music
import com.badlogic.gdx.audio.Sound
import com.badlogic.gdx.utils.Disposable
import com.badlogic.gdx.utils.TimeUtils
import com.lastlantern.save.Settings
import com.lastlantern.sim.Sfx

/**
 * Ses efektleri ve muzik. Ayni efektin ayni anda onlarca kez calmasini
 * (yuzlerce koz toplanirken) kisitlar: hem kulak hem SoundPool icin.
 */
class Audio(private val settings: () -> Settings) : Disposable {
    private val sounds = arrayOfNulls<Sound>(Sfx.entries.size)
    private val lastPlay = LongArray(Sfx.entries.size)
    private val minGapMs = IntArray(Sfx.entries.size) { 30 }

    private var music: Music? = null
    private var musicName: String? = null
    private var fading: Music? = null
    private var fadeT = 0f
    private var musicVolume = 0f
    private var duck = 1f
    private var paused = false

    fun load() {
        for (s in Sfx.entries) {
            val f = Gdx.files.internal(s.file)
            if (f.exists()) sounds[s.ordinal] = Gdx.audio.newSound(f)
        }
        minGapMs[Sfx.HIT.ordinal] = 45
        minGapMs[Sfx.EMBER.ordinal] = 35
        minGapMs[Sfx.SPARK.ordinal] = 70
        minGapMs[Sfx.DAGGER.ordinal] = 70
        minGapMs[Sfx.ENEMY_DIE.ordinal] = 40
        minGapMs[Sfx.COIN.ordinal] = 60
        minGapMs[Sfx.ENEMY_SHOOT.ordinal] = 120
        minGapMs[Sfx.MOTH.ordinal] = 150
    }

    fun play(s: Sfx, pitch: Float = 1f, volume: Float = 1f) {
        val vol = settings().sfx * volume
        if (vol <= 0.01f) return
        val now = TimeUtils.millis()
        if (now - lastPlay[s.ordinal] < minGapMs[s.ordinal]) return
        lastPlay[s.ordinal] = now
        sounds[s.ordinal]?.play(vol, pitch.coerceIn(0.5f, 2f), 0f)
    }

    fun playMusic(name: String?) {
        if (name == musicName) return
        fading?.dispose()
        fading = music
        fadeT = 0.8f
        musicName = name
        music = null
        if (name != null) {
            val f = Gdx.files.internal("music/$name.ogg")
            if (f.exists()) {
                music = Gdx.audio.newMusic(f).apply {
                    isLooping = true
                    volume = 0f
                    if (!paused) play()
                }
                musicVolume = 0f
            }
        }
    }

    /** Diyalog/duraklatma sirasinda muzigi kis. */
    fun duck(on: Boolean) {
        duck = if (on) 0.35f else 1f
    }

    fun update(dt: Float) {
        val target = settings().music * duck
        musicVolume += (target - musicVolume) * (dt * 3f).coerceAtMost(1f)
        music?.volume = musicVolume
        val f = fading
        if (f != null) {
            fadeT -= dt
            if (fadeT <= 0f) {
                f.stop()
                f.dispose()
                fading = null
            } else {
                f.volume = target * (fadeT / 0.8f)
            }
        }
    }

    fun pause() {
        paused = true
        music?.pause()
        fading?.pause()
    }

    fun resume() {
        paused = false
        music?.play()
    }

    override fun dispose() {
        sounds.forEach { it?.dispose() }
        music?.dispose()
        fading?.dispose()
    }
}
