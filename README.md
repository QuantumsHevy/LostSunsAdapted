# Lost Suns Adapted

A Kerbal Space Program parts mod that attempts to add a working crunch drive and related parts from Staladus' *Lost Suns*.

## Dependencies

LSA requires these mods in order to run as intended.

- [ModuleManager](https://github.com/sarbian/ModuleManager)
- [B9PartSwitch](https://github.com/blowfishpro/B9PartSwitch)

## Compatibilities

Patches that add compatibility with these mods are included with installation.

- [Community Tech Tree](https://github.com/post-kerbin-mining-corporation/CommunityTechTree)
- [VABOrganizer](https://github.com/KSPModStewards/VABOrganizer)

## Installation

Download the latest ZIP and extract the folder directly into your installation's `GameData` folder using File Explorer's "Extract all" option.

You may also extract the folder manually and place ONLY the parent `LostSunsAdapted` folder inside `GameData`.

This mod is not yet available to install via CKAN.

## Settings

Settings that apply to every craft with a crunch drive are stored in `LostSunsAdapted/CrunchDrives/Settings.cfg`.

| Key | Default | Effect |
| --- | --- | --- |
| `allowTimeWarp` | `true` | Time warp may be activated by the player during all spools and during crunches that are longer than WarpMinLegSeconds. |
| `allowNearbyCraft` | `true` | A crunch may start (and carry on) with other craft loaded nearby. |
| `useTurnController` | `true` | Turn between the two legs (and onto retrograde at arrival) with the MechJeb2-derived attitude controller. Setting this to false should switch to SAS. |
| `dropoutSphereMultiplier` | `1.0` | Scales every dropout sphere altitude above its body. Hard-limited to 1km above the surface or atmosphere. |
| `looseStartConditions` | `true` | A spool may start inside a dropout sphere, inside an atmosphere, or when the craft is not pointing at its target. A crunch may start inside a dropout sphere or an atmosphere if it heads out of the sphere (the craft must still be aligned to start the crunch). |
| `ignoreDropoutSpheres` | `false` | Manual mode only: the crunch ignores every dropout sphere, so only the player (or set duration) ends it. High risk of collisions and physics instability. |

## Known Issues

- No known issues, yet. If you find a bug in the mod or otherwise wish to contribute your expertise, feel free to start a GitHub issue or fork the repository to work your magic!

## Credits

- **[Quantums Hevy](https://github.com/QuantumsHevy)**, original mod author
- **[Staladus](https://www.youtube.com/@Staladus)**, author of the *Lost Suns* IP and original creator of this Crunch Drive concept
- **[Danyal Adam](https://www.youtube.com/@DanyalAdam-n3n)**, part modeler
- **[Wild Blue Industries](https://github.com/Angel-125)**, author of [Blueshift](https://github.com/Angel-125/Blueshift) from which FTL methodology for KSP was heavily referenced
- **[Umbra Space Industries](https://github.com/UmbraSpaceIndustries)**, author of [Warp Drive](https://github.com/UmbraSpaceIndustries/WarpDrive) from which FTL methodology for KSP was heavily referenced

## License

![Not By AI badge](Media/Developed-By-a-Human-Not-By-AI-Badge-white.png)

Copyright © 2026 Quantums Hevy

This work is licensed under the [GNU General Public License v3.0.](./COPYING) (GPL-3.0).
