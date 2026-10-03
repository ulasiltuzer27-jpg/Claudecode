package com.lastlantern.android

import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.os.VibrationEffect
import android.os.Vibrator
import android.os.VibratorManager
import android.util.Log
import com.android.billingclient.api.AcknowledgePurchaseParams
import com.android.billingclient.api.BillingClient
import com.android.billingclient.api.BillingClientStateListener
import com.android.billingclient.api.BillingFlowParams
import com.android.billingclient.api.BillingResult
import com.android.billingclient.api.PendingPurchasesParams
import com.android.billingclient.api.ProductDetails
import com.android.billingclient.api.Purchase
import com.android.billingclient.api.PurchasesUpdatedListener
import com.android.billingclient.api.QueryProductDetailsParams
import com.android.billingclient.api.QueryPurchasesParams
import com.badlogic.gdx.Gdx
import com.google.android.gms.ads.AdError
import com.google.android.gms.ads.AdRequest
import com.google.android.gms.ads.FullScreenContentCallback
import com.google.android.gms.ads.LoadAdError
import com.google.android.gms.ads.MobileAds
import com.google.android.gms.ads.rewarded.RewardedAd
import com.google.android.gms.ads.rewarded.RewardedAdLoadCallback
import com.google.android.play.core.review.ReviewManagerFactory
import com.google.android.ump.ConsentInformation
import com.google.android.ump.ConsentRequestParameters
import com.google.android.ump.UserMessagingPlatform
import com.lastlantern.platform.PlatformServices
import java.util.concurrent.atomic.AtomicBoolean

/**
 * Android servisleri: AdMob odullu reklam (UMP onayiyla), Play Billing
 * (tek seferlik "supporter_pack"), In-App Review, titresim ve URL acma.
 *
 * Kurallar:
 *  - Reklam, AB/Birlesik Krallik onayi alinmadan istenmez (UMP canRequestAds).
 *  - Satin alma once dogrulanir (PURCHASED), sonra 3 gun icinde onaylanir
 *    (acknowledge); onaylanmayan satin alimi Play otomatik iade eder.
 *  - Oyun tarafina giden her geri cagri Gdx.app.postRunnable ile render
 *    is parcacigina tasinir.
 */
class AndroidPlatform(private val activity: AndroidLauncher) : PlatformServices, PurchasesUpdatedListener {
    override val name = "android"

    private val consent: ConsentInformation = UserMessagingPlatform.getConsentInformation(activity)
    private val adsStarted = AtomicBoolean(false)
    @Volatile private var rewarded: RewardedAd? = null
    @Volatile private var loading = false
    private var retryDelayMs = 4_000L

    private lateinit var billing: BillingClient
    @Volatile private var supporter = false
    @Volatile private var loaded = false
    @Volatile private var details: ProductDetails? = null
    private var pendingBuy: ((Boolean) -> Unit)? = null

    fun start() {
        startConsent()
        startBilling()
    }

    fun dispose() {
        if (::billing.isInitialized) billing.endConnection()
    }

    private fun gdx(fn: () -> Unit) {
        Gdx.app?.postRunnable(fn) ?: fn()
    }

    // ------------------------------------------------------------------ UMP + reklam
    private fun startConsent() {
        val params = ConsentRequestParameters.Builder().build()
        consent.requestConsentInfoUpdate(activity, params, {
            UserMessagingPlatform.loadAndShowConsentFormIfRequired(activity) { error ->
                if (error != null) Log.w(TAG, "UMP formu: ${error.message}")
                if (consent.canRequestAds()) startAds()
            }
        }, { error ->
            Log.w(TAG, "UMP bilgisi alinamadi: ${error.message}")
            if (consent.canRequestAds()) startAds()
        })
        // Onceki oturumdan onay varsa beklemeden basla
        if (consent.canRequestAds()) startAds()
    }

    private fun startAds() {
        if (!adsStarted.compareAndSet(false, true)) return
        Thread {
            MobileAds.initialize(activity) {
                activity.runOnUiThread { loadRewarded() }
            }
        }.start()
    }

