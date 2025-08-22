# Developer Setup

A step-by-step guide for configuring the Cave-Runner Unity project for local development.

## Unity Version
- **Unity 2022.3 LTS** is the recommended editor release. Other versions are untested.

## Mandatory Packages
These packages must be installed through Unity's Package Manager:
- **Input System** – modern input API used by `InputManager` and gameplay scripts.
- **Addressables** – handles asynchronous asset loading and memory-friendly content delivery.
- **Unity Test Framework** – ships by default and is required to run the project's Edit Mode tests.

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

