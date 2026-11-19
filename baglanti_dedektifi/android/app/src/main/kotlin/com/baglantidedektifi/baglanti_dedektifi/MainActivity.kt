package com.baglantidedektifi.baglanti_dedektifi

import android.os.Bundle
import android.util.Log
import com.google.android.gms.games.PlayGamesSdk
import io.flutter.embedding.android.FlutterActivity

class MainActivity : FlutterActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        // Play Games yalnizca gecerli bir uygulama kimligiyle derlendiyse
        // baslatilir; aksi halde SDK'ya hic dokunulmaz.
        val appId = getString(R.string.play_games_app_id)
        if (appId.isNotEmpty() && appId.all { it.isDigit() }) {
            try {
                PlayGamesSdk.initialize(this)
            } catch (e: Exception) {
                Log.w("BaglantiDedektifi", "Play Games baslatilamadi", e)
            }
        }
    }
}
