# JFlightShaker

**JFlightShaker** is a Windows application that maps throttle and stick inputs to
arcade-style flight-sim rumble effects using audio bass shakers.

I wanted something similar to the effects I get in **Microsoft Flight Simulator 2024**
with SimHub, but since SimHub can’t run custom bass-shaker effects while
**Project Wingman**, **Ace Combat**, **Star Citizen** is running, I built my own solution.

Instead of reading game telemetry, JFlightShaker generates **simulated effects**
directly from controller input — making it game-agnostic and lightweight.
Makes a great massage chair too 😆.

(Also… getting ready for Ace Combat 8 😄)

---

<img width="1038" height="590" alt="JFlightShaker_v0 1 0" src="https://github.com/user-attachments/assets/39ffe2ea-336a-4ff8-8aa3-efdf276f4030" />

## Download

Grab the initial release here:

- https://github.com/julesian/JFlightShaker/releases

---

## Features

- **Two main rumble effects**
  - Throttle (axis-driven)
  - Gun fire (button-driven)
- **Configurable bindings**
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
3. Select **Audio**: the Windows audio output device connected to your bass shaker amp / sound card.
4. Click **Start**.
5. Bind controls:
	- Double-click an effect row (or select it and click Edit)
	- Choose the Device
	- In Edit Binding window you can set effect intensity and select control type (Axis | Button)
	- Save
6. If you want to clear a binding, select a row and click Unbind.
7. Click Stop (or close the app) to stop input processing and audio output.

### What you should expect
- **Throttle** effect:
	- Moving the bound axis changes the rumble intensity.
- **Gun Fire** effect:
	- Pressing the bound button triggers the gun effect.
- **Mute Effects**:
  	 - All effects will be muted
	- If set to Button (Hold): as long as the button is held *(Good for switch type control)*
	- If set to Button (Trigger): acts as a toggle
- **Start/Stop** controls whether input + audio output are running.
- Clicking **Stop** or closing will stop processing input and audio output.
- Device + audio selections are restored on next launch.

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
- Better UI/UX
- Research: Default Controller ForceFeedback to Effects
- Editable effects thorugh UI (You can customize through JSON)
- Turning on / off effects via controller buttons / switch
	- *Can't have the idle vibration while I'm walking on Star Citizen*
- Customizable gun interval
- Adding of custom effects

  
*This is vibe coded, as I don't know this stack yet, but of course I'm trying to learn
as I build this. (Exploring Codex)* 

