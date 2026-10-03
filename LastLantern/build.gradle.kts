buildscript {
    val androidEnabled = gradle.extra.has("androidEnabled") && gradle.extra["androidEnabled"] as Boolean
    repositories {
        gradlePluginPortal()
        mavenCentral()
        if (androidEnabled) google()
    }
    dependencies {
        val kotlinVersion = project.property("kotlinVersion") as String
        classpath("org.jetbrains.kotlin:kotlin-gradle-plugin:$kotlinVersion")
        classpath("org.jetbrains.kotlin:kotlin-serialization:$kotlinVersion")
        if (androidEnabled) {
            classpath("com.android.tools.build:gradle:${project.property("agpVersion")}")
        }
    }
}

allprojects {
    version = providers.gradleProperty("VERSION_NAME").get()
}
