# SenzAI

Senzai, an ai sensei to train your game sensitivity. A 3D aim trainer built in Unity that figures out your optimal mouse sensitivity using Bayesian optimization — no guessing, no spreadsheets, just shoot stuff and let it do the math.

---

## What it does

You play, it watches. Every shot you take gets scored based on how close you were to the target, how fast you reacted, and how far away the target was. Over time, a Gaussian Process model fits a curve over all those scores across the sensitivity range and figures out where your personal peak is.

Hit `ESC` in-game to open the panel. Hit Analyze. It'll tell you your optimal sensitivity, a 90% confidence interval, and the equivalent setting for Valorant, CS2, Apex Legends, and Overwatch 2.

Sessions are saved as JSON files so the model gets smarter the more you play. It also decays old data slightly each session so it doesn't get stuck on outdated results.

---

## Features

- FPS movement — WASD, sprint, jump, the usual
- Targets spawn at randomized distances and positions, respawn after being hit
- Every shot logs: hit/miss, distance, aim deviation, reaction time, mouse speed, crosshair offset X/Y, target angular size
- Bayesian sensitivity optimizer (Gaussian Process + Expected Improvement, no external libraries — all the math is done in C#)
- Real-time stats HUD: accuracy, shots, session timer
- Sensitivity converter with calibration scale for Windows DPI scaling differences
- Head bob, landing tilt, sprint FOV, camera shake, bullet tracers
- Procedurally generated map
- Python companion script (`analysis/analyze_sensitivity.py`) for offline analysis and sklearn validation

---

## How to use

1. Open the project in Unity 6
2. Open `SampleScene`
3. Hit Play
4. Shoot targets for a few minutes (the optimizer needs at least 10 shots to start)
5. Press `ESC` → click **Analyze**
6. If you have enough data it'll show a recommended sensitivity and the cross-game equivalents
7. Hit **Apply** to try it, **Reset** to start fresh

The more sessions you play, the more confident the recommendation gets. The convergence % in the panel tells you how much uncertainty is left in the model.

---

## Notes on the sensitivity converter

The cross-game conversion uses a calibration scale (default 0.5) to correct for the difference between Unity's mouse input and raw input games like Valorant/CS2. If the values seem off, tweak `Calibration Scale` on the `SensitivityUI` component in the Inspector:

- 100% Windows display scaling → set to `1.0`
- 200% (4K, common on 1440p+) → set to `0.5`

---

## Project structure

```
Assets/Scripts/
  PlayerMovement.cs        — WASD + jump
  MouseLook.cs             — mouse camera control
  PlayerEffects.cs         — head bob, landing tilt, sprint FOV, footsteps
  ShootingController.cs    — raycast shooting + tracer + data logging calls
  TargetController.cs      — target hit/flash/respawn
  TargetSpawner.cs         — spawns targets at varied distances
  SensitivityOptimizer.cs  — Gaussian Process Bayesian optimizer (the main thing)
  SensitivityCalibrator.cs — manages sensitivity during a training session
  SensitivityUI.cs         — ESC panel, analyze/apply/reset buttons, converter
  SensitivityConverter.cs  — standalone cross-game sensitivity converter UI
  SessionLogger.cs         — tracks shots per session, saves to JSON
  ShotData.cs              — data class for a single shot
  SessionData.cs           — data class for a full session
  SessionStats.cs          — accuracy/distance stats calculation
  MapGenerator.cs          — procedural level generator
  CameraShake.cs           — recoil shake on fire
  CrossHair.cs             — crosshair feedback on hit/miss
  StatsDisplay.cs          — real-time HUD
  MainMenuController.cs    — main menu

analysis/
  analyze_sensitivity.py   — companion script, reads saved JSON + optimizer model,
                             optionally runs sklearn GradientBoosting to cross-validate
```

---

## Dependencies

- Unity 6.0+
- Unity Input System package
- TextMeshPro
- Python 3 + numpy + scikit-learn (optional, for the analysis script only)

---

## Credits

**Adithya Santhosh P** — everything

Movement and camera reference: [Unity FPSSample](https://github.com/Unity-Technologies/FPSSample) by Unity Technologies (MIT License). The FPS controller architecture (CharacterController-based movement, mouse look separation) was studied from that project. No source files were copied directly.

Gaussian Process math reference: Wikipedia — Gaussian process regression, and a few YouTube explanations of Bayesian optimization. The Cholesky decomposition and Expected Improvement logic were implemented from scratch in C# based on those references.

---

## License

MIT License

Copyright (c) 2026 Adithya Santhosh P

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.