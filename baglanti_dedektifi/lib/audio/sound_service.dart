import 'package:audioplayers/audioplayers.dart';
import 'package:flutter/foundation.dart';

/// Oyun sesleri. Dosyalar `tool/audio/synth_sfx.py` ile uretilir.
enum Sfx {
  pick('sfx_pick'),
  thread('sfx_thread'),
  correct('sfx_correct'),
  wrong('sfx_wrong'),
  stamp('sfx_stamp'),
  star('sfx_star'),
  coin('sfx_coin'),
  caseSolved('sfx_case_solved'),
  page('sfx_page');

  const Sfx(this.file);

  final String file;

  String get asset => 'audio/$file.ogg';
}

abstract class SoundService {
  Future<void> init({required bool sfx, required bool music});

  void play(Sfx sfx);

  void setSfxEnabled(bool on);

  void setMusicEnabled(bool on);

  /// Uygulama arka plana gecince.
  void pauseMusic();

  void resumeMusic();
}

/// Testler icin: hic ses calmaz, cagrilari kaydeder.
class SilentSoundService implements SoundService {
  final List<Sfx> played = [];

  @override
  Future<void> init({required bool sfx, required bool music}) async {}

  @override
  void play(Sfx sfx) => played.add(sfx);

  @override
  void setSfxEnabled(bool on) {}

  @override
  void setMusicEnabled(bool on) {}

  @override
  void pauseMusic() {}

  @override
  void resumeMusic() {}
}

/// audioplayers ile gercek ses. Kisa efektler havuzdan, ambiyans dongude.
class AudioplayersSoundService implements SoundService {
  static const _musicAsset = 'audio/music_rain.ogg';
  static const _musicVolume = 0.35;

  final Map<Sfx, AudioPool> _pools = {};
  AudioPlayer? _music;
  bool _sfxOn = true;
  bool _musicOn = true;
  bool _musicPlaying = false;
  bool _paused = false;

  @override
  Future<void> init({required bool sfx, required bool music}) async {
    _sfxOn = sfx;
    _musicOn = music;
    try {
      // Kullanicinin arka planda calan muzigini durdurma: onunla karis.
      await AudioPlayer.global.setAudioContext(
        AudioContextConfig(focus: AudioContextConfigFocus.mixWithOthers)
            .build(),
      );
    } catch (e) {
      debugPrint('Ses baglami ayarlanamadi: $e');
    }
    for (final s in Sfx.values) {
      try {
        _pools[s] = await AudioPool.create(
          source: AssetSource(s.asset),
          maxPlayers: s == Sfx.pick || s == Sfx.thread ? 4 : 2,
          playerMode: kIsWeb ? PlayerMode.mediaPlayer : PlayerMode.lowLatency,
        );
      } catch (e) {
        debugPrint('Ses yuklenemedi (${s.file}): $e');
      }
    }
    await _startMusicIfWanted();
  }

  Future<void> _startMusicIfWanted() async {
    if (!_musicOn || _musicPlaying || _paused) return;
    try {
      final player = _music ??= AudioPlayer();
      await player.setReleaseMode(ReleaseMode.loop);
      await player.play(AssetSource(_musicAsset), volume: _musicVolume);
      _musicPlaying = true;
    } catch (e) {
      // Web'de tarayici, kullanici dokunmadan sesi engeller; ilk efektte
      // yeniden denenir.
      debugPrint('Ambiyans baslatilamadi: $e');
    }
  }

  @override
  void play(Sfx sfx) {
    if (!_musicPlaying) _startMusicIfWanted();
    if (!_sfxOn) return;
    final pool = _pools[sfx];
    if (pool == null) return;
    pool.start(volume: sfx == Sfx.pick ? 0.6 : 0.9).catchError((Object e) {
      debugPrint('Ses calinamadi (${sfx.file}): $e');
      return () async {};
    });
  }

  @override
  void setSfxEnabled(bool on) => _sfxOn = on;

  @override
  void setMusicEnabled(bool on) {
    _musicOn = on;
    if (on) {
      _startMusicIfWanted();
    } else {
      _music?.stop();
      _musicPlaying = false;
    }
  }

  @override
  void pauseMusic() {
    _paused = true;
    if (_musicPlaying) _music?.pause();
  }

  @override
  void resumeMusic() {
    _paused = false;
    if (!_musicOn) return;
    if (_musicPlaying) {
      _music?.resume();
    } else {
      _startMusicIfWanted();
    }
  }
}
