# Kill Confirm Overlay Widget

UWP/Xbox Game Bar widget for showing kill confirmation sprite animations over a game.

The widget connects to `ws://127.0.0.1:3000/events`, plays sprite sheets from `Assets\KillConfirm`, and relies on the outer Packaging Project to declare and package the full-trust `KillConfirmService\cskillconfirm.exe` companion.

Build this project through the root `..\Build-IntegratedPackage.ps1` script so the Rust service executable and sound packs are copied into the widget content before the Packaging Project builds the final MSIX.
