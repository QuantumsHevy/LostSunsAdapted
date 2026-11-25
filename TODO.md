# To Do

## Parts

- Redo all textures (re-seam, cull invisible faces, fix AO maps, increase resolution where allowable)
- Improve all colliders (direct edit of the Blender tool's generated meshes)
- Model and add UGDF-CD "Deva" Crunch Drive with appropriate stats and descriptions

## Compatibility

- Determine if reference shield sizes pertain to radius or diameter (and adjust parts accordingly)
  - 10, 15, 25, and 30m
  - 45, 50, and 60m for good measure
  - All current 2.5m cores appear to be near 30m in size (1.25m core is around 5m in size)
- Figure out why SystemHeat stats don't show until after part placement in VAB (FFT's reactors' stats do show up)

## Done

### Reorganize All Options

#### Intended Flight PAW

- **Crunch Drive**
  - Status [readout] [all readouts should stay as they are unless otherwise specified]
  - Crunch Coefficient [only appears during spool]
  - Charge Time [only appears during spool]
  - ENGAGE CRUNCH DRIVE (FORE/AFT) [only appears during spool]
  - Direction: FORE/AFT [should disappear during spool]
  - Power Draw Rate [slider] [should disappear during crunch]
  - START/STOP CASIMIR GENERATION [toggle] [should disappear during crunch]
  - EXIT CRUNCH FIELD [toggle] [only appears during crunch]
- **Drive Targeting**
  - Target [readout]
  - Distance [readout] [only appears when target selected]
  - Gravity Well (Planet) [readout] [remove nearest dropout zone readout]
  - Mode Two-Leg/One-Leg/Manual [slider] [should disappear during spool and crunch]
  - Capture Assistance [toggle]
  - Retrograde on Arrival [toggle]
  - Dropout Alt. [slider] [should disappear during crunch]
  - Set Speed/Time/Both/Neither [slider] [only appears when mode = manual]
  - Coefficient [slider] [only appears when mode = manual]
  - Duration [slider] [only appears when mode = manual]

#### Intended VAB PAW

- **Crunch Drive**
  - Power Draw Rate [slider]
- **Drive Targeting**
  - Mode Two-Leg/One-Leg/Manual [slider]
  - Capture Assistance [toggle]
  - Retrograde on Arrival [toggle]
  - Dropout Alt. [slider]
  - Set Speed/Time/Both/Neither [slider] [only appears when mode = manual]
  - Coefficient [slider] [only appears when mode = manual]
  - Duration [slider] [only appears when mode = manual]  

#### Global settings, only in Settings.cfg

- Allow Time Warp [true/false] [default: true]
- Allow Nearby Craft [true/false] [default: true] [change to false later]
- Use MechJeb2-Derived Turn Controller [true/false] [default: true]
- Dropout Sphere Multiplier [decimal] [default: 1.0]
- Loose Start Conditions [true/false] [default: true]
- Ignore Dropout Spheres [true/false] [default: false]

All other values/defaults should be set by parts' configs, then by CrunchTuning.cs if not defined.
