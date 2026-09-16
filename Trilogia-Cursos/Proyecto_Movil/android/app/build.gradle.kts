import java.util.Properties
import java.io.FileInputStream

plugins {
    id("com.android.application")
    id("kotlin-android")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

android {
    namespace = "cr.distribuidorajj.proyecto_movil"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = JavaVersion.VERSION_17.toString()
    }

    defaultConfig {
        // Definitivo desde el primer APK. Cambiarlo despues seria otra
        // aplicacion para el telefono: los choferes quedarian con dos
        // instaladas, y Google Play lo tratara como un producto distinto.
        applicationId = "cr.distribuidorajj.proyecto_movil"
        minSdk = flutter.minSdkVersion
        targetSdk = flutter.targetSdkVersion
        versionCode = flutter.versionCode
        versionName = flutter.versionName
    }

    // La firma sale de android/key.properties, que NO se versiona, o de
    // variables de entorno en el servidor de compilacion.
    //
    // El keystore es la identidad de la aplicacion: perderlo obliga a publicar
    // con otro applicationId y a que todos reinstalen. Hay que respaldarlo
    // fuera de este repositorio y fuera de GitHub.
    signingConfigs {
        create("release") {
            val propertiesFile = rootProject.file("key.properties")

            if (propertiesFile.exists()) {
                val keyProperties = Properties()
                keyProperties.load(FileInputStream(propertiesFile))

                keyAlias = keyProperties.getProperty("keyAlias")
                keyPassword = keyProperties.getProperty("keyPassword")
                storeFile = keyProperties.getProperty("storeFile")?.let { file(it) }
                storePassword = keyProperties.getProperty("storePassword")
            } else if (System.getenv("ANDROID_KEYSTORE_PATH") != null) {
                keyAlias = System.getenv("ANDROID_KEY_ALIAS")
                keyPassword = System.getenv("ANDROID_KEY_PASSWORD")
                storeFile = file(System.getenv("ANDROID_KEYSTORE_PATH"))
                storePassword = System.getenv("ANDROID_KEYSTORE_PASSWORD")
            }
        }
    }

    buildTypes {
        release {
            // Si no hay keystore configurado se firma con la clave de
            // depuracion, para que `flutter run --release` funcione en una
            // maquina de desarrollo. Un APK asi NO se distribuye.
            val releaseSigning = signingConfigs.getByName("release")
            signingConfig = if (releaseSigning.storeFile != null) {
                releaseSigning
            } else {
                signingConfigs.getByName("debug")
            }

            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
        }
    }
}

flutter {
    source = "../.."
}
