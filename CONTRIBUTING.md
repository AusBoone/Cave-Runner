# Contributing to Cave-Runner

<!--
  Contributing guidelines for the Cave-Runner project.
  This document now describes the automated GitHub Actions workflow
  that executes Unity EditMode tests and preserves the results.
-->

This document outlines guidelines for contributing to the Cave-Runner
Unity project. The repository intentionally avoids Node.js and npm
scripts; development relies solely on the Unity editor and its
command-line interface.

## Workflow

- Fork the repository and create your feature branch from `main`.
- Make changes within the Unity project and include explanatory comments where
  relevant.
- Run the EditMode tests through the Unity Test Runner or via the
  `Unity -batchmode` command.
- Push your branch. The `.github/workflows/tests.yml` workflow runs the same
  command on GitHub Actions, uploads `Results.xml` and `test.log` as
  artifacts, and fails when tests report errors.
- Submit a pull request describing your changes.

## Testing

Run the project's EditMode tests using Unity. The following command
demonstrates the required arguments:

```bash
Unity -batchmode -projectPath <path> -runTests -testPlatform editmode \
  -testResults Results.xml -logFile test.log -quit
```

The command writes a detailed log and XML results file, both of which should be
reviewed before opening a pull request. The GitHub Actions workflow
`tests.yml` executes this command automatically for every push and pull
request, ensuring consistent test coverage.

## Code Style

- Use descriptive names for classes and variables.
- Provide inline comments and summary headers explaining the purpose of each
  script.
- Validate inputs and raise clear errors for unexpected states.

Following these conventions keeps the project maintainable and
understandable for all contributors.
