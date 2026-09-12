plugins {
    id("com.android.application")
    id("com.google.gms.google-services")

    // The Flutter Gradle Plugin must be applied after
    // the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

android {
    namespace =
        "com.crediplus.crediplus_authenticator"

    compileSdk =
        flutter.compileSdkVersion

    ndkVersion =
        flutter.ndkVersion

    compileOptions {
        sourceCompatibility =
            JavaVersion.VERSION_17

        targetCompatibility =
            JavaVersion.VERSION_17
    }

    defaultConfig {
        applicationId =
            "com.crediplus.crediplus_authenticator"

        minSdk =
            flutter.minSdkVersion

        targetSdk =
            flutter.targetSdkVersion

        versionCode =
            flutter.versionCode

        versionName =
            flutter.versionName
    }

    buildTypes {
        release {
            signingConfig =
                signingConfigs
                    .getByName("debug")
        }
    }
}

kotlin {
    compilerOptions {
        jvmTarget =
            org.jetbrains.kotlin.gradle.dsl
                .JvmTarget
                .JVM_17
    }
}

dependencies {

    implementation(
        "androidx.biometric:biometric:1.1.0"
    )

    implementation(
        platform(
            "com.google.firebase:firebase-bom:34.3.0"
        )
    )

    implementation(
        "com.google.firebase:firebase-messaging"
    )
}

flutter {
    source = "../.."
}