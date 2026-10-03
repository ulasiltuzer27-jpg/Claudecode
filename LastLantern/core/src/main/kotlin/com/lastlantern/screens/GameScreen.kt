package com.lastlantern.screens

import com.badlogic.gdx.Gdx
import com.badlogic.gdx.Input
import com.badlogic.gdx.scenes.scene2d.ui.ImageButton
import com.badlogic.gdx.scenes.scene2d.utils.TextureRegionDrawable
import com.lastlantern.LastLanternGame
import com.lastlantern.content.Content
import com.lastlantern.content.Tier
import com.lastlantern.render.PixelView
import com.lastlantern.render.WorldRenderer
import com.lastlantern.sim.Bot
import com.lastlantern.sim.Ev
import com.lastlantern.sim.RunConfig
import com.lastlantern.sim.RunState
import com.lastlantern.sim.Sfx
import com.lastlantern.sim.World
import com.lastlantern.ui.ChestOverlay
import com.lastlantern.ui.Hud
import com.lastlantern.ui.Joystick
import com.lastlantern.ui.LevelUpOverlay
import com.lastlantern.ui.Overlay
import com.lastlantern.ui.PauseOverlay
import com.lastlantern.ui.ReviveOverlay
import com.lastlantern.ui.UiKit

/** Bir gece: simulasyon + cizim + HUD + katmanlar. */
class GameScreen(game: LastLanternGame, config: RunConfig, private val tutorial: Boolean = false) : BaseScreen(game) {
    val world = World(config)
    private val view = PixelView()
    val renderer = WorldRenderer(game.assets, world, { game.settings }, game.supporter)
    private val hud = Hud(game, world)
    private val joystick = Joystick(game)
    private val pauseButton: ImageButton
    var bot: Bot? = if (game.launch.autoplay) Bot(world) else null

    private var accumulator = 0f
    private var overlay: Overlay? = null
    private var paused = false
    private var finished = false
    private var tutorialStep = 0
    private var tutorialTimer = 0f
    /** Ekran goruntusu senaryolari icin: simulasyonu dondur. */
    var frozen = false

    init {
        stage.addActor(joystick)
        stage.addActor(hud)
        val icon = TextureRegionDrawable(game.assets.region("icon_ui_pause"))
        pauseButton = ImageButton(ImageButton.ImageButtonStyle().apply {
            up = ui.slot
            imageUp = icon
        })
        ui.onClick(pauseButton) { openPause() }
        stage.addActor(pauseButton)
        game.audio.playMusic(world.stage.music)
        if (world.bossSlot >= 0) hud.bossName = world.enemies.def[world.bossSlot]?.let { t["hud.boss_name.${it.id}"] }
    }

    override fun resize(width: Int, height: Int) {
        super.resize(width, height)
        view.resize(width, height)
        world.viewHalfW = view.vw / 2f
        world.viewHalfH = view.vh / 2f
        joystick.setBounds(0f, 0f, uiW, uiH)
        hud.setBounds(0f, 0f, uiW, uiH)
        val top = Gdx.graphics.safeInsetTop / game.uiScale.toFloat()
        val right = Gdx.graphics.safeInsetRight / game.uiScale.toFloat()
        pauseButton.setBounds(uiW - 24f - right, uiH - top - 34f, 20f, 20f)
        overlay?.let { it.setSize(uiW, uiH); it.invalidateHierarchy() }
    }

    // ------------------------------------------------------------------ dongu
    override fun render(delta: Float) {
        val dt = delta.coerceAtMost(1f / 20f)
        processEvents()
        if (!paused && !frozen && overlay == null && world.state == RunState.PLAYING) {
            accumulator += dt
            var steps = 0
            while (accumulator >= STEP && steps < 4) {
                accumulator -= STEP
                steps++
                stepWorld()
                processEvents()
                if (world.state != RunState.PLAYING) break
            }
            if (steps == 4) accumulator = 0f
            renderer.update(dt)
            tutorialTick(dt)
        } else if (!paused && !frozen && overlay == null) {
            renderer.update(dt)
        }
        checkState()
        renderer.render(game.batch, view)
        stage.act(dt)
        stage.draw()
    }

