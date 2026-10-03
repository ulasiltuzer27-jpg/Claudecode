package com.lastlantern.sim

/**
 * Seed'li, hizli ve platformdan bagimsiz RNG (xorshift64*).
 *
 * java.util.Random yerine: ayni seed her JVM'de ve Android'de ayni diziyi
 * vermeli ki testler ve gunluk meydan okuma tekrarlanabilir olsun.
 */
class Rng(seed: Long) {
    private var s: Long = if (seed == 0L) 0x9E3779B97F4A7C15uL.toLong() else seed

    fun nextLong(): Long {
        var x = s
        x = x xor (x ushr 12)
        x = x xor (x shl 25)
        x = x xor (x ushr 27)
        s = x
        return x * 0x2545F4914F6CDD1DL
    }

    /** [0, 1) */
    fun nextFloat(): Float = ((nextLong() ushr 40).toInt()) / (1 shl 24).toFloat()

    fun nextInt(bound: Int): Int {
        require(bound > 0)
        return ((nextLong() ushr 33) % bound).toInt()
    }

    fun range(min: Float, max: Float): Float = min + (max - min) * nextFloat()

    fun chance(p: Float): Boolean = nextFloat() < p

    fun angle(): Float = nextFloat() * TAU

    fun <T> pick(list: List<T>): T = list[nextInt(list.size)]

    companion object {
        const val TAU = (Math.PI * 2).toFloat()
    }
}
