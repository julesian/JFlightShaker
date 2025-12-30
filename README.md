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

## Running

1. Clone the repository
2. Build from source (no prebuilt executable yet)
3. Launch the app
4. Select audio output, throttle, and stick devices
5. Click **Start** to begin monitoring inputs and triggering effects

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