    private fun stepWorld() {
        val b = bot
        val (mx, my) = if (b != null) {
            b.think(STEP)
            b.mx to b.my
        } else input()
        world.update(STEP, mx, my)
    }

    private fun input(): Pair<Float, Float> {
        var x = joystick.outX
        var y = joystick.outY
        val k = Gdx.input
        if (k.isKeyPressed(Input.Keys.A) || k.isKeyPressed(Input.Keys.LEFT)) x -= 1f
        if (k.isKeyPressed(Input.Keys.D) || k.isKeyPressed(Input.Keys.RIGHT)) x += 1f
        if (k.isKeyPressed(Input.Keys.W) || k.isKeyPressed(Input.Keys.UP)) y += 1f
        if (k.isKeyPressed(Input.Keys.S) || k.isKeyPressed(Input.Keys.DOWN)) y -= 1f
        return x to y
    }

    private fun processEvents() {
        val ev = world.events
        for (i in 0 until ev.count) {
            val type = ev.type[i]
            val x = ev.x[i]
            val y = ev.y[i]
            val av = ev.a[i]
            val bv = ev.b[i]
            renderer.onEvent(type, x, y, av, bv)
            when (type) {
                Ev.SFX -> {
                    val s = Sfx.entries[bv]
                    val vol = when (s) {
                        Sfx.HIT -> 0.45f
                        Sfx.EMBER -> 0.55f
                        Sfx.SPARK, Sfx.DAGGER -> 0.5f
                        Sfx.FLAME -> 0.4f
                        else -> 1f
                    }
                    game.audio.play(s, if (av > 0f) av else 1f, vol)
                }
                Ev.HIT -> game.audio.play(Sfx.HIT, 0.9f + (i % 5) * 0.05f, 0.4f)
                Ev.KILL -> {
                    game.audio.play(Sfx.ENEMY_DIE, 0.9f + (i % 7) * 0.04f, 0.6f)
                    val d = world.enemyDefs[bv]
                    if (d.tier != Tier.NORMAL || av > 0f) haptic(40)
                }
                Ev.PLAYER_HURT -> haptic(35)
                Ev.BOSS_WARNING -> hud.announce(t["hud.boss_warning"], UiKit.RED, 3.5f)
                Ev.MINIBOSS -> hud.announce(t["hud.miniboss"], UiKit.EMBER, 2.5f)
                Ev.BOSS_SPAWN -> {
                    val d = world.enemies.def[bv]
                    hud.bossName = d?.let { t["hud.boss_name.${it.id}"] }
                    game.audio.playMusic("boss")
                    haptic(80)
                }
                Ev.BOSS_DEATH -> haptic(120)
                Ev.DAWN -> {
                    hud.announce(t["hud.dawn"], UiKit.GOLD, 3f)
                    game.audio.playMusic(null)
                }
                Ev.EVOLVE -> haptic(60)
                Ev.LEVEL_UP -> haptic(25)
            }
        }
        ev.clear()
    }

    private fun haptic(ms: Int) {
        if (game.settings.vibration) game.platform.vibrate(ms)
    }

