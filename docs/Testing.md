# Testing Cave-Runner

The repository includes a set of EditMode tests located under `Assets/Tests`. They can be run through the Unity Test Runner.

For setup prerequisites see [DeveloperSetup](DeveloperSetup.md).

## Running Tests in the Editor
1. Open **Window > General > Test Runner**.
2. Select the **EditMode** tab.
3. Click **Run All** to execute the tests.

## Command Line
Tests can also be triggered from the command line for continuous integration:

```bash
Unity -batchmode -projectPath <path> -runTests -testPlatform editmode \
  -testResults Results.xml -logFile test.log -quit
```

The above command writes an XML report and log file that can be archived by your CI system.

## Continuous Integration
EditMode tests are automatically executed on every push and pull request using
GitHub Actions. The workflow defined at `.github/workflows/unity-tests.yml`
installs Unity via the GameCI actions, runs the command shown above and uploads
the generated `TestResults` folder as an artifact. No manual steps are required
to verify that new commits keep the tests passing.

## What Is Covered?
- Jump and slide buffering to ensure responsive controls.
- Enhanced gravity logic for snappy jumps.
- Fast fall behaviour when holding the down input.
- Air dash impulse when sliding with horizontal input.
- Adaptive difficulty scaling based on analytics.

Each test creates lightweight GameObjects so they run quickly without requiring a scene.
