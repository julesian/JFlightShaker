# JFlightShaker

**JFlightShaker** is a Windows application that maps throttle and stick inputs to
arcade-style flight-sim rumble effects using audio bass shakers.

I wanted something similar to the effects I get in **Microsoft Flight Simulator 2024**
with SimHub, but since SimHub can’t run custom bass-shaker effects while
**Project Wingman** or **Ace Combat** is running, I built my own solution.

Instead of reading game telemetry, JFlightShaker generates **simulated effects**
directly from controller input — making it game-agnostic and lightweight.

(Also… getting ready for Ace Combat 8 😄)

This is vibe coded, as I don't know this stack yet, but of course I'm trying to learn
as I build this.

---

## Download

Grab the initial release here:

- https://github.com/julesian/JFlightShaker/releases

---

## Features

- **Two main rumble effects**
  - Throttle (axis-driven)
  - Gun fire (button-driven)
- **Configurable bindings** (JSON)
- **Configurable effects** (JSON)
- Portable setup

---

## Requirements

- Windows 10 / 11
- .NET (To finalize)
- DirectInput-compatible devices
- Audio output device connected to bass shakers

---

## Devices Tested

- Daiichi Aura AST-1B4 Bass Shaker (50W)
- Nobsound NS-10G Amplifier
- Moza MTP Throttle
- VKB Gladiator

---

## How to use

1. Download and unzip the latest release.
2. Run `JFlightShaker.exe`.
3. Select:
   - **Audio**: the Windows audio output device connected to your bass shaker amp/sound card
   - **Throttle**: the device you want to use for the throttle axis
   - **Stick**: the device you want to use for gun fire / button events
4. Click **Start**.
5. To customize: Edit the json configs in the same folder and in /Config
6. Rerun App

### What you should expect
- Moving the selected throttle axis changes the rumble intensity.
- Pressing the configured gun button triggers the gun effect.
- Clicking **Stop** or closing will stop processing input and audio output.

---

## Configuration

All configuration files are stored **next to the executable**.

```
Config/
├── appsettings.json # App-level settings (devices, paths, version)
├── bindings.json # Input
└── profiles/
├── throttle_effect.json
└── gun_effect.json
```

---

## Todo
- Proper rebinding UI
- UI/UX rework
- Research: Default Controller ForceFeedback to Effects
- Executable


