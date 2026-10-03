// Android SDK yoksa (ör. dl.google.com'a erisimi olmayan bir ortam) :android
// modulu hic dahil edilmez; core + masaustu yine derlenir, test edilir ve
// calistirilir. CI'da (GitHub Actions) ANDROID_HOME tanimli oldugu icin APK/AAB
// orada uretilir.
val localProps = file("local.properties")
val androidEnabled = System.getenv("ANDROID_HOME") != null ||
    System.getenv("ANDROID_SDK_ROOT") != null ||
    (localProps.exists() && localProps.readText().contains("sdk.dir"))

pluginManagement {
    repositories {
        gradlePluginPortal()
        mavenCentral()
    }
}

dependencyResolutionManagement {
    repositories {
        mavenCentral()
        // google() sadece Android etkinken: erisilemeyen bir depo Gradle'da
        // "bulunamadi" degil "hata" sayilir ve tum derlemeyi durdurur.
        if (androidEnabled) google()
    }
}

rootProject.name = "LastLantern"
gradle.extra["androidEnabled"] = androidEnabled

include(":core", ":lwjgl3", ":tools")
if (androidEnabled) include(":android")
