import 'dart:convert';

import 'package:shared_preferences/shared_preferences.dart';

import 'progress.dart';

/// Ilerlemenin nerede saklandigi. Testlerde [MemoryProgressStore].
abstract class ProgressStore {
  Future<Progress> load();
  Future<void> save(Progress progress);
}

class PrefsProgressStore implements ProgressStore {
  static const _key = 'progress_v1';

  SharedPreferences? _prefs;

  Future<SharedPreferences> get _p async => _prefs ??= await SharedPreferences.getInstance();

  @override
  Future<Progress> load() async {
    final raw = (await _p).getString(_key);
    if (raw == null) return Progress();
    try {
      return Progress.fromJson((jsonDecode(raw) as Map).cast<String, Object?>());
    } catch (_) {
      // Bozuk kayit oyunu kilitlemesin; sifirdan basla.
      return Progress();
    }
  }

  @override
  Future<void> save(Progress progress) async {
    await (await _p).setString(_key, jsonEncode(progress.toJson()));
  }
}

class MemoryProgressStore implements ProgressStore {
  MemoryProgressStore([Progress? initial]) : _json = initial?.toJson();

  Map<String, Object?>? _json;
  int saves = 0;

  @override
  Future<Progress> load() async =>
      _json == null ? Progress() : Progress.fromJson(jsonDecode(jsonEncode(_json)) as Map<String, Object?>);

  @override
  Future<void> save(Progress progress) async {
    saves++;
    _json = progress.toJson();
  }
}
