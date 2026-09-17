# Flutter y sus plugins se registran por reflexion: ofuscar sus nombres rompe
# el arranque. El resto del codigo Kotlin si se reduce y ofusca.
-keep class io.flutter.** { *; }
-keep class io.flutter.plugins.** { *; }
-keep class com.it_nomads.fluttersecurestorage.** { *; }
-dontwarn io.flutter.embedding.**
