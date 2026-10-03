package com.lastlantern.sim

class Player {
    var x = 0f
    var y = 0f
    var hp = 100f
    var iframes = 0f
    var faceX = 1f      // son hareket yonu (hancer bu yone atilir)
    var faceY = 0f
    var moving = false
    var flipX = false
    var anim = 0f
    var level = 1
    var xp = 0f
    var xpToNext = 6
    var distance = 0f   // toplam yurunen mesafe (basarim)
    var hurtFlash = 0f

    val radius get() = 6f
}
