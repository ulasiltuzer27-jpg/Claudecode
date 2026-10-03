# Last Lantern (Son Fener)

Dikey ekranda tek başparmakla oynanan, "survivors-like" bir roguelite. Hedef
platform Android (Google Play).
libGDX 1.14 · Kotlin 2.4 · Gradle 8.14

## Modüller

| Modül | İçerik |
|---|---|
| `core/` | Oyunun tamamı (saf Kotlin/JVM). Burada derlenir ve test edilir. |
| `lwjgl3/` | Masaüstü başlatıcı: geliştirme, otomatik oynatma, ekran görüntüsü. |
| `android/` | Android başlatıcı, reklam/satın alma, manifest, ikonlar. |
| `tools/` | Varlık üreticileri (Python) ve atlas paketleyici. |
| `assets/` | Üretilmiş oyun varlıkları (atlas, font, ses, çeviriler). |

## Derleme

```bash
./gradlew :core:test                 # birim testleri + denge simülasyonu
./gradlew :lwjgl3:run                # masaüstünde oyna
./gradlew :android:assembleDebug     # Android SDK gerekir (ANDROID_HOME)
```

Android SDK yoksa `:android` modülü otomatik olarak devre dışı kalır. APK ve AAB
her push'ta GitHub Actions'ta derlenir (`.github/workflows/last-lantern-android.yml`).
