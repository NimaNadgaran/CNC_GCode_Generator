# CNC G-code Generator

Windows desktop application for planning, validating, previewing, and exporting
Fagor ISO-style CNC programs for one known machine configuration:

- **Machine:** Danobat RTM-2500, serial `30106A`
- **Controller:** Fagor 8055M
- **Application type:** .NET 10 WPF desktop application
- **Output:** deterministic ASCII `.PIM` programs

This software generates and validates text only. It does not connect to,
transfer programs to, or operate the CNC. Passing software validation does not
prove that a program is safe for the machine, workpiece, wheel, fixtures, or
operator.

## Safety notice

This is safety-critical industrial software. Always review the complete
generated program and perform the machine commissioning procedure before real
use. Back up CNC programs and machine parameters, confirm the machine profile
and current coordinates, verify physical directions and clearances, and use
controller simulation, dry run/graphics mode, single-block operation, reduced
feed override, and a reachable feed hold/emergency stop where available.

The application does not simulate physical guards, fixtures, wheel condition,
spindle rotation, coolant, hydraulics, magnetic chuck behavior, PLC logic, or
machine interlocks. `M03` is only a spindle-start request; it is not proof that
the spindle is rotating.

## Current behavior and limitations

The current implementation:

- Uses the Danobat RTM-2500 / `30106A` / Fagor 8055M identity.
- Emits explicit `G71`, `G94`, and `G90` setup commands.
- Uses absolute `G90` motion. Incremental `G91` output is not currently
  exposed by the application.
- Treats the first physical X stroke as RIGHT, currently mapped to numerical
  `X-`. This direction must be reconfirmed during commissioning.
- Lets the operator select the currently unconfirmed Y-down and initial-Z-cross
  signs in Settings. These selections are not proof of physical direction.
- Performs an X out-and-back cycle, then applies Z cross travel according to the
  configured coverage and exact final remainder.
- Applies Y downfeed to roughing sweeps; finishing sweeps do not receive Y
  downfeed.
- Emits independent feed values for X, Y, and Z movement categories.
- Reserves `V` for the servo-controlled diamond dresser. Dressing is disabled.
- Leaves coolant, hydraulic, magnetic-chuck, guard, reference, and other
  unconfirmed machine-specific commands disabled.
- Ends with `M05`, restores `G90`, and emits `M30`.
- Parses the generated text independently and compares the simulated trajectory
  with the planned trajectory before export.
- Exports atomically and requires an explicit operator review confirmation.

Unknown machine values are not replaced with invented limits or M-codes.
Machine-specific assumptions must be confirmed against documentation or the
actual machine before they are used.

## Requirements

- Windows 10/11 with Windows Desktop runtime support required by .NET 10.
- .NET 10 SDK for building and testing.
- Visual Studio 2022 or later with the .NET desktop workload is optional; the
  command-line SDK is sufficient.

Check the installed SDK from a Command Prompt:

```cmd
dotnet --info
```

The project targets `net10.0` for the core library and
`net10.0-windows` for the WPF application and test runner.

## Repository layout

```text
CNCGCodeGenerator.slnx             Solution
CNCGCodeGenerator/                 WPF user interface and settings adapter
CNCGCodeGenerator.Core/            Domain, planning, validation, export, parser
CNCGCodeGenerator.Tests/           Dependency-free console regression tests
CNCGCodeGenerator.Tests/Golden/    Reviewed expected-output fixtures
docs/                               Backend and implementation notes
SETTINGS.md                         Current settings behavior
setup/                              Optional Visual Studio Installer project
```

Safety-critical calculations belong in `CNCGCodeGenerator.Core`; the UI should
only collect inputs, display validation results, and request export of the
validated snapshot.

## Build and test from Command Prompt

Open **Command Prompt** in the repository directory, then run:

```cmd
dotnet restore CNCGCodeGenerator.slnx
dotnet build CNCGCodeGenerator.slnx -c Debug --no-restore
dotnet build CNCGCodeGenerator.slnx -c Release --no-restore
dotnet run --project CNCGCodeGenerator.Tests/CNCGCodeGenerator.Tests.csproj -c Debug --no-build
dotnet run --project CNCGCodeGenerator.Tests/CNCGCodeGenerator.Tests.csproj -c Release --no-build
```

The test project is an executable console runner rather than a VSTest/xUnit
project. Its exit code is non-zero when a regression check fails.