    private fun loadRewarded() {
        if (loading || rewarded != null || !consent.canRequestAds()) return
        loading = true
        RewardedAd.load(activity, BuildConfig.ADMOB_REWARDED_ID, AdRequest.Builder().build(), object : RewardedAdLoadCallback() {
            override fun onAdLoaded(ad: RewardedAd) {
                loading = false
                rewarded = ad
                retryDelayMs = 4_000L
            }

            override fun onAdFailedToLoad(error: LoadAdError) {
                loading = false
                rewarded = null
                Log.w(TAG, "Reklam yuklenemedi: ${error.code} ${error.message}")
                // Ustel geri cekilme ile tekrar dene (en fazla ~2 dk)
                val delay = retryDelayMs
                retryDelayMs = (retryDelayMs * 2).coerceAtMost(120_000L)
                activity.window.decorView.postDelayed({ loadRewarded() }, delay)
            }
        })
    }

    override fun rewardedReady(): Boolean = rewarded != null

    override fun showRewarded(placement: String, onReward: () -> Unit, onClosed: () -> Unit) {
        activity.runOnUiThread {
            val ad = rewarded
            if (ad == null) {
                gdx(onClosed)
                loadRewarded()
                return@runOnUiThread
            }
            rewarded = null
            var earned = false
            ad.fullScreenContentCallback = object : FullScreenContentCallback() {
                override fun onAdDismissedFullScreenContent() {
                    gdx(if (earned) onReward else onClosed)
                    loadRewarded()
                }

                override fun onAdFailedToShowFullScreenContent(error: AdError) {
                    Log.w(TAG, "Reklam gosterilemedi: ${error.message}")
                    gdx(onClosed)
                    loadRewarded()
                }
            }
            ad.show(activity) { earned = true }
        }
    }

    override fun privacyOptionsRequired(): Boolean =
        consent.privacyOptionsRequirementStatus == ConsentInformation.PrivacyOptionsRequirementStatus.REQUIRED

    override fun showPrivacyOptions() {
        activity.runOnUiThread {
            UserMessagingPlatform.showPrivacyOptionsForm(activity) { error ->
                if (error != null) Log.w(TAG, "Gizlilik formu: ${error.message}")
            }
        }
    }

    // ------------------------------------------------------------------ Billing
    private fun startBilling() {
        billing = BillingClient.newBuilder(activity)
            .setListener(this)
            .enablePendingPurchases(PendingPurchasesParams.newBuilder().enableOneTimeProducts().build())
            .build()
        connect()
    }

    private fun connect(after: (() -> Unit)? = null) {
        billing.startConnection(object : BillingClientStateListener {
            override fun onBillingSetupFinished(result: BillingResult) {
                if (result.responseCode == BillingClient.BillingResponseCode.OK) {
                    queryDetails()
                    queryPurchases(null)
                    after?.invoke()
                } else {
                    Log.w(TAG, "Billing kurulamadi: ${result.debugMessage}")
                }
            }

            override fun onBillingServiceDisconnected() {
                Log.w(TAG, "Billing baglantisi koptu")
            }
        })
    }

    private fun ready(fn: () -> Unit) {
        if (billing.isReady) fn() else connect(fn)
    }

    private fun queryDetails() {
        val product = QueryProductDetailsParams.Product.newBuilder()
            .setProductId(SUPPORTER_ID)
            .setProductType(BillingClient.ProductType.INAPP)
            .build()
        val params = QueryProductDetailsParams.newBuilder().setProductList(listOf(product)).build()
        billing.queryProductDetailsAsync(params) { result, productDetailsResult ->
            if (result.responseCode == BillingClient.BillingResponseCode.OK) {
                details = productDetailsResult.productDetailsList.firstOrNull { it.productId == SUPPORTER_ID }
            }
        }
    }

    private fun queryPurchases(onDone: ((Boolean) -> Unit)?) {
        val params = QueryPurchasesParams.newBuilder().setProductType(BillingClient.ProductType.INAPP).build()
        billing.queryPurchasesAsync(params) { result, purchases ->
            if (result.responseCode == BillingClient.BillingResponseCode.OK) {
                val owned = purchases.any { handle(it) }
                supporter = owned
                loaded = true
            }
            onDone?.let { cb -> gdx { cb(supporter) } }
        }
    }

