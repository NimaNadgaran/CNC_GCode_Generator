# Settings guide

This document describes the settings implemented by the current application.
The supported machine identity is Danobat RTM-2500, serial 30106A, with a
Fagor 8055M controller. The application generates and validates G-code only;
it does not verify physical clearances, guards, fixtures, wheel condition,
spindle rotation, coolant, hydraulics, or other machine interlocks.

All example values in this document are synthetic offline test values. They
are not approved production settings and must not be run on the machine without
the commissioning and operator-review procedure in `AGENTS.md`.

## Main-window travel inputs

These fields describe planned axis travel. They are not workpiece dimensions
and are not calculated automatically from the workpiece.

### X table stroke travel (mm)

The distance between the two X stroke endpoints. The first X stroke is always
physical RIGHT, which the current configuration maps to negative X (`X-`). A
value of `600` therefore commands the first X endpoint at `StartX - 600`.

The value must be positive and exactly representable at the configured output
precision of three decimal places.

### Y downfeed per roughing sweep (mm)

The vertical Y movement applied once at the beginning of each roughing sweep.
Finishing sweeps never receive Y downfeed. The configured Y-down sign is used
to determine whether the target is numerically positive or negative.

When roughing sweeps are zero, this field is set to `0` by the UI and disabled.
A blank value is also normalized to zero by the settings adapter. A positive
value with zero roughing sweeps is rejected; a zero or negative value is not a
valid roughing downfeed when roughing is enabled.

Example: `0.020` with two roughing sweeps commands a total Y movement of
`0.040 mm`.

### Z cross-travel coverage (mm)

The total Z coverage from the starting Z coordinate. Coverage may be zero. For
nonzero coverage, the planner emits complete configured steps and one exact
final remainder when necessary; it never overshoots the requested coverage.

Example: coverage `210` with a `20` mm step ends with a `10` mm final step.

## Operation Setup

Open **Settings** to edit the operation values below. The application currently
uses these fixed operation rules:

- Coordinates are absolute (`G90`); incremental output is not available.
- The first X direction is physical RIGHT, commanded as `X-`.
- Z stepping occurs after one complete X out-and-back cycle.
- The planner alternates the Z coverage direction on successive sweeps.
- Coolant, dressing, magnet, hydraulic, guard, and other unconfirmed
  machine-specific commands are disabled.

### Required operation values

| Setting | Meaning and validation |
|---|---|
| Start X coordinate (mm) | Known current/work-coordinate X position before starting. |
| Start Y coordinate (mm) | Known current/work-coordinate Y position before starting. |
| Start Z coordinate (mm) | Known current/work-coordinate Z position before starting. |
| X grinding feed (mm/min) | Positive feed explicitly emitted on every X move. |
| Y downfeed feed (mm/min) | Positive feed for Y moves. The text is still required by the current settings adapter even when no Y move is generated. |
| Z cross-feed (mm/min) | Positive feed for Z moves. The text is still required by the current settings adapter even when coverage is zero. |
| Z cross-step (mm) | Positive step when Z coverage is nonzero. The text is still required by the current settings adapter when coverage is zero. |
| Spindle speed (rpm) | Positive programmed spindle request, emitted as `S... M03`; this does not prove rotation. |
| Roughing sweeps (count) | Whole number from 0 through 100000. Each roughing sweep may receive one Y downfeed. |
| Finishing sweeps (count) | Whole number from 0 through 100000. At least one roughing or finishing sweep is required. |
| Extra final-Z X round trips (count) | Whole number from 0 through 100000. Adds X out-and-back cycles at the final Z position without Y or Z movement. |
| Program number (six digits) | Exactly six ASCII digits; it becomes the filename stem. |
| Program comment (ASCII) | Optional printable ASCII text, maximum 200 characters. Newlines, control characters, `;`, `%`, `(`, and `)` are rejected. |

The input adapter rejects incomplete numeric text, commas, exponent notation,
localized digits, non-finite values, and values that cannot be represented
exactly at three decimal places. It does not silently replace invalid input
with zero or truncate fractional values.

## Machine configuration

The current implementation stores the supported-machine constants and signs in
`CNCGCodeGenerator.Core/Danobat30106AConfig.cs`:

| Configuration | Current value | Status |
|---|---|---|
| Manufacturer | Danobat | Known machine identity |
| Model | RTM-2500 | Known machine identity |
| Serial number | 30106A | Known machine identity |
| Controller | Fagor 8055M | Known machine identity |
| Rightward table motion | `X-` (`XRightSign = -1`) | Operator information; reconfirm during commissioning |
| Y down direction | `Y-` or `Y+` | Unconfirmed; selectable in Settings |
| Initial Z cross direction | `Z-` or `Z+` | Unconfirmed; selectable in Settings |

The Y and Z sign selections are saved in the per-user settings file, but they
are not proof of physical direction. They are not a complete persisted machine
profile. Software limits, hard limits, clearances,
wheel data, offsets, reference positions, feed/RPM ranges, controller file
rules, and machine-specific M-codes remain unknown and are not invented by the
generator.

The V axis is reserved for the servo-controlled diamond dresser, but dressing
is not implemented. Y must never be substituted for V.

Reusable settings are stored at `%LOCALAPPDATA%\CNCGCodeGenerator\settings.json`.
The file is versioned and written atomically. Start X/Y/Z coordinates are
intentionally cleared when the application starts because they represent the
current machine position and must be entered and reviewed again. The export
review checkbox is never persisted.

## Generated output

The output is deterministic ASCII text using invariant decimal formatting and
three-decimal-place validation. It contains the following main sections:

```text
; PROGRAM 000101
; GENERATOR 3.0.0
; MACHINE DANOBAT RTM-2500 SERIAL 30106A FAGOR 8055M
; COMMENT offline test
; SETUP
G71
G94
G90
S500 M03
; GRINDING
...
; SHUTDOWN
M05
G90
M30
```

The filename is the six-digit program number with the fixed `.PIM` extension.
The generated program explicitly establishes metric, feed, and absolute modes;
stops the spindle before restoring `G90` and ending with `M30`; and writes an
explicit feed on every motion block.

Before export, the generated text is independently parsed and compared with
the planned trajectory. Export uses the exact validated snapshot, requires the
operator acknowledgement that the start position, travel direction, and
clearances were verified, and writes atomically. Existing files require an
overwrite confirmation and receive a recoverable backup when replacement is
possible.

## Offline regression values

These values exercise the current finishing-only regression and are not machine
defaults:

```text
Start X = 1200       Start Y = 250       Start Z = 500
X travel = 400       Y downfeed = 0     Z coverage = 81
X feed = 1000        Y feed = 50        Z feed = 100
Z step = 40           RPM = 500          Roughing = 0
Finishing = 2         Extra round trips = 1
Program number = 000110
Y-down direction = Y-  Initial Z direction = Z-
```

Expected checks: no Y blocks; Z positions are `460`, `420`, `419`, then
`459`, `499`, `500`; nine X round trips are generated; and the program ends
with `M05`, `G90`, and `M30`.

## Operator review notice

Validation passing means only that the configured inputs and implemented
formatting/trajectory checks passed. It is not a claim that the program is safe
to run. Before real-machine use, back up CNC programs and parameters, confirm
the machine profile and current coordinates, verify directions and clearances,
simulate on the controller if available, and use dry-run/single-block/reduced
feed procedures with the emergency stop available.
