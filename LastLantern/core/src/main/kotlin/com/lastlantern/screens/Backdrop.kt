package com.lastlantern.screens

import com.lastlantern.LastLanternGame
import com.lastlantern.content.Content
import com.lastlantern.render.PixelView
import com.lastlantern.render.WorldRenderer
import com.lastlantern.sim.RunConfig
import com.lastlantern.sim.World
import kotlin.math.cos
import kotlin.math.sin

/**
 * Menulerin arka plani: oyunun kendisi. Karanlik bir ormanda fenerci durur,
 * cevrede karanlikta gozler yanip soner. Ayni cizim hatti kullanildigi icin
 * menu ve oyun ayni dunyaya ait hissettirir.
 */
class Backdrop(private val game: LastLanternGame, stageId: String = "woods", heroId: String? = null) {
    private val world: World
    private val view = PixelView()
    private val renderer: WorldRenderer
    var cameraLift = 0.18f

    init {
        val hero = Content.character(heroId ?: game.save.data.selectedCharacter)
        world = World(RunConfig(Content.stage(stageId), hero, seed = 1234))
        // Karanlikta bekleyen birkac golge: yalnizca gozleri gorunur
        val ids = listOf("shade", "shade", "bat", "husk", "shade", "wisp", "shade", "sporeling", "shade", "bat")
        ids.forEachIndexed { k, id ->
            val a = k * 0.63f + 0.4f
            val r = 120f + (k % 3) * 40f
            val i = world.spawnEnemy(id, cos(a) * r, sin(a) * r * 1.3f)
            if (i >= 0) world.enemies.facing[i] = if (cos(a) > 0) -1 else 1
        }
        renderer = WorldRenderer(game.assets, world, { game.settings }, game.supporter)
    }

    fun resize(w: Int, h: Int) {
        view.resize(w, h)
        world.viewHalfW = view.vw / 2f
        world.viewHalfH = view.vh / 2f
    }

    fun render(dt: Float) {
        renderer.update(dt)
        // Dusmanlarin animasyonu ilerlesin ama yerlerinden kipirdamasinlar
        val e = world.enemies
        for (i in 0 until e.high) if (e.active[i]) e.anim[i] += dt
        world.player.anim += dt * 0.5f
        renderer.render(game.batch, view, cameraLift)
    }

    fun dispose() = view.dispose()
}
