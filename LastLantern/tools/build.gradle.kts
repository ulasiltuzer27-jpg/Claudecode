plugins {
    java
}

val gdxVersion: String by project

dependencies {
    implementation("com.badlogicgames.gdx:gdx-tools:$gdxVersion")
}

// Python ureticilerinin yazdigi tekil PNG'leri (tools/build/sprites) tek bir
// atlas'ta toplar. Ayarlar tools/sprites/pack.json'da.
tasks.register<JavaExec>("packAtlas") {
    group = "assets"
    description = "tools/build/sprites -> assets/atlas/game.atlas"
    classpath = sourceSets["main"].runtimeClasspath
    mainClass.set("com.badlogic.gdx.tools.texturepacker.TexturePacker")
    val input = rootProject.file("tools/build/sprites")
    val output = rootProject.file("assets/atlas")
    args(input.absolutePath, output.absolutePath, "game")
    // AWT'nin X sunucusu aramasini engeller (Xvfb olmadan da calissin)
    jvmArgs("-Djava.awt.headless=true")
}
