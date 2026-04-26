# CS Kill Confirm

This tool helps you confirm your Counter-Strike 2 kills.

## Installation

1. Download the latest binary release from the [Releases](https://github.com/st0nie/cskillconfirm/releases) page.

2. Extract the downloaded file.

3. Make sure to place the `sounds` directory in the same folder as the executable.

## Game State Integration Setup

1. Download the GSI configuration file from [here](https://github.com/st0nie/gsi-cs2-rs/blob/main/gsi_cfg/gamestate_integration_fast.cfg).

2. Place this file in your CS2 configuration directory:
    - Windows: `C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\cfg\`
    - Linux: `~/.steam/steam/steamapps/common/Counter-Strike Global Offensive/game/csgo/cfg/`
    - Mac: `~/Library/Application Support/Steam/steamapps/common/Counter-Strike Global Offensive/game/csgo/cfg/`

## Usage

1. Start the application by running the executable.

2. Launch CS2 with the GSI configuration in place.

3. The tool will now provide crossfire-like kill confirmations while you play.

## Troubleshooting

If you're experiencing issues:
- Verify that the `sounds` directory is in the same folder as the executable
- Check that the GSI configuration file is in the correct CS2 cfg directory
