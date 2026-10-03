import org.jetbrains.kotlin.gradle.dsl.JvmTarget

plugins {
    kotlin("jvm")
    kotlin("plugin.serialization")
}

val gdxVersion: String by project
val serializationVersion: String by project

java {
    sourceCompatibility = JavaVersion.VERSION_17
    targetCompatibility = JavaVersion.VERSION_17
}

kotlin {
    compilerOptions { jvmTarget.set(JvmTarget.JVM_17) }
}

dependencies {
    api("com.badlogicgames.gdx:gdx:$gdxVersion")
    api("org.jetbrains.kotlinx:kotlinx-serialization-json:$serializationVersion")

    testImplementation(platform("org.junit:junit-bom:5.13.4"))
    testImplementation("org.junit.jupiter:junit-jupiter")
    testRuntimeOnly("org.junit.platform:junit-platform-launcher")
    testImplementation("com.badlogicgames.gdx:gdx-backend-headless:$gdxVersion")
    testImplementation("com.badlogicgames.gdx:gdx-platform:$gdxVersion:natives-desktop")
}

tasks.test {
    useJUnitPlatform {
        // Denge olcumu uzun surer; yalnizca -Pprobe ile calisir.
        if (!project.hasProperty("probe")) excludeTags("probe")
    }
    systemProperty("probe.stages", (project.findProperty("probeStages") ?: "woods").toString())
    // Testler assets/ klasorunu (i18n, font) gercek dosyalar uzerinden dogrular.
    workingDir = rootProject.file("assets")
    maxHeapSize = "1g"
    testLogging {
        events("failed", "skipped")
        showStandardStreams = true
        exceptionFormat = org.gradle.api.tasks.testing.logging.TestExceptionFormat.FULL
    }
}
