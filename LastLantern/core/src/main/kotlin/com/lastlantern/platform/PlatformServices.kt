package com.lastlantern.platform

/**
 * Platforma ozel servisler. Oyun cekirdegi yalnizca bu arayuzu bilir;
 * Android tarafi AdMob, Play Billing ve In-App Review ile, masaustu tarafi
 * sahte (aninda odul veren) uygulamayla doldurur.
 *
 * Tum geri cagirmalar oyun (render) is parcaciginda calistirilmalidir;
 * Android uygulamasi bunu Gdx.app.postRunnable ile garanti eder.
 */
interface PlatformServices {
    /** Odullu reklam su an gosterilebilir mi (yuklu mu)? */
    fun rewardedReady(): Boolean

    /**
     * Odullu reklami gosterir. Oyuncu reklami sonuna kadar izlerse [onReward],
     * aksi halde (kapatti, yuklenemedi) [onClosed] cagrilir. Ikisi birden
     * cagrilmaz.
     */
    fun showRewarded(placement: String, onReward: () -> Unit, onClosed: () -> Unit)

    /** Destekci Paketi satin alinmis mi (Play Billing'den dogrulanmis)? */
    fun isSupporter(): Boolean

    /** Satin alimlar magazadan en az bir kez okundu mu? (Okunmadan iade durumu bilinemez.) */
    fun purchasesLoaded(): Boolean

    /** Destekci Paketi'nin yerel para birimiyle fiyati; bilinmiyorsa null. */
    fun supporterPrice(): String?

    fun buySupporter(onResult: (Boolean) -> Unit)

    fun restorePurchases(onResult: (Boolean) -> Unit)

    /** GDPR/UMP: ayarlarda "Gizlilik secenekleri" dugmesi gosterilmeli mi? */
    fun privacyOptionsRequired(): Boolean

    fun showPrivacyOptions()

    /** Play In-App Review akisini ister (gosterilip gosterilmeyecegine Play karar verir). */
    fun requestReview()

    fun vibrate(millis: Int)

    fun openUrl(url: String)

    /** Geri tusu/hareketi oyun tarafindan mi ele alinsin (false: sistem uygulamayi kapatir). */
    fun setBackHandled(handled: Boolean)

    /** Uygulamadan cik (ana menude geri). */
    fun exitApp()

    val name: String
}

/** Masaustu ve testler icin: reklam aninda "izlenmis" sayilir. */
open class FakePlatform : PlatformServices {
    var supporter = false
    override val name = "desktop"
    override fun rewardedReady() = true
    override fun showRewarded(placement: String, onReward: () -> Unit, onClosed: () -> Unit) = onReward()
    override fun isSupporter() = supporter
    override fun purchasesLoaded() = true
    override fun supporterPrice(): String? = "₺49,99"
    override fun buySupporter(onResult: (Boolean) -> Unit) {
        supporter = true
        onResult(true)
    }
    override fun restorePurchases(onResult: (Boolean) -> Unit) = onResult(supporter)
    override fun privacyOptionsRequired() = false
    override fun showPrivacyOptions() {}
    override fun requestReview() {}
    override fun vibrate(millis: Int) {}
    override fun openUrl(url: String) {}
    override fun setBackHandled(handled: Boolean) {}
    override fun exitApp() {}
}
