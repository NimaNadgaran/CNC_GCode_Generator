# Danobat RTM-2500 backend

This application supports one machine: Danobat RTM-2500, serial 30106A, with a
Fagor 8055M controller. The safety-critical planner is in
`CNCGCodeGenerator.Core`; WPF code only adapts inputs and presents the report.

`Danobat30106AConfig` contains the known X convention (`RIGHT = X-`) and the
single locations for the still-unconfirmed Y-down and initial-Z-cross signs.
The normal operation model has start X/Y/Z, main-screen X/Y/Z travel, feeds,
Z step, spindle RPM, pass counts, six-digit program number, and an ASCII
comment. V dressing, coolant, magnet, hydraulic, guard, and other
machine-specific commands remain disabled.

The planner always creates absolute G90 motion. It performs one complete X
out-and-back cycle at each Z row, uses exact final partial Z steps, applies Y
downfeed once per roughing sweep, and applies no Y downfeed to finishing
sweeps. Output is deterministic ASCII with `.PIM` extension and ends `M05`,
`G90`, `M30`.

Generated text is parsed and compared with the semantic motion plan before it
can be exported. Export is atomic and requires the operator confirmation:
“I verified the start position, travel direction and clearances on the
machine.” The confirmation is not persisted and is cleared when inputs or
settings change. Automated checks do not prove machine safety; commissioning,
controller simulation, and operator review remain required.

Verification:

```powershell
dotnet restore CNCGCodeGenerator.Tests/CNCGCodeGenerator.Tests.csproj
dotnet build CNCGCodeGenerator.Tests/CNCGCodeGenerator.Tests.csproj -c Debug --no-restore
dotnet build CNCGCodeGenerator.Tests/CNCGCodeGenerator.Tests.csproj -c Release --no-restore
dotnet run --project CNCGCodeGenerator.Tests/CNCGCodeGenerator.Tests.csproj -c Debug --no-build
dotnet run --project CNCGCodeGenerator.Tests/CNCGCodeGenerator.Tests.csproj -c Release --no-build
```

The test project is a dependency-free console runner rather than a VSTest
project.
