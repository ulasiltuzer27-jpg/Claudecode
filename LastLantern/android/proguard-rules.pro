# libGDX: derleme zamaninda kullanilan, calisma zamaninda olmayan siniflar.
-dontwarn com.badlogic.gdx.backends.android.AndroidFragmentApplication
-dontwarn com.badlogic.gdx.utils.GdxBuild
-dontwarn com.badlogic.gdx.jnigen.**

# Native metotlar (JNI) adiyla baglanir; yeniden adlandirilmamali.
-keepclasseswithmembernames class * { native <methods>; }

# kotlinx.serialization: @Serializable siniflarin uretilmis serializer'lari.
-keepattributes *Annotation*, InnerClasses
-dontnote kotlinx.serialization.**
-keepclassmembers @kotlinx.serialization.Serializable class com.lastlantern.** {
    *** Companion;
    *** INSTANCE;
    kotlinx.serialization.KSerializer serializer(...);
}
-keepclasseswithmembers class com.lastlantern.** {
    kotlinx.serialization.KSerializer serializer(...);
}
