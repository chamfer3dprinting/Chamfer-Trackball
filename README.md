# Chamfer Trackball

Windows trackball navigation, with editable application source and reproducible builds.
The current source rebuild is **v1.5.6**. Previous release history is retained.

## Download

Download **ChamferTrackball.exe** from [Releases](https://github.com/chamfer3dprinting/Chamfer-Trackball/releases).
The single executable includes the application, .NET runtime, interface and integration
payloads. Microsoft Edge WebView2 Runtime is also required (normally already installed
on Windows). Quit the previous Trackball app before opening the rebuilt version.

## Controls

Ordinary mouse clicks, pointing and scrolling remain normal outside a held navigation
mode. Defaults in the 3D modelling program:

| Hold | Ball | Wheel |
| --- | --- | --- |
| Shift | Orbit | Camera roll with a native integration |
| Ctrl+Shift | Pan | Zoom simultaneously |
| Alt | Snap to one of 26 aligned views | — |
| Backtick | Move through the scene | Turn |
| Backtick+Alt | Move sideways / change height | Turn |

All held-key bindings can be changed and saved. Ctrl alone is never a navigation
trigger. The Normal mouse program has all navigation bindings off.

The Nulea M512's ordinary mouse input reports X/Y. An independent third control uses
a wheel; this software does not add a physical twist sensor.

## Application compatibility

- **Blender 4.2+ and Autodesk Fusion:** native adapters are included inside the executable.
  Under CAD apps, install the adapter, then enable it in the host application's add-on
  settings. They use real camera and cursor/selection pivot data and change the view only.
- **Chamfer3D Studio:** retains the existing local WebSocket motion protocol for orbit,
  pan, roll and zoom. Its older v1 link does not support the rebuilt Alt snap / fly modes.
- **Other 3D applications:** configurable orbit, pan and wheel-zoom mouse emulation.
  Existing profiles cover common CAD, modelling and slicer applications. Add a custom
  executable and edit its controls in CAD apps. Native snapping, pivot and fly navigation
  require a host adapter; mouse emulation cannot query arbitrary scene geometry.

Support is declared by capability; do not describe every application as having native
integration. The included integrations still require physical testing in the owner's
installed CAD applications.

## Source and validation

- `src/Chamfer.Trackball.Core`: settings persistence/migration, chord precedence,
  combinations and pivot-based camera mathematics.
- `src/Chamfer.Trackball.App`: Windows Raw Input, input routing, WebView2 interface,
  loopback native adapter service, tray, update validation and integration installation.
- `integrations/`: included Blender and Fusion adapters, with host APIs used only on
  their main threads.
- `tests/`: regression checks and interface script checks.
- `.github/workflows/build-source.yml`: Windows build, actual embedded-interface
  smoke test and SHA-256-verified executable prerelease.
- `release-notes/`: version history.

Build with .NET 10 SDK:
```
dotnet run --project tests/Chamfer.Trackball.Tests.csproj -c Release
dotnet publish src/Chamfer.Trackball.App/Chamfer.Trackball.App.csproj -c Release -r win-x64 --self-contained true
```

Source is now kept here. No Claude session or private source export is required.