    private fun checkState() {
        if (overlay != null || paused || finished) return
        when (world.state) {
            RunState.LEVEL_UP -> {
                if (bot != null) {
                    world.upgrades.choose(bot!!.chooseOffer(world.upgrades.offers))
                    return
                }
                show(LevelUpOverlay(game, world) { overlayClosed() })
            }
            RunState.CHEST -> {
                if (bot != null) {
                    world.resume()
                    return
                }
                show(ChestOverlay(game, world) {
                    world.resume()
                    overlayClosed()
                })
            }
            RunState.DEAD -> {
                val canRevive = !world.adReviveUsed && !tutorial
                if (!canRevive || bot != null) {
                    world.giveUp()
                    return
                }
                game.audio.play(Sfx.DEFEAT, 1f, 0.6f)
                show(ReviveOverlay(game, onRevive = {
                    world.reviveByAd()
                    overlayClosed()
                }, onDecline = {
                    world.giveUp()
                    overlayClosed()
                }))
            }
            RunState.VICTORY, RunState.DEFEAT -> finish()
            RunState.PLAYING -> Unit
        }
    }

    private fun show(o: Overlay) {
        joystick.release()
        joystick.enabledInput = false
        overlay = o
        game.audio.duck(true)
        stage.addActor(o)
    }

    private fun overlayClosed() {
        overlay = null
        joystick.enabledInput = true
        game.audio.duck(false)
        accumulator = 0f
    }

    fun openPause() {
        if (paused || overlay != null || finished || world.state != RunState.PLAYING) return
        paused = true
        joystick.release()
        joystick.enabledInput = false
        game.audio.duck(true)
        val o = PauseOverlay(game, world, onResume = {
            paused = false
            overlayClosed()
        }, onGiveUp = {
            paused = false
            overlayClosed()
            world.giveUp()
        })
        overlay = o
        stage.addActor(o)
    }

    private fun finish() {
        finished = true
        game.audio.duck(false)
        game.audio.play(if (world.state == RunState.VICTORY) Sfx.VICTORY else Sfx.DEFEAT)
        if (tutorial) game.save.data.tutorialDone = true
        val summary = com.lastlantern.save.Progression.applyRun(game.save.data, world)
        game.save.save()
        game.go(ResultsScreen(game, world, summary))
    }

    // ------------------------------------------------------------------ egitim
    private fun tutorialTick(dt: Float) {
        if (!tutorial) return
        tutorialTimer += dt
        when (tutorialStep) {
            0 -> if (tutorialTimer > 0.6f) next("tut.move")
            1 -> if (joystick.movedDistance > 60f || tutorialTimer > 9f) next("tut.auto")
            2 -> if (tutorialTimer > 3.5f) next("tut.embers")
            3 -> if (world.player.level > 1 || tutorialTimer > 12f) next("tut.light")
            4 -> if (tutorialTimer > 4f) next("tut.dawn")
            5 -> if (tutorialTimer > 3.5f) {
                hud.hint = null
                tutorialStep++
            }
        }
    }

    private fun next(key: String) {
        hud.hint = t[key]
        tutorialStep++
        tutorialTimer = 0f
    }

    // ------------------------------------------------------------------ yasam dongusu
    override fun pause() {
        // Uygulama arka plana gecti: oyunu duraklat
        if (game.launch.capture == null) openPause()
    }

    override fun onBack() {
        if (overlay is PauseOverlay) {
            (overlay as PauseOverlay).close()
            paused = false
            overlayClosed()
        } else {
            openPause()
        }
    }

    override fun onKey(keycode: Int): Boolean {
        if (keycode == Input.Keys.P) {
            openPause()
            return true
        }
        return false
    }

    override fun dispose() {
        super.dispose()
        view.dispose()
    }

    companion object {
        const val STEP = 1f / 60f

        fun tutorial(game: LastLanternGame) = GameScreen(
            game,
            RunConfig(Content.stage("woods"), Content.character("keeper"), game.save.data.meta, supporter = game.supporter),
            tutorial = true,
        )

        fun start(game: LastLanternGame, stageId: String, endless: Boolean = false): GameScreen {
            val d = game.save.data
            val cfg = RunConfig(Content.stage(stageId), Content.character(d.selectedCharacter), HashMap(d.meta),
                seed = game.launch.seed ?: System.nanoTime(), endless = endless, supporter = game.supporter)
            return GameScreen(game, cfg)
        }
    }
}
