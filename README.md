# WorldBuilderCoop

Cooperative multiplayer mod for **Broke Protocol's World Builder**. Build
together in real-time over Steam or LAN.

> Mod for the [BrokeProtocol ModLoader](https://github.com/Expected333/BrokeProtocol-ModLoader).

## Features

- **Steam mode** — host a lobby or join a friend via Steam Friends
- **Local mode** — instant testing in LAN or solo across two instances
- **Real-time synchronization**:
  - Object placement, transform and deletion
  - Shared selection across all players
  - Synchronized Undo/Redo history (ordered snapshots)
  - Current map propagated to all clients
- **Non-destructive patches** — no game files modified, everything runs
  through Harmony
- **Dedicated UI** — connection panel integrated into the editor, live
  status indicators

## Installation

The recommended way is via the ModLoader:

1. Install [BrokeProtocol-ModLoader](https://github.com/Expected333/BrokeProtocol-ModLoader-Installer/releases)
2. Launch the game and open the **MODS** menu from the main menu
3. Go to the **Available** tab → install **WorldBuilderCoop**
4. Restart the game

**Manual install:** drop `WorldBuilderCoop.dll` into `BrokeProtocol/Mods/`.

## Requirements

- Broke Protocol (recent version)
- BrokeProtocol-ModLoader **≥ 1.0.0**
- Steam running (for online mode)

## Usage

1. Start the game and enter the **World Builder** (from the main menu)
2. The mod adds a connection panel to the editor UI:
   - **Host** — opens a Steam lobby, accept friends via the Steam overlay
   - **Join** — connects to a friend currently hosting
   - **Local** — runs as a LAN host on a fixed port for local testing
3. Once connected, every action (place / move / delete / undo / map switch)
   syncs across all clients automatically.

## Project structure

```
WorldBuilderCoop/
├── mod.json                         ← Registry manifest
├── WorldBuilderCoop.slnx            ← Visual Studio solution
└── WorldBuilderCoop/                ← Source code
    ├── Core.cs                      ← ModBase entry point
    ├── Behavior/                    ← Player & history tracking
    ├── Events/                      ← Event bus
    ├── Managers/                    ← Selection batching, map loading
    ├── Network/                     ← Steam & local transports
    ├── Patches/                     ← Harmony patches
    ├── UI/                          ← Editor UI extensions
    └── Utility/                     ← Logging, helpers
```

## Building

1. Open `WorldBuilderCoop.slnx` in Visual Studio (or your IDE of choice)
2. Make sure the DLL references point to your local Broke Protocol install
3. Build in `Release` mode
4. Output: `WorldBuilderCoop/bin/Release/WorldBuilderCoop.dll`

## Bugs & feedback

Open an [issue](https://github.com/Expected333/WorldBuilderCoop/issues) with:
- Your game version
- `ClientLog.txt` / `HostLog.txt` from the game folder
- Steps to reproduce

## Credits

- **Expected333** — code & design
- The Broke Protocol community for testing

## License

See [LICENSE](LICENSE) if present, otherwise all rights reserved.
