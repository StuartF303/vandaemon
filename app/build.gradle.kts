plugins {
    id("com.android.application") version "8.7.3"
    id("org.jetbrains.kotlin.android") version "1.9.25"
}

android {
    namespace = "com.vandaemon.shell"
    compileSdk = 34

    defaultConfig {
        applicationId = "com.vandaemon.shell"
        minSdk = 29          // provisional — confirm against the §8 on-hardware fingerprint (FR-013)
        targetSdk = 34
        versionCode = 1
        versionName = "0.1.0"
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    buildTypes {
        getByName("debug") {
            isMinifyEnabled = false
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