    /** true: gecerli (odenmis) Destekci Paketi. */
    private fun handle(p: Purchase): Boolean {
        if (SUPPORTER_ID !in p.products) return false
        if (p.purchaseState != Purchase.PurchaseState.PURCHASED) return false
        if (!p.isAcknowledged) {
            val ack = AcknowledgePurchaseParams.newBuilder().setPurchaseToken(p.purchaseToken).build()
            billing.acknowledgePurchase(ack) { r ->
                if (r.responseCode != BillingClient.BillingResponseCode.OK) Log.w(TAG, "Onaylanamadi: ${r.debugMessage}")
            }
        }
        return true
    }

    override fun onPurchasesUpdated(result: BillingResult, purchases: MutableList<Purchase>?) {
        val cb = pendingBuy
        pendingBuy = null
        when (result.responseCode) {
            BillingClient.BillingResponseCode.OK -> {
                val ok = purchases?.any { handle(it) } == true
                if (ok) supporter = true
                cb?.let { gdx { it(ok) } }
            }
            BillingClient.BillingResponseCode.ITEM_ALREADY_OWNED -> queryPurchases { owned -> cb?.invoke(owned) }
            else -> cb?.let { gdx { it(false) } }
        }
    }

    override fun isSupporter() = supporter

    override fun purchasesLoaded() = loaded

    override fun supporterPrice(): String? = details?.oneTimePurchaseOfferDetails?.formattedPrice

    override fun buySupporter(onResult: (Boolean) -> Unit) {
        activity.runOnUiThread {
            ready {
                val d = details
                if (d == null) {
                    queryDetails()
                    gdx { onResult(false) }
                    return@ready
                }
                val pdp = BillingFlowParams.ProductDetailsParams.newBuilder().setProductDetails(d).build()
                val flow = BillingFlowParams.newBuilder().setProductDetailsParamsList(listOf(pdp)).build()
                pendingBuy = onResult
                val r = billing.launchBillingFlow(activity, flow)
                if (r.responseCode != BillingClient.BillingResponseCode.OK) {
                    pendingBuy = null
                    gdx { onResult(false) }
                }
            }
        }
    }

    override fun restorePurchases(onResult: (Boolean) -> Unit) {
        activity.runOnUiThread { ready { queryPurchases(onResult) } }
    }

    // ------------------------------------------------------------------ diger
    override fun requestReview() {
        activity.runOnUiThread {
            val manager = ReviewManagerFactory.create(activity)
            manager.requestReviewFlow().addOnCompleteListener { task ->
                if (task.isSuccessful) manager.launchReviewFlow(activity, task.result)
            }
        }
    }

    override fun vibrate(millis: Int) {
        val v: Vibrator? = if (Build.VERSION.SDK_INT >= 31) {
            (activity.getSystemService(Context.VIBRATOR_MANAGER_SERVICE) as? VibratorManager)?.defaultVibrator
        } else {
            @Suppress("DEPRECATION")
            activity.getSystemService(Context.VIBRATOR_SERVICE) as? Vibrator
        }
        if (v == null || !v.hasVibrator()) return
        if (Build.VERSION.SDK_INT >= 26) {
            v.vibrate(VibrationEffect.createOneShot(millis.toLong(), VibrationEffect.DEFAULT_AMPLITUDE))
        } else {
            @Suppress("DEPRECATION")
            v.vibrate(millis.toLong())
        }
    }

    override fun openUrl(url: String) {
        activity.runOnUiThread {
            try {
                activity.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url)))
            } catch (e: Exception) {
                Log.w(TAG, "URL acilamadi: $url")
            }
        }
    }

    override fun setBackHandled(handled: Boolean) = activity.setBackHandled(handled)

    override fun exitApp() {
        activity.runOnUiThread { activity.moveTaskToBack(true) }
    }

    companion object {
        private const val TAG = "LastLantern"
        const val SUPPORTER_ID = "supporter_pack"
    }
}
