# KillConfirmGameBar

夸克网盘：
链接：https://pan.quark.cn/s/0f1f493e93f9?pwd=DFY8
提取码：DFY8

[English](README.md) | [简体中文](README.zh-CN.md)

KillConfirmGameBar is a Counter-Strike 2 kill-confirm overlay for Xbox Game Bar.

It plays voice lines and shows animated effects when CS2 reports kills through Game State Integration. The overlay runs inside Xbox Game Bar, so it can stay on top while you play.

## Features

- CS2 kill-confirm sound effects.
- Xbox Game Bar overlay widget.
- Animated effects for normal kills, headshots, knife kills, first kills, and last kills.
- Voice-pack switching from the control panel.
- Small-group Windows installer support.

Current voice packs:

- `cf`
- `cffhd`
- `cfsex`

## Requirements

- Windows 10/11
- Xbox Game Bar enabled
- Counter-Strike 2

If you want to build from source, you also need:

- Rust toolchain
- Visual Studio or Visual Studio Build Tools with Windows/UWP/MSIX tooling
- Inno Setup 6, only for building the optional `.exe` installer

## Install

For normal use, download a release package and run the installer or install script included with that release.

After installing:

1. Open Xbox Game Bar with `Win + G`.
2. Open the Kill Confirm Overlay widget.
3. Start CS2.
4. The installer will try to configure CS2 Game State Integration automatically.

The overlay uses a small local companion service. The installer sets up the package and the local connection needed by the Xbox Game Bar widget.

## CS2 Game State Integration

CS2 needs a GSI config that points to:

```text
http://127.0.0.1:3000/
```

The installer tries to create this config automatically. If kill events do not trigger, place `KillConfirmService/gsi/gamestate_integration_killconfirm.cfg` in the CS2 cfg folder manually:

```text
C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\cfg\
```

The upstream GSI reference config is available here:

```text
https://github.com/st0nie/gsi-cs2-rs/blob/main/gsi_cfg/gamestate_integration_fast.cfg
```

## Build From Source

From the repository root:

```powershell
.\Build-IntegratedPackage.ps1
```

To create a transferable install package:

```powershell
.\Build-TransferPackage.ps1
```

To create the optional installer:

```powershell
.\Build-Installer.ps1
```

## Project Layout

- `KillConfirmService`: Rust local service for CS2 Game State Integration and audio playback.
- `Widget`: Xbox Game Bar widget.
- `Package`: Windows packaging project.
- `Installer`: installer wrapper files.
- `SourceAssets`: source animations, audio, icons, and voice packs.

Generated package-ready files are refreshed from `SourceAssets` during the build.

## Notes

- The app communicates only with a local service on `127.0.0.1`.
- Voice-pack behavior is controlled by `sound.lua` files inside each sound pack.
- Only install sound packs from sources you trust.
- Test signing files are for development builds only. Public releases should be signed with a proper release certificate or trusted signing service.

## Credits

The Rust service is based on the open-source `cskillconfirm` project by st0nie:

```text
https://github.com/st0nie/cskillconfirm
```

This project also uses `gsi-cs2-rs`:

```text
https://github.com/st0nie/gsi-cs2-rs
```

## License

This project is licensed under the GNU Affero General Public License v3.0. See `LICENSE`.

This project is not affiliated with Valve, Microsoft, Xbox, CrossFire, Valorant, Battlefield, or any other game publisher.
