import java.util.Properties

plugins {
    id("com.android.application")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

// Yayin imzasi: android/key.properties (depoya GIRMEZ, bkz. docs/play_store.md).
// Dosya yoksa release derleme debug anahtariyla imzalanir; bu yalnizca
// telefonda denemek icindir, Play Store'a yuklenemez.
val keystoreProperties = Properties().apply {
    val file = rootProject.file("key.properties")
    if (file.exists()) file.inputStream().use { load(it) }
}
val hasReleaseKey = keystoreProperties.getProperty("storeFile") != null

// AdMob uygulama kimligi. Varsayilan: Google'in test kimligi.
// Yayinda: ./gradlew ... -PadmobAppId=ca-app-pub-XXXX~YYYY ya da
// android/gradle.properties icine admobAppId=...
val admobAppId: String =
    (project.findProperty("admobAppId") as String?) ?: "ca-app-pub-3940256099942544~3347511713"

// Google Play Games uygulama kimligi (yalnizca rakamlar). Bos birakilirsa
// MainActivity Play Games SDK'sini hic baslatmaz.
// Yayinda: -PplayGamesAppId=123456789012 ve --dart-define=PLAY_GAMES=true
val playGamesAppId: String = (project.findProperty("playGamesAppId") as String?) ?: ""

android {
    namespace = "com.baglantidedektifi.baglanti_dedektifi"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        // flutter_local_notifications zamanlanmis bildirimler icin gerekli.
        isCoreLibraryDesugaringEnabled = true
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    defaultConfig {
        // Play Store'daki kalici kimlik: ilk yuklemeden sonra DEGISTIRILEMEZ.
        applicationId = "com.baglantidedektifi.app"
        minSdk = flutter.minSdkVersion
        targetSdk = flutter.targetSdkVersion
        versionCode = flutter.versionCode
        versionName = flutter.versionName
        manifestPlaceholders["admobAppId"] = admobAppId
        resValue("string", "play_games_app_id", playGamesAppId)
    }

    signingConfigs {
        if (hasReleaseKey) {
            create("release") {
                keyAlias = keystoreProperties.getProperty("keyAlias")
                keyPassword = keystoreProperties.getProperty("keyPassword")
                storeFile = file(keystoreProperties.getProperty("storeFile"))
                storePassword = keystoreProperties.getProperty("storePassword")
            }
        }
    }

    buildFeatures {
        // play_games_app_id kaynagi resValue ile uretiliyor.
        resValues = true
    }

    // Release derlemesindeki "lintVital" denetimi, bazi eklentilerde (or.
    // in_app_purchase_android) JDK 17 ile Lint'in kendi hatasiyla cokuyor
    // (List.removeLast NoSuchMethodError). Lint gelistirmede ayrica calisir.
    lint {
        checkReleaseBuilds = false
    }

    buildTypes {
        release {
            signingConfig = if (hasReleaseKey) {
                signingConfigs.getByName("release")
            } else {
                signingConfigs.getByName("debug")
            }
        }
    }
}

kotlin {
    compilerOptions {
        jvmTarget = org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17
    }
}

flutter {
    source = "../.."
}

dependencies {
    coreLibraryDesugaring("com.android.tools:desugar_jdk_libs:2.1.4")
    // MainActivity'de PlayGamesSdk.initialize icin (games_services ile ayni surum).
    implementation("com.google.android.gms:play-services-games-v2:21.0.0")
}
