# Danobat backend and validation

This application remains a .NET 10 Windows WPF application in the existing Visual
Studio solution. The previous implementation generated a generic milling path
inside `MainWindow.xaml.cs`, used G21, treated Z as vertical cutting depth and Y
as cross travel, invented rapid approach/retract positions, used a single feed,
and wrote arbitrary preview text directly to a file. No tests, separate backend,
machine profiles, or documentation directory existed in the supplied folder.
The folder had no `.git` metadata; original edited files are in `.work-backup`.
Existing installer binaries were not rebuilt or released.

## Implemented architecture

`CNCGCodeGenerator.Core` has no WPF or third-party dependency:

- `Domain.cs`: decimal units, axis positions, typed requests/motions, strict input
  parsing and structured validation errors. `ExactDecimal.cs` checks decimal
  arithmetic against scaled integers so small increments cannot silently disappear
  at extreme coordinates.
- `MachineProfile.cs`: schema version 1, per-rule provenance, strict JSON loading,
  profile consistency, limits, work-to-machine offsets and clearance envelopes.
- `Generation.cs`: one semantic planner, Fagor postprocessor, independent text
  interpreter, trajectory/feed/spindle/modal comparison and operator report.
- `Export.cs`: immutable validated snapshot, invalidation, audit result and atomic
  file replacement with recoverable backup.

The WPF window delegates to this pipeline. Settings preserve incomplete drafts,
but incomplete profiles cannot produce an exportable result. Input changes and
opening settings clear the preview and disable export, including cancelled edits.
Export uses the exact validated G-code, never the report textbox. The report and
SHA-256 are shown with the code. Requests, profile JSON, report and code hash are
available on `ValidatedProgram`; `PreviewSession.LastExport` records export outcome
and path. This is in-memory audit data, not a durable production audit log.

## Machine facts and unresolved data

AGENTS.md is the only supplied machine authority. Identity is fixed to Danobat
RTM-2500, serial 30106A, Fagor 8055M. X is table motion, Y vertical downfeed, Z cross
travel, and V the dresser. AGENTS.md reports rightward X as negative; its profile
row carries that observation in notes and requires commissioning evidence before
enablement. No machine numeric values are shipped as approved defaults.

Every required profile rule needs a value, unit, confirmation status, source,
ISO date (`YYYY-MM-DD`), notes, and matching profile version. `operator-entered`
also requires that row's explicit approval. `assumed` and `unknown` always block;
there is no expert override. Null means unknown. Saving/loading a draft does not
certify its values. Missing rules, unknown schema versions, identity changes,
duplicate JSON properties and unsupported properties are rejected; no silent
migration is performed. Update the profile version when its meaning changes.

Before export can be enabled, supply evidence for all applicable limits, signs,
feeds, installed-wheel RPM range, current work-coordinate offsets, setup reference
method, clearance and spindle procedures, decimal precision and file constraints.
For each axis, `ClearanceMinimum/Maximum` describe a reviewed rectangular allowed
machine-coordinate envelope for the actual wheel/fixture/workpiece setup. They
must lie within machine soft limits. All axis-aligned motion segments are checked
against both boxes. This is not a geometric model of guards, wheel contact or
fixtures. Update the envelope and setup review whenever physical setup changes.
Machine coordinate = work coordinate + the explicitly confirmed axis offset.

No G53/G54/reference selection is emitted. The approved setup procedure must
establish the coordinate system and start positions before execution. M03 is a
spindle request only; neither rotation nor physical interlocks are simulated.
The spindle procedure must address verification before contact motion. No delays,
retract/home paths, PLC operations or optional machine M-codes are invented.

Coolant and dressing requests block. V never substitutes for Y or vice versa.
Dressing, chuck/hydraulics, automatic approach/retract, optional M-code entry,
compact loops and direct CNC transfer are intentionally unavailable until their
machine requirements are confirmed and separately implemented/tested.

