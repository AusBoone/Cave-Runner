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

