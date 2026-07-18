import java.util.Properties

plugins {
    id("com.android.application") version "8.7.3"
    id("org.jetbrains.kotlin.android") version "1.9.25"
}

// Release signing is driven by a gitignored app/keystore.properties (see keystore.properties.example).
// Absent (fresh clone, debug-only work), the release build simply carries no signing config.
val keystorePropsFile = rootProject.file("keystore.properties")
val keystoreProps = Properties().apply {
    if (keystorePropsFile.exists()) keystorePropsFile.inputStream().use { load(it) }
}

android {
    namespace = "com.vandaemon.shell"
    compileSdk = 34

    defaultConfig {
        // Head-unit (005) keeps its sideload id; the tablet flavor overrides it below.
        applicationId = "com.vandaemon.shell"
        minSdk = 29          // provisional — confirm against the §8 on-hardware fingerprint (FR-013)
        targetSdk = 34
        versionCode = 1
        versionName = "0.1.0"
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    signingConfigs {
        create("release") {
            if (keystorePropsFile.exists()) {
                storeFile = file(keystoreProps.getProperty("storeFile"))
                storePassword = keystoreProps.getProperty("storePassword")
                keyAlias = keystoreProps.getProperty("keyAlias")
                keyPassword = keystoreProps.getProperty("keyPassword")
            }
        }
    }

    // One dimension: which VanDaemon device this build targets.
    flavorDimensions += "target"
    productFlavors {
        // Existing 005 launcher shell: bundled UI + vehicle bridge, sideloaded.
        create("headunit") {
            dimension = "target"
        }
        // v1 tablet UI-launcher: loads the live Pi UI over LAN, no bridge. Play Store target.
        create("tablet") {
            dimension = "target"
            applicationId = "dev.vandaemon.ui"
            versionName = "0.1.0"
        }
    }

    buildTypes {
        getByName("debug") {
            isMinifyEnabled = false
        }
        getByName("release") {
            isMinifyEnabled = false
            if (keystorePropsFile.exists()) {
                signingConfig = signingConfigs.getByName("release")
            }
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }

    testOptions {
        unitTests.isReturnDefaultValues = true
    }

    androidResources {
        // CRITICAL: Android's default asset-ignore pattern drops directories starting with
        // '_' (the "<dir>_*" token). Blazor's runtime lives in _framework/ and Razor class
        // library assets in _content/ — both would be silently excluded from the APK, so the
        // hosted UI could never boot. Override the pattern to keep underscore dirs while
        // still ignoring VCS/editor junk. Do NOT remove this.
        ignoreAssetsPattern = "!.svn:!.git:!.ds_store:!*.scc:!CVS:!thumbs.db:!picasa.ini:!*~"
    }

    // Sources live under src/<set>/kotlin (FR-013 layout).
    sourceSets {
        getByName("main") { java.srcDirs("src/main/kotlin") }
        getByName("test") { java.srcDirs("src/test/kotlin") }
        getByName("androidTest") { java.srcDirs("src/androidTest/kotlin") }
        getByName("tablet") { java.srcDirs("src/tablet/kotlin") }
    }
}

dependencies {
    implementation("androidx.core:core-ktx:1.13.1")
    implementation("androidx.webkit:webkit:1.12.1")

    testImplementation("junit:junit:4.13.2")

    androidTestImplementation("androidx.test:core:1.6.1")
    androidTestImplementation("androidx.test:core-ktx:1.6.1")
    androidTestImplementation("androidx.test.ext:junit:1.2.1")
    androidTestImplementation("androidx.test:runner:1.6.2")
    androidTestImplementation("androidx.test:rules:1.6.1")
}
