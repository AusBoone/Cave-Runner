# Graphics Settings

This project targets lightweight 2D visuals so it can run on a wide range of hardware.
The default setup uses Unity's built‑in renderer with no post processing.

- **Resolution** – 1920x1080 is recommended but the game is not capped and will
  adapt to the screen size selected in the player settings.
- **Frame Rate** – VSync is enabled by default so the maximum frame rate matches
  the monitor refresh rate. Disable VSync in the Quality settings if you need a
  fixed value.
- **Quality Levels** – Only the Low and High levels are included. Most effects
  are inexpensive so Low disables parallax backgrounds and reduces particle
  counts while High enables all visuals.

Adjust these options via *Edit > Project Settings > Quality* in Unity to balance
performance for your target devices.