## Operation semantics

The main screen accepts **planned axis travel**, not workpiece dimensions:

- X: distance between the two table stroke endpoints, greater than zero.
- Y: downfeed before each roughing sweep, nonnegative.
- Z: coverage from the starting Z edge, nonnegative; zero means X-only strokes.

The operator calculates overtravel, entry/exit allowances and wheel contact-width
effects before entering these travels. Settings require current X/Y/Z/V work
positions, feeds for moving axes, positive Z step, spindle RPM, whole sweep counts,
program name and a named setup review. No start coordinate or count is inferred.
An operation must have at least one roughing or finishing sweep. Zero roughing
sweeps permits finishing-only operation, with zero Y downfeed.

The first X stroke follows the selected physical direction using `X.RightSign`.
Choose Z stepping after each X stroke or after a full out-and-back cycle. One row
is ground at every Z level including the final coverage edge. Z steps are full
steps plus one exact remainder; there is no extra Z step at the end. X endpoints
alternate continuously, including between sweeps. Later sweeps reverse Z coverage;
roughing sweeps begin with Y downfeed, finishing sweeps do not. Extra spark-out
round trips remain at the final Z level. These semantics are displayed for review;
they must match the operator's approved grinding procedure before use.

G90 and G91 use the same absolute plan. G91 differences are computed from consecutive
exact targets and independently accumulated after formatting. Every motion carries
its axis-specific F word. V stays at its known start coordinate. The report states
the actual final position of every axis and which X side is reached.

## Numerical/output policy

Inputs use complete ASCII decimal text, `.` separator, optional sign and no
whitespace, exponent, grouping, comma, localized digits or non-finite values.
Persian/Arabic digits are intentionally rejected rather than partially converted.
Decimal parsing also rejects values that .NET would round beyond decimal capacity.
Negative zero prints as `0`. Output is invariant and contains no timestamps.
The parser independently checks numeric-word exactness and accumulation using a
separate scaled representation. Regression testing reproduced and then prevented
silent loss of a 0.001 mm Y increment at an extreme decimal coordinate.
Full-step division uses scaled integer quotient/remainder rather than rounded
decimal division; a second regression prevents an incorrect extra full step when
an extreme ratio rounds up to an integer.

The implementation supports confirmed output precision from 0 through 12 decimal
places. Values not exactly representable at that precision are blocked; the operator
must explicitly adjust them. There is no implicit rounding or microscopic remainder
tolerance. Motion, target and incremental values are checked. Count bounds are
calculated before allocating/planning. Arithmetic overflow produces a blocking error.

Only explicitly confirmed ASCII and LF/CRLF export are supported. The extension and
name length must be supplied. A conservative ASCII filename subset is enforced and
Windows device names are rejected. Profiles requiring numbered blocks are blocked
until controller-specific numbering constraints are implemented. Controller limits
must fit the documented software bounds; bounds are implementation capabilities,
not assumed machine facts. The reviewed filename is fixed during export; rename
the operation and revalidate to change it. Overwriting requires exact-path approval
and keeps a GUID-suffixed backup. The temporary file is flushed before rename/replace.
Cancelling export writes nothing. No program is transmitted or executed.

Machine profiles can be loaded explicitly. Saving for restart uses
`%LOCALAPPDATA%\CNCGCodeGenerator\machine-profile.json`, with explicit overwrite
confirmation and backup. A corrupt startup profile shows a blocking error. Operation
start coordinates and setup approval are not persisted, to require fresh entry.
Main-screen labels support English and Persian; detailed settings/errors/reports
currently use English and all executable program text is ASCII.

## Representative change (offline illustration only)

Previous blocks, with hidden defaults and milling interpretation:

```text
G21
G90
G00 Z5
G00 X-5 Y-5
S12000 M03
G01 Z-10 F500
G01 X105
G01 Y55
G01 X-5
G00 Z5
M05
M30
```

