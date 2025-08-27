<!--
  DeveloperSetup.md
  ------------------
  Comprehensive instructions for setting up a local Cave Runner development
  environment. This guide covers required packages, optional tooling, save
  encryption variables, and platform-specific configuration steps. Maintainers
  updated this file to document save file encryption keys so new contributors
  can easily secure local builds. This revision also highlights persistence
  directory permissions and common I/O troubleshooting tips so developers avoid
  save failures.
-->

# Developer Setup

A step-by-step guide for configuring the Cave-Runner Unity project for local development.

## Unity Version
- **Unity 2022.3 LTS** is the recommended editor release. Other versions are untested.

## Mandatory Packages
These packages must be installed through Unity's Package Manager:
- **Input System** – modern input API used by `InputManager` and gameplay scripts.
- **Addressables** – handles asynchronous asset loading and memory-friendly content delivery.
- **Unity Test Framework** – ships by default and is required to run the project's Edit Mode tests.
- **TextMeshPro** – provides TMP_Text and TMP_Dropdown components for sharp UI rendering.

## Optional Tooling
Consider installing the following to streamline development:
- **Git LFS** for versioning large binary assets.
- **Rider** or **Visual Studio** for advanced C# editing and debugging.
- **Unity Hub CLI** for automated editor installs and project management.

## Opening the Project
1. Install **Unity 2022.3 LTS** via Unity Hub.
2. Clone the repository:
   ```bash
   git clone https://github.com/your-org/Cave-Runner.git
   ```
3. In Unity Hub choose **Open**, select the cloned folder, and let the editor import assets and packages.

## Enabling the Input System
1. Open **Window > Package Manager**.
2. Install the **Input System** package.
3. When Unity prompts to enable the new system, click **Yes**. Unity restarts and sets **Player Settings > Active Input Handling** to **Input System Package** (or **Both** if you need legacy support).

## Enabling Addressables
1. In **Package Manager**, install the **Addressables** package.
2. Go to **Window > Asset Management > Addressables > Groups**.
3. If prompted, create Addressables settings. Mark assets as addressable from the inspector and organize them into groups.
4. Build the addressable content via **Build > New Build > Default Build Script** to generate runtime data.

