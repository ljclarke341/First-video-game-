# Turning the game into an Android app

The game is plain HTML/CSS/JS. Capacitor wraps it in a native Android shell so
Play can distribute it. You need a real computer for this part — Android
Studio can't run in a browser.

## What to install first

| Tool | Notes |
|---|---|
| **Node.js 20+** | https://nodejs.org |
| **Android Studio** | https://developer.android.com/studio — includes the Android SDK |
| **JDK 21** | Android Studio bundles one; Capacitor 7+ requires 21 |

In Android Studio, open **SDK Manager** and install the **Android 16 (API 36)**
SDK platform. Play requires new apps to target API 36 (see PUBLISHING.md).

## One-time setup

```bash
git clone <your repo>
cd First-video-game-
npm install @capacitor/core @capacitor/cli @capacitor/android
npx cap add android
```

`capacitor.config.json` is already written. **Change `appId` before you run
`cap add`** — `com.yourname.chromarush` must become something you own, e.g.
`com.jordanclarke.chromarush`. It is permanent: once an app is published under
an application ID, that ID can never be reused or changed.

## The build loop

```bash
npm run dev      # play at http://localhost:5173 in a browser
npx cap sync     # copy www/ into the Android project after any change
npx cap open android
```

Then press **Run** in Android Studio with a device or emulator selected.

Because the game is just web files, 95% of your iteration happens in the
browser with `npm run dev`. Only re-sync when you want it on a device.

## Lock the app to portrait

In `android/app/src/main/AndroidManifest.xml`, on the `<activity>` tag:

```xml
android:screenOrientation="portrait"
```

## Set the target API level

Open `android/variables.gradle` and make sure:

```gradle
minSdkVersion = 23
compileSdkVersion = 36
targetSdkVersion = 36
```

## App icon

`store/icon-1024.png` is your master. In Android Studio:
**right-click `app` → New → Image Asset**, choose the PNG, and it generates
every density plus the adaptive icon. Use `#06060d` as the background colour.

## Building the file you upload

Play takes an **AAB** (Android App Bundle), not an APK.

1. **Build → Generate Signed App Bundle / APK → Android App Bundle**
2. **Create new…** keystore. Save the `.jks` file and its passwords somewhere
   you will still have in five years.
3. Build **release**.

Output: `android/app/release/app-release.aab`.

> **Back up the keystore.** Losing it means you can never update the app again
> under that listing. Play App Signing gives you a recovery path, but only if
> you enrolled before you lost it. The `.gitignore` here deliberately excludes
> `*.jks` and `*.keystore` so you never commit them.