New synthetic regression fixture: known start X10/Y20/Z30/V40, 6 mm
rightward X stroke, 5 mm negative-Z coverage, 2 mm Z step, round-trip strategy.
These values are **not confirmed machine settings or a production program**:

```text
G71
G94
G90
S500 M03
G91
G01 X-6 F100
G01 X6 F100
G01 Z-2 F20
G01 X-6 F100
G01 X6 F100
G01 Z-2 F20
G01 X-6 F100
G01 X6 F100
G01 Z-1 F20
G01 X-6 F100
G01 X6 F100
M05
G90
M30
```

Final work position is X10/Y20/Z25/V40. The real unknown default profile blocks
this output. Operator review and the commissioning procedure in AGENTS.md remain
mandatory before real-machine use; test success is not proof of safe operation.

## Verification

The dependency-free console regression runner is a test project in the Visual
Studio solution. It uses STA for in-process WPF integration tests, returns nonzero
on failure, and requires no test packages or network. Run it with `dotnet run`,
not `dotnet test` (there is no VSTest adapter). It covers unit, boundary, parser,
profile, injection, invariant-culture, deterministic randomized-property, golden,
export, input-adapter and WPF preview/invalidation cases. Golden files are hand
specified, include synthetic spindle requests and no unknown M-codes, and must be
reviewed when changed. Property tests use a fixed seed for reproducibility.

```powershell
dotnet restore CNCGCodeGenerator.Tests/CNCGCodeGenerator.Tests.csproj
dotnet build CNCGCodeGenerator.Tests/CNCGCodeGenerator.Tests.csproj -c Debug --no-restore
dotnet build CNCGCodeGenerator.Tests/CNCGCodeGenerator.Tests.csproj -c Release --no-restore
dotnet run --project CNCGCodeGenerator.Tests/CNCGCodeGenerator.Tests.csproj -c Debug --no-build
dotnet run --project CNCGCodeGenerator.Tests/CNCGCodeGenerator.Tests.csproj -c Release --no-build
dotnet format CNCGCodeGenerator.Tests/CNCGCodeGenerator.Tests.csproj --verify-no-changes --no-restore
dotnet format CNCGCodeGenerator/CNCGCodeGenerator.csproj --verify-no-changes --no-restore
dotnet format CNCGCodeGenerator.Core/CNCGCodeGenerator.Core.csproj --verify-no-changes --no-restore
```

Build project files directly because `setup.vdproj` requires Visual Studio's
installer extension and is not a dotnet CLI project. Tests instantiate WPF windows
without showing them; manual layout, interactive file-dialog/confirmation and
full application restart usability still need operator review. File/profile reload
and cancelled-export semantics are covered at the service level. No installer,
physical CNC, PLC, spindle, transfer or commissioning test is performed by this suite.

### Recorded verification, 2026-09-14

- Dependency restore completed successfully.
- Debug and Release builds of the test project and both referenced application/core
  projects completed with zero warnings and zero errors.
- The full regression runner passed 19/19 groups and 3,514 assertions in each
  configuration, including 300 deterministic randomized operation cases.
- `dotnet format --verify-no-changes --no-restore` completed successfully for all
  three projects after the final changes.
- The two extreme-arithmetic regressions were observed failing before their fixes
  and passing afterward. All five hand-specified golden files remained unchanged
  by the arithmetic fixes.
- Source comparison used `git diff --no-index` against `.work-backup` because the
  supplied folder is not a Git checkout. The existing main layout was retained;
  settings were extended to capture operation setup and machine evidence. App
  startup resources, assembly metadata, publishing profiles and installer sources
  were not edited. Build outputs were regenerated; existing installer binaries
  were not rebuilt.
- No physical-machine commissioning, CNC transfer, interactive file-dialog test,
  manual visual inspection or installer verification was performed. Detailed UI
  reports/settings are currently English even when the main labels are Persian.