## Persistent Data Path Permissions
Unity stores save files in [`Application.persistentDataPath`](https://docs.unity3d.com/ScriptReference/Application-persistentDataPath.html).
Verify that the editor or built game can write to this location; otherwise,
serialization and optional encryption will fail. If you require secure storage,
configure the AES keys described in [Save Encryption](#save-encryption).

### I/O Permission Troubleshooting
- **`UnauthorizedAccessException`** – On Windows, the path may point inside
  **Program Files** or another protected directory. Move the project to a
  user-writable folder or run Unity with administrative privileges.
- **Read-only file system** – On macOS or Linux, inspect permissions with
  `ls -ld <path>` and adjust via `chmod` or relocate the project from
  read-only volumes.
- **Editor cannot write to path** – Avoid opening the project directly from a
  compressed archive or network share that enforces read-only mode.

## Save Encryption
When `CR_AES_KEY` and `CR_AES_IV` are provided, the game encrypts serialized
save data written to `Application.persistentDataPath`. Omit these variables if
plaintext saves suffice. Both of the following environment variables must be
present:

- `CR_AES_KEY` – Base64 string representing **32 bytes** (256‑bit key).
- `CR_AES_IV` – Base64 string representing **16 bytes** (initialization vector).

Example commands generate random values and export them for the current shell:

```bash
export CR_AES_KEY="$(openssl rand -base64 32)"
export CR_AES_IV="$(openssl rand -base64 16)"
```

On Windows PowerShell:

```powershell
$env:CR_AES_KEY = [Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }))
$env:CR_AES_IV  = [Convert]::ToBase64String((1..16 | ForEach-Object { Get-Random -Maximum 256 }))
```

If either variable is missing or cannot be decoded, the game logs a warning and
falls back to plaintext save files.

## Running Edit Mode Tests
1. Open **Window > General > Test Runner**.
2. Select the **EditMode** tab and click **Run All**.
3. From the command line, tests can be run with:
   ```bash
   Unity -batchmode -projectPath <path> -runTests -testPlatform editmode \
     -testResults Results.xml -logFile test.log -quit
   ```

## Building the Game
1. Open **File > Build Settings...**.
2. Click **Add Open Scenes** and select your target platform.
3. Press **Build** and choose an output directory. Unity exports the executable and required data files.

## Mobile Platform Setup

### Android Platform

#### Prerequisites
- Install **Android Build Support** (SDK, NDK, and OpenJDK) through Unity Hub when installing the Unity editor so all required toolchains are available.
- Ensure **ADB** is on your system path if deploying directly to a device. Android Studio is optional but supplies emulators and extra debugging tools.
- Test on hardware via USB with developer mode enabled or use Android Virtual Device images for emulator-based testing.

#### Build Steps
1. Open **File > Build Settings...**, choose **Android**, and click **Switch Platform**. Unity will reimport assets for the platform, which may take several minutes.
2. Under **Player Settings > Other Settings**:
   - Assign a unique **Package Name** such as `com.yourcompany.caverunner`.
   - Choose **IL2CPP** as the scripting backend and **ARM64** as the target architecture for 64‑bit devices.
3. In **Publishing Settings**, configure a **Keystore** to sign release packages; a debug keystore suffices for internal builds.
4. Press **Build** or **Build and Run** to generate an `.apk` or `.aab` file.

### iOS Platform

#### Prerequisites
- Use **macOS** with the latest **Xcode** installed to access Apple's build tools and simulators.
- Add **iOS Build Support** via Unity Hub when installing Unity 2022.3 LTS.
- An **Apple Developer** account is required for device provisioning; the free tier supports simulator builds only.

#### Build Steps
1. Open **File > Build Settings...**, select **iOS**, and click **Switch Platform** to create iOS-specific assets.
2. Under **Player Settings > Other Settings**:
   - Provide a unique **Bundle Identifier**.
   - Set **Architecture** to **ARM64** and **Scripting Backend** to **IL2CPP**.
3. Press **Build** to export an Xcode project and open it in Xcode.
4. In Xcode, choose a team and provisioning profile, then build and deploy to device or submit to TestFlight.

### Touch Input Configuration
- The project uses Unity's **Input System**; touch is exposed via the `Touchscreen` device.
- In your input actions, add bindings targeting `Primary Touch` or use a `Pointer` action map to support gestures.
- For gameplay scripts, enable enhanced touch support with:
  ```csharp
  using UnityEngine.InputSystem.EnhancedTouch;
  TouchSimulation.Enable();
  EnhancedTouchSupport.Enable();
  ```
- In the scene, ensure an **Event System** with **Input System UI Input Module** is present for UI touch handling.

### Mobile Troubleshooting
- **Android build fails with SDK/NDK errors** – Reinstall Android Build Support via Unity Hub and verify paths under **Preferences > External Tools**.
- **iOS project fails to compile** – Update Xcode and confirm your provisioning profile and signing certificate are valid.
- **Touches not detected** – Confirm **Active Input Handling** is set to **Input System Package** and that `EnhancedTouchSupport` is enabled before reading touches.
- **Device orientation or resolution incorrect** – Adjust **Player Settings > Resolution and Presentation** for the target device.
- **Gradle build timeouts** – Use a stable network connection and increase the `Gradle Daemon` timeout via `gradle.properties` if needed.

## Steamworks Setup
Follow these steps to enable Steam integration via the Steamworks.NET plugin:

1. **Install the Plugin**
   - Download and import the [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) Unity package.
   - After import, confirm that the `Steamworks` folder exists under `Assets/Plugins`.
   - The `SteamManager` script in `Assets/Scripts/SteamManager.cs` depends on this plugin for all Steam API calls.

2. **Place `steam_appid.txt`**
   - Create a file named `steam_appid.txt` containing your numeric Steam App ID.
   - For editor or test runs, place the file in the Unity project root (next to the `Assets` folder).
   - For standalone builds, ensure the file sits beside the compiled game executable.

3. **Editor and Test Considerations**
   - Steamworks only initializes if the Steam client is running and the App ID matches.
   - Edit Mode tests referencing `SteamManager` require the Steamworks plugin; otherwise mock or disable those tests.
   - When running the game outside Steam, ensure the `steam_appid.txt` is present or `SteamManager` will log an error and skip initialization.

### Troubleshooting
- **`Steamworks DLL not found`** – The plugin's native libraries are missing. Reimport the plugin and confirm files under `Assets/Plugins/Steamworks`.
- **`SteamAPI.Init()` returns false** – The Steam client may not be running or the App ID in `steam_appid.txt` is incorrect.
- **`steam_appid.txt` ignored** – Ensure the file resides next to the executable or project root; Steam ignores files in subdirectories.
- **Tests failing due to Steam initialization** – Mock Steam interfaces or wrap calls in `#if UNITY_STANDALONE` to bypass Steamworks in non-standalone environments.

