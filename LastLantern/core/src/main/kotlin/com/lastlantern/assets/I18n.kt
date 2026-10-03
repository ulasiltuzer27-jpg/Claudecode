package com.lastlantern.assets

import com.badlogic.gdx.Gdx
import com.badlogic.gdx.utils.I18NBundle
import java.util.Locale
import java.util.MissingResourceException

/** Ceviri paketi. Dil "" ise cihaz dili; desteklenmeyen dilde Ingilizce. */
class I18n {
    private lateinit var bundle: I18NBundle
    var language = "en"
        private set

    fun load(override: String) {
        I18NBundle.setSimpleFormatter(true)
        I18NBundle.setExceptionOnMissingKey(false)
        val locale = when {
            override.isNotEmpty() -> Locale(override)
            else -> Locale.getDefault()
        }
        language = if (locale.language == "tr") "tr" else "en"
        bundle = I18NBundle.createBundle(Gdx.files.internal("i18n/strings"), Locale(language), "UTF-8")
    }

    operator fun get(key: String): String = try {
        bundle.get(key)
    } catch (e: MissingResourceException) {
        key
    }

    fun f(key: String, vararg args: Any?): String = try {
        bundle.format(key, *args)
    } catch (e: MissingResourceException) {
        key
    }

    /** Ondalik sayi: Turkcede virgul. */
    fun num(v: Float, decimals: Int = 1): String {
        val s = "%.${decimals}f".format(Locale.US, v).trimEnd('0').trimEnd('.')
        return if (language == "tr") s.replace('.', ',') else s
    }

    companion object {
        fun time(seconds: Float): String {
            val t = seconds.toInt().coerceAtLeast(0)
            return "%d:%02d".format(t / 60, t % 60)
        }
    }
}
