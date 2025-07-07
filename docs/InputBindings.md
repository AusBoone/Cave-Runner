# Input Manager

This guide explains how player input is handled in **Cave-Runner** and how to customize controls.

## Overview
- `InputManager` supports both the legacy `KeyCode` based input and Unity's newer Input System package.
- Xbox and PlayStation controllers work out of the box with either input mode.
- Actions for jump, slide, down/fast‑fall, horizontal movement and pause are exposed.
- Key bindings are stored in `PlayerPrefs` so changes persist between sessions.

## Rebinding Keys

1. Call `InputManager.StartRebindJump`, `StartRebindSlide`, `StartRebindDown` or `StartRebindPause` from a UI button.
2. The methods start an interactive rebinding operation that waits for the player to press a new control.
3. The selected binding is saved automatically when the operation completes.

If the Input System package is not installed the game falls back to the default `KeyCode` assignments defined in `InputManager`.

For the legacy input manager you can change keys directly:

```csharp
InputManager.SetMoveLeftKey(KeyCode.A);
InputManager.SetMoveRightKey(KeyCode.D);
InputManager.SetJumpKey(KeyCode.Space);
```

## Querying Input
Use the accessor methods to check controls each frame:

```csharp
if (InputManager.GetJumpDown()) { /* start jump */ }
if (InputManager.GetDown()) { /* apply fast fall */ }
if (InputManager.GetSlideUp()) { /* cancel slide */ }
float x = InputManager.GetHorizontal(); // -1..1 for left/right
```

These helpers abstract away the details of which input method is active.

## Mobile Touch Controls

When building for mobile platforms you can provide on-screen buttons for jump,
slide and pause. The simplified `MobileUI.prefab` lives under `Resources/UI` and
is instantiated automatically by `UIManager` on mobile devices. It exposes three
large buttons hooked up through `TouchInputManager`. You can also drop the
legacy `MobileControls.prefab` into your scene if you prefer manual placement.

The buttons invoke `OnJumpDown`/`OnJumpUp`, `OnSlideDown`/`OnSlideUp` and
`OnPause` on `TouchInputManager` so the existing input queries work unchanged.

