package com.lastlantern.assets

import com.badlogic.gdx.Gdx
import com.badlogic.gdx.graphics.Texture
import com.badlogic.gdx.graphics.g2d.BitmapFont
import com.badlogic.gdx.graphics.g2d.TextureAtlas
import com.badlogic.gdx.graphics.g2d.TextureRegion
import com.badlogic.gdx.utils.Array as GdxArray
import com.badlogic.gdx.utils.Disposable

/**
 * Atlas, fontlar ve isik dokusu. Butun sprite'lar tek atlas sayfasinda;
 * fontlar da ayni sayfadan okunur, boylece bir karede neredeyse hic doku
 * degisimi (ve SpriteBatch flush'u) olmaz.
 */
class Assets : Disposable {
    lateinit var atlas: TextureAtlas
        private set
    lateinit var font: BitmapFont
        private set
    lateinit var fontOutline: BitmapFont
        private set
    lateinit var light: Texture
        private set
    lateinit var pixel: TextureRegion
        private set

    private val regions = HashMap<String, TextureRegion>()
    private val anims = HashMap<String, GdxArray<TextureAtlas.AtlasRegion>>()

    fun load() {
        atlas = TextureAtlas(Gdx.files.internal("atlas/game.atlas"))
        font = BitmapFont(Gdx.files.internal("fonts/font.fnt"), atlas.findRegion("font"))
        fontOutline = BitmapFont(Gdx.files.internal("fonts/font_outline.fnt"), atlas.findRegion("font_outline"))
        for (f in listOf(font, fontOutline)) {
            f.setUseIntegerPositions(true)
            f.data.markupEnabled = true
            // Fontta olmayan bir karakter bos kalmasin, bosluk gibi davransin
            f.data.missingGlyph = f.data.getGlyph(' ')
        }
        light = Texture(Gdx.files.internal("textures/light.png")).apply {
            setFilter(Texture.TextureFilter.Linear, Texture.TextureFilter.Linear)
        }
        pixel = region("pixel")
    }

    fun has(name: String): Boolean = try {
        region(name); true
    } catch (e: IllegalStateException) {
        false
    }

    /** "ad", "ad" (indeks 0) ya da "ad_3" (TexturePacker indeksli kare) bicimlerini cozer. */
    fun region(name: String): TextureRegion = regions.getOrPut(name) {
        atlas.findRegion(name) ?: atlas.findRegion(name, 0) ?: INDEXED.find(name)?.let {
            atlas.findRegion(it.groupValues[1], it.groupValues[2].toInt())
        } ?: error("atlas'ta yok: $name")
    }

    private companion object {
        val INDEXED = Regex("^(.*)_(\\d+)$")
    }

    fun frames(name: String): GdxArray<TextureAtlas.AtlasRegion> = anims.getOrPut(name) {
        val list = atlas.findRegions(name)
        if (list.size > 0) list else GdxArray<TextureAtlas.AtlasRegion>().apply {
            add(atlas.findRegion(name) ?: error("atlas'ta yok: $name"))
        }
    }

    fun frame(name: String, time: Float, frameTime: Float): TextureRegion {
        val f = frames(name)
        if (f.size == 1) return f[0]
        val i = ((time / frameTime).toInt() % f.size + f.size) % f.size
        return f[i]
    }

    override fun dispose() {
        atlas.dispose()
        font.dispose()
        fontOutline.dispose()
        light.dispose()
    }
}
