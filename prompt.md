Fix the finishing-only validation bug in the CNC G-Code Generator.

Current reproduction:

- Roughing sweeps: `0`
- Finishing sweeps: `2`
- Extra final-Z X round trips: `1`
- Y downfeed per roughing sweep: `0`

The application incorrectly blocks export with:

```text
EXPORT BLOCKED
Input: Y downfeed: Y downfeed requires roughing sweeps.
```

Required behavior:

1. When `Roughing sweeps > 0`:
   - `Y downfeed per roughing sweep` must be a finite number greater than zero.
   - Preserve the existing Y-downfeed generation behavior.
2. When `Roughing sweeps == 0`:
   - Accept Y downfeed as blank or `0`.
   - Normalize blank to numeric zero.
   - Generate no Y-axis movement.
   - Do not block finishing-only programs.
   - If a positive Y downfeed somehow remains while roughing is zero, block it with the clearer message:

```text
Y downfeed must be zero when roughing sweeps are zero.
```

1. In the UI, when roughing sweeps becomes zero:
   - Set the Y-downfeed field to `0`.
   - Disable or visually mark the field as unused.
   - Re-enable it when roughing sweeps becomes greater than zero.
   - Do not modify the separate `Y downfeed feed` machine setting; it can remain configured but must not be emitted when no Y move exists.

The likely current bug is that validation checks whether the Y-downfeed field was provided, instead of checking its parsed numeric value.

Use logic equivalent to:

```csharp
if (roughingSweeps > 0)
{
    if (!yDownfeed.HasValue ||
        !double.IsFinite(yDownfeed.Value) ||
        yDownfeed.Value <= 0)
    {
        AddError("Y downfeed must be greater than zero when roughing is enabled.");
    }
}
else
{
    if (yDownfeed.HasValue && yDownfeed.Value > 0)
    {
        AddError("Y downfeed must be zero when roughing sweeps are zero.");
    }

    yDownfeed = 0;
}
```

Adapt this to the project’s existing types and validation architecture. Do not blindly paste the example if the project uses `decimal`, result objects, nullable models, or centralized validation.

Add regression tests for all of these cases:

- Roughing `0`, Y `0`, finishing greater than zero → valid export and no Y commands.
- Roughing `0`, Y blank, finishing greater than zero → valid export, normalized to zero, and no Y commands.
- Roughing `0`, positive Y → validation error with the new message.
- Roughing greater than zero, Y `0` → validation error.
- Roughing greater than zero, Y blank → validation error.
- Roughing greater than zero, positive Y → existing roughing behavior remains unchanged.
- Negative, NaN, and infinite Y values remain rejected.

Regression input for program `000110`:

```text
Start X = 1200
Start Y = 250
Start Z = 500
X grinding feed = 1000
Y downfeed feed = 50
Z cross-feed = 100
Z cross-step = 40
Spindle speed = 500
Roughing sweeps = 0
Finishing sweeps = 2
Extra final-Z X round trips = 1
X travel = 400
Y downfeed per roughing sweep = 0
Z coverage = 81
Y-down direction = Y-
Initial Z direction = Z-
```

Expected generated motion:

- No `Y` command anywhere.
- First finishing sweep: `Z500 → Z460 → Z420 → Z419`.
- Second finishing sweep: `Z419 → Z459 → Z499 → Z500`.
- Each position gets one complete `X800 → X1200` round trip.
- One additional `X800 → X1200` round trip at final `Z500`.
- Nine X round trips total.
- End with `M05`, `G90`, and `M30`.

Do not modify the established G90 path calculations, X direction, Z remainder handling, alternating Z-sweep behavior, spindle commands, feeds, shutdown sequence, or other safety validation.

Inspect the existing code first, implement the smallest maintainable fix, run all existing tests plus the new regression tests, and report:

- Root cause
- Files changed
- Exact validation change
- Tests executed and results
- Generated Test 110 G-code