For a clean test-only workflow:

```cmd
dotnet restore CNCGCodeGenerator.Tests/CNCGCodeGenerator.Tests.csproj
dotnet build CNCGCodeGenerator.Tests/CNCGCodeGenerator.Tests.csproj -c Release --no-restore
dotnet run --project CNCGCodeGenerator.Tests/CNCGCodeGenerator.Tests.csproj -c Release --no-build
```

## Run the application

From the repository directory:

```cmd
dotnet run --project CNCGCodeGenerator/CNCGCodeGenerator.csproj -c Debug
```

The application stores versioned user settings under:

```text
%LOCALAPPDATA%\CNCGCodeGenerator\settings.json
```

Start X/Y/Z values are cleared on startup because they represent the current
machine position and must be re-entered and reviewed. Do not copy synthetic
example values into a production setup without verifying them.

## Create an EXE from Command Prompt

### Framework-dependent Windows EXE

This is smaller, but the target Windows machine must have the matching .NET 10
Windows Desktop runtime installed:

```cmd
dotnet publish CNCGCodeGenerator/CNCGCodeGenerator.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts\publish\win-x64-framework-dependent
```

The executable will be:

```text
artifacts\publish\win-x64-framework-dependent\CNCGCodeGenerator.exe
```

### Self-contained single-file Windows EXE

This bundles the .NET runtime and is suitable when the destination computer
does not already have the required runtime. It is larger:

```cmd
dotnet publish CNCGCodeGenerator/CNCGCodeGenerator.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts\publish\win-x64-self-contained
```

Run the published application with:

```cmd
artifacts\publish\win-x64-self-contained\CNCGCodeGenerator.exe
```

Use `win-arm64` instead of `win-x64` only when the deployment computer and
dependencies support ARM64. The publish directory is intentionally ignored by
Git. Copy or package the complete publish directory when using the
framework-dependent build; do not copy only the EXE.

### Optional installer project

`setup\setup.vdproj` is a Visual Studio Installer Project. Building it requires
the Visual Studio Installer Projects extension and a compatible Visual Studio
installation. The `dotnet publish` commands above are the reproducible command
line method for creating the application EXE and do not build an MSI installer.

## Generated program and export rules

The normal output contains a program identifier, generator version, machine
identity, setup section, grinding section, explicit motion feeds, and shutdown
section. Program numbers must be six ASCII digits and comments are restricted
to safe printable ASCII text. Generated files use the six-digit program number
as the filename stem and the current `.PIM` extension.

Before exporting, confirm that the displayed start position, physical travel
directions, clearances, machine profile, and every generated block are correct.
Changing inputs or settings invalidates the prior preview/export approval.
Existing destination files require explicit overwrite confirmation and receive
a recoverable backup when replacement is possible.

## Development guidance

Read [AGENTS.md](AGENTS.md) before changing generation, validation, axis signs,
units, machine profiles, parser behavior, or export. It defines the source of
truth hierarchy, required safety behavior, testing expectations, and machine
commissioning rules.

Additional implementation details are in [docs/backend.md](docs/backend.md)
and [SETTINGS.md](SETTINGS.md). Any change to safety-critical generation logic
must include boundary/regression tests and representative output review.

Do not commit, publish, transmit, or execute CNC programs as part of a normal
software build. A successful build or test run is not approval for real-machine
use.

## Troubleshooting

- **`dotnet` is not recognized:** install the .NET 10 SDK and reopen Command
  Prompt.
- **WPF framework/runtime errors:** build and run on Windows with the Windows
  Desktop runtime/SDK installed.
- **Export is blocked:** correct the reported input/profile/round-trip error and
  generate a fresh preview; do not bypass the validation pipeline.
- **Settings fail to load:** the settings file may be corrupt or from an
  unsupported schema. Preserve a copy for diagnosis, then remove or repair it
  only after reviewing the impact; the application must fail closed.
- **A spindle does not start:** `S... M03` only requests the PLC/CNC spindle
  start. Investigate the machine using approved electrical and PLC diagnostics;
  do not add bypass commands to the generated program.

## License and machine documentation

No license file or machine-specific manufacturer documentation is included in
this repository. Before production use, establish the applicable project
license and retain the Danobat 30106A documentation, Fagor 8055M manual,
machine parameters, electrical drawings, and known-good machine programs under
the organization's controlled document process.


