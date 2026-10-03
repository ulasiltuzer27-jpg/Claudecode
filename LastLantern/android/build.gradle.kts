import org.jetbrains.kotlin.gradle.dsl.JvmTarget

plugins {
    id("com.android.application")
    kotlin("android")
}

val gdxVersion: String by project
val natives: Configuration by configurations.creating

fun prop(name: String): String = providers.gradleProperty(name).get()

android {
    namespace = "com.lastlantern.android"
    compileSdk = 36

    defaultConfig {
        applicationId = prop("APP_ID")
        minSdk = 24
        targetSdk = 36
        versionCode = prop("VERSION_CODE").toInt()
        versionName = prop("VERSION_NAME")
        manifestPlaceholders["admobAppId"] = prop("ADMOB_APP_ID")
        buildConfigField("String", "ADMOB_REWARDED_ID", "\"${prop("ADMOB_REWARDED_ID")}\"")
    }

    sourceSets["main"].apply {
        assets.srcDirs(rootProject.file("assets"))
        jniLibs.srcDirs("libs")
    }

    buildFeatures {
        buildConfig = true
    }

    // Upload anahtari depoda DEGIL; CI secret'larindan ya da yerel ortam
    // degiskenlerinden gelir. Yoksa release AAB imzasiz uretilir (yine de
    // derlemenin dogrulugunu kanitlar).
    val keystorePath = System.getenv("LL_KEYSTORE_PATH")
    val releaseSigning = if (keystorePath != null && file(keystorePath).exists()) {
        signingConfigs.create("release") {
            storeFile = file(keystorePath)
            storePassword = System.getenv("LL_KEYSTORE_PASSWORD")
            keyAlias = System.getenv("LL_KEY_ALIAS")
            keyPassword = System.getenv("LL_KEY_PASSWORD")
        }
    } else null

    buildTypes {
        getByName("release") {
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
            signingConfig = releaseSigning
        }
        getByName("debug") {
            applicationIdSuffix = ".debug"
            versionNameSuffix = "-debug"
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    packaging {
        // Sikistirilmamis ve 16 KB hizali .so (Android 15+ / Play zorunlulugu)
        jniLibs { useLegacyPackaging = false }
    }

    lint {
        abortOnError = true
        checkReleaseBuilds = true
        // Ceviri eksikligini derleme hatasi saymak yerine uyarida birak
        warning += "MissingTranslation"
    }
}

kotlin {
    compilerOptions { jvmTarget.set(JvmTarget.JVM_17) }
}

dependencies {
    implementation(project(":core"))
    implementation("com.badlogicgames.gdx:gdx-backend-android:$gdxVersion")
    natives("com.badlogicgames.gdx:gdx-platform:$gdxVersion:natives-armeabi-v7a")
    natives("com.badlogicgames.gdx:gdx-platform:$gdxVersion:natives-arm64-v8a")
    natives("com.badlogicgames.gdx:gdx-platform:$gdxVersion:natives-x86")
    natives("com.badlogicgames.gdx:gdx-platform:$gdxVersion:natives-x86_64")
}

// libGDX'in .so dosyalari jar icinde gelir; Android bunlari libs/<abi>/ altinda bekler.
val copyAndroidNatives by tasks.registering {
    val outRoot = file("libs")
    inputs.files(natives)
    outputs.dir(outRoot)
    doLast {
        natives.files.forEach { jar ->
            val abi = jar.nameWithoutExtension.substringAfterLast("natives-")
            val outputDir = File(outRoot, abi)
            outputDir.mkdirs()
            project.copy {
                from(project.zipTree(jar))
                into(outputDir)
                include("*.so")
            }
        }
    }
}

tasks.matching { it.name.contains("merge") && it.name.contains("JniLibFolders") }.configureEach {
    dependsOn(copyAndroidNatives)
}
