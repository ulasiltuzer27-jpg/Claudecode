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

android {
    namespace = "com.baglantidedektifi.baglanti_dedektifi"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
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
