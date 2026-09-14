# AGENTS.md

## 1. Purpose and scope

This repository contains a Windows application that generates CNC programs for
a Danobat RTM-2500 surface-grinding machine equipped with a Fagor 8055M CNC.

Treat this as safety-critical industrial software.

A generated program can move heavy machine components, start a grinding wheel,
and potentially damage the machine, grinding wheel, workpiece, fixtures, or
injure an operator. Correct-looking G-code is not automatically safe G-code.

These instructions apply to every file in this repository unless a more
specific AGENTS.md exists in a subdirectory.

The application is initially a G-code generator and validator. It must not
automatically operate the machine, bypass interlocks, modify PLC logic, or
transmit and execute programs without a separately designed and reviewed
transfer system.

---

## 2. Mandatory behavior for every Codex session

Before editing code:

1. Read this complete file.
2. Inspect the repository structure.
3. Read all relevant files under `docs/`.
4. Detect the existing language, framework, architecture, and test system.
5. Inspect current uncommitted changes and preserve unrelated user work.
6. Identify which machine assumptions are confirmed and which are unknown.
7. Explain the intended change and its safety impact.
8. Add or update tests before considering the change complete.

Do not rewrite the project or replace its framework merely because another
technology would be easier.

Do not commit, push, publish, release, transmit G-code, or connect to the CNC
unless the user explicitly requests that exact action.

Do not claim that generated G-code is safe to run solely because automated tests
pass.

---

## 3. Source-of-truth hierarchy

When information conflicts, use this order of authority:

1. Machine-specific Danobat documentation for serial number 30106A.
2. Original Danobat PLC, electrical drawings, machine parameters, and existing
   known-good programs from this exact machine.
3. Official Fagor 8055M documentation matching the installed CNC version.
4. Directly observed behavior on this exact machine, documented by the operator.
5. User-confirmed machine measurements and direction tests.
6. Generic Fagor documentation.
7. Generic CNC or grinding knowledge.

Generic milling-machine behavior, internet examples, or behavior from another
Danobat machine must never override machine-specific evidence.

If authoritative sources conflict, stop and ask the user. Do not silently choose
one interpretation.

Every machine rule stored by the application should have a provenance status:

- `manufacturer-confirmed`
- `controller-manual-confirmed`
- `existing-program-confirmed`
- `operator-observed`
- `operator-entered`
- `assumed`
- `unknown`

Only confirmed or explicitly operator-approved rules may be enabled for normal
export. Assumed and unknown rules must produce blocking errors unless the user
uses a clearly identified expert override.

---

## 4. Known machine identity

Current known machine profile:

- Manufacturer: Danobat
- Model: RTM-2500
- Machine type: surface grinder
- Machine serial number: 30106A
- CNC: Fagor 8055M
- Programming family: Fagor ISO-style CNC programming
- Primary use: surface grinding
- Installed diamond dresser: servo-controlled mounted dresser
- Dresser-axis letter: V

Do not change these values globally without explicit user confirmation.

Some available manuals may be labeled Fagor 8050. Treat them as supporting
references only. Do not assume every 8050 function, parameter, or syntax is
identical to the installed 8055M control.

---

## 5. Confirmed axis meanings

The established machine-axis meanings are:

| Axis | Confirmed physical meaning |
|------|----------------------------|
| X | Longitudinal table movement |
| Y | Vertical grinding-head movement |
| Z | In/out cross movement across workpiece depth/width |
| V | Servo-driven diamond dresser axis |

Never use generic milling conventions to reinterpret these axes.

### 5.1 Critical terminology

Avoid the unqualified word `depth` in the UI and internal model because it can
mean two different things:

- `Y downfeed` means vertical grinding-wheel movement and material removal.
- `Z cross travel` means in/out travel across the workpiece width/depth.

Use explicit names such as:

- `tableTravelX`
- `verticalDownfeedY`
- `crossTravelZ`
- `dresserInfeedV`

Do not name a field only `depth`, `feed`, `step`, `position`, or `travel`.

### 5.2 Axis direction signs

Operator information currently indicates:

- Rightward X table motion is negative X.
- Leftward X table motion is positive X.

This must remain configurable and must be physically reconfirmed during machine
commissioning.

The positive and negative physical directions of Y, Z, and V are not considered
fully confirmed by this file.

Never infer an axis sign from words such as:

- inward
- outward
- up
- down
- front
- back
- left
- right

Store logical physical directions separately from numerical signs in the
machine profile.

The UI must show both, for example:

`Move table right: X−`

The generator must derive the numerical sign from the selected machine profile,
not from scattered hard-coded multipliers.

---

## 6. Confirmed basic Fagor programming conventions

The following codes are currently accepted for this application:

| Code | Meaning |
|------|---------|
| G71 | Metric programming in millimetres |
| G94 | Feedrate in millimetres per minute |
| G90 | Absolute coordinate programming |
| G91 | Incremental coordinate programming |
| G01 | Controlled linear movement |
| S... | Programmed spindle-speed request |
| M03 | Clockwise spindle-start request through CNC/PLC interface |
| M05 | Spindle-stop request |
| M30 | End of program |

Comments may be written using the Fagor-supported semicolon comment form.

Block numbers are optional unless the selected machine profile requires them.

Use `M03`, not inconsistent mixtures of `M3` and `M03`, in generated output.
Use consistent uppercase formatting.

### 6.1 Mandatory explicit modal header

Do not rely on modes left active by a previous program.

A normal metric program should explicitly establish at least:

```text
G71
G94
G90

If the movement cycle uses incremental programming, switch to G91 immediately
before the incremental section.

6.2 Mandatory end-state handling

If G91 is used anywhere, restore G90 before program termination.

A normal spindle program should explicitly stop the spindle before M30:

M05
G90
M30

Machine-specific coolant-off, chuck, hydraulic, or safe-position commands may
only be inserted when their exact codes and required sequence are confirmed.

6.3 Feedrate modality

Feedrate is modal. Never accidentally allow a fast X feedrate to be inherited by
a Y, Z, or V movement.

Every movement category must have an independently named and validated feed:

X grinding/traverse feed
Y vertical/downfeed feed
Z cross-feed
V dresser feed

When movement changes from one category to another, emit the appropriate F
value explicitly unless the code generator has formally proved that the active
feed is already correct.

6.4 Spindle command limitations

S500 M03 has been entered on the machine.

Observed behavior:

PLC output O02 changed from 0 to 1.
A relay sound was heard in the electrical cabinet.
The spindle did not physically start during that troubleshooting event.
No ordinary CNC error was shown.

Therefore:

M03 is considered a spindle-start request.
O02 = 1 is evidence of the PLC output request, not proof of spindle rotation.
The application must not report spindle running merely because it emitted
M03.
Do not infer that all guards, relays, drives, contactors, lubrication systems,
or other interlocks are satisfied.
Do not add PLC bypasses or workarounds.
7. Machine-specific functions that remain unconfirmed

The following functions must not be assigned invented M-codes:

Grinding-wheel coolant
Diamond-dresser coolant
Water-oil/coolant pump sometimes described as Z1 pump
Hydraulic-pump control
Table hydraulic output
Magnetic chuck on
Magnetic chuck off
Demagnetization cycle
Main machine-door unlock
Grinding-wheel guard unlock
Auxiliary head unlock
Axis-reference cycle
Automatic dresser cycle
Dresser return/home cycle
Wheel warm-up cycle
Automatic spindle verification
Safe retract or machine-home movement
Alarm reset
Program-controlled interlock bypass

Store these as nullable or unknown profile values.

Unknown must never be represented as zero because zero can be interpreted as a
real command or coordinate.

If the user enters a machine-specific M-code, store:

Exact code
Function description
Source
Date confirmed
Machine serial number
Required preconditions
Required postconditions
Whether it has been physically tested
Notes about observed PLC inputs/outputs

Machine-specific codes must be configurable rather than hard-coded throughout
the generator.

8. Known PLC and diagnostic observations

These details are diagnostic evidence, not general G-code commands:

PLC input I10 has been associated with the Spanish label
defensa muela cerrada, meaning grinding-wheel guard closed.
PLC output O02 changes when the spindle-start command is requested.
Marcha cabezal muela refers to grinding-wheel head/spindle run.
El cabezal muela está parado means the grinding-wheel spindle/head is
stopped.
La defensa de la máquina está abierta means the machine guard/door is open.
Selector con llave means key selector.
Realizar referencia de ejes means perform/reference the axes.
Salida mesa refers to a table output.
The precise role of SQ60 in spindle operation has not been confirmed.
The exact meaning and logic of
Reglaje máquina selección muela desbloqueo cabezal auxiliar
must be checked against the original PLC/electrical documentation.

Do not generate G-code using PLC address names such as I10, O02, or SQ60.
They are diagnostic identifiers unless machine documentation explicitly proves
otherwise.

9. Information that must remain configurable or unknown

Do not invent values for:

X, Y, Z, or V software limits
Physical hard limits
Maximum safe table stroke
Maximum workpiece height
Maximum allowable wheel RPM
Minimum safe wheel RPM
Acceleration or deceleration
Safe approach clearance
Safe retract clearance
Wheel-to-workpiece clearance
Wheel-to-dresser clearance
Wheel width
Installed wheel diameter
Wheel bore
Remaining wheel diameter
Maximum dresser infeed
Dresser sign direction
Grinding downfeed
Spark-out count
Coolant delay
Spindle acceleration delay
Coordinate-system offsets
Axis-reference positions
Backlash compensation
Permitted decimal precision
Controller block-length limit
Program-number restrictions
File extension
File encoding
Serial-transfer parameters
Machine-specific M-codes

The user has discussed purchasing a wheel approximately:

Diameter: 500 mm
Bore: 203 mm
Width: 100–150 mm

These are purchasing requirements or candidate dimensions, not proof of the
currently installed wheel or every wheel permitted by the machine.

Do not use them as hidden defaults.

10. Application architecture requirements

Keep the safety-critical generation logic independent from the graphical UI.

Prefer these conceptual layers, adapted to the existing framework:

Domain
units
axes
directions
machine profile
grinding parameters
provenance status
Planning
converts user intent into a typed motion plan
contains no formatted G-code strings
Validation
validates inputs, geometry, machine limits, modal state, and trajectory
returns structured errors and warnings
PostProcessor
converts a validated motion plan to Fagor 8055M syntax
ParserSimulator
parses the generated output independently
reconstructs modal state and final trajectory
Presentation
Windows UI
preview
warnings
export confirmation
Persistence
versioned machine profiles
application settings
no silent migration of safety-critical values

Do not generate final G-code directly inside button click handlers or view code.

The planning, validation, and post-processing layers must be deterministic and
testable without starting the UI.

Use strongly typed units or unmistakably unit-suffixed names. Never pass an
unlabeled generic number where it could represent millimetres, inches, RPM, or
millimetres per minute.

11. Numerical correctness requirements

All calculations and exported numbers must use invariant formatting:

Decimal separator must be .
Never emit a locale-dependent comma decimal
Never emit thousands separators
Never emit scientific notation
Never emit NaN
Never emit positive or negative infinity
Normalize negative zero to zero
Reject non-finite values
Reject empty or partially parsed numeric text
Do not silently truncate fractional input

The application may run under Persian, English, German, or other Windows locale
settings. Generated G-code must remain identical for identical semantic inputs.

For movement-step calculations:

Avoid uncontrolled binary floating-point accumulation.
Prefer decimal arithmetic or an integer-scaled representation.
Calculate full steps and the final remainder explicitly.
Never overshoot a requested endpoint merely to make all steps equal.
Suppress a final remainder move when it is zero within the defined numerical
tolerance.
Do not create microscopic accidental moves due to rounding.
Round only at a defined boundary and document the precision.
Validate the rounded coordinates, not only the unrounded internal values.
12. Required input model

Do not use one ambiguous collection of text fields.

A grinding operation should explicitly represent, where applicable:

Selected machine profile
Units
Coordinate mode
Known current/start position for X, Y, Z, and V
Workpiece X length
Workpiece Z width
Left X overtravel
Right X overtravel
Z entry allowance
Z exit allowance
Wheel contact width when relevant
X stroke start coordinate
X stroke end coordinate
Z coverage start coordinate
Z coverage end coordinate
X grinding feedrate
Z cross-feed rate
Y downfeed rate
V dresser feedrate
Spindle RPM
Spindle direction
Cross-step amount
Cross-step timing
Vertical infeed amount
Number of roughing passes
Number of finishing passes
Spark-out strokes or cycles
Whether coolant commands are enabled
Whether dressing is enabled
Program number/name
Operator notes

Do not assume the workpiece dimension is the same as required axis travel.
Overtravel, wheel width, fixture clearance, and selected reference point affect
the actual trajectory.

Every displayed input must state its unit.

13. Grinding-path behavior

The generator must distinguish between these cross-feed strategies:

Cross-step after every X stroke.
Cross-step after a complete X out-and-back cycle.

The user's earlier example used an out-and-back X cycle followed by one Z step.
Do not silently replace that behavior with a step after every stroke.

The UI must show the selected strategy in plain language and preview the exact
motion sequence.

For a reciprocating path:

Alternate X endpoints correctly.
Do not unintentionally repeat two moves in the same direction.
Track the actual current X endpoint.
Apply Z movement only at the intended end of the stroke or cycle.
Do not add a final Z step after the requested Z coverage is already complete.
Handle the final partial Z step.
Calculate the exact final X, Y, Z, and V positions.
Report whether the program ends at the starting X side or opposite side.
Never claim that the machine returns to its initial position unless every
changed axis actually returns.

The direction of the first X stroke must be explicit. Never assume positive X
means right.

14. Absolute and incremental programming

Support G90 and G91 through a common semantic motion model.

Do not maintain separate, inconsistent path algorithms for absolute and
incremental output.

For G91:

Accumulate every move from the known starting position.
Validate the accumulated absolute trajectory against machine limits.
Never validate only individual increments.
Restore G90 before ending the program.
Show cumulative final positions in the preview.

For G90:

Every generated target must be validated in the active coordinate system.
Do not assume machine coordinates and work coordinates are identical.
Do not emit G53, G54, G28, or another coordinate-selection command unless its
behavior is confirmed for this machine profile.
Require the reference origin or starting coordinates needed to interpret the
program.

Switching output mode must not change the intended physical trajectory. Tests
must prove equivalence between G90 and G91 output for the same operation.

15. Dresser handling

The dresser is mounted on a servo-controlled axis identified as V.

Do not substitute Y for V.

A requested dressing amount such as 0.05 mm is not enough information to
generate a safe dressing cycle. The generator also needs confirmed information
about:

V positive direction
V zero/reference method
Current dresser position
Wheel/dresser contact position
Approach clearance
Dressing infeed
Traverse behavior
Dresser feedrate
Wheel-running requirement
Coolant requirement
Retract amount
Maximum permitted penetration
Number of dressing passes
Whether the value means radial removal, diameter reduction, or axis travel

Until those items are confirmed, dresser generation must remain disabled or
produce a blocking validation error.

Never infer wheel contact using only a feeler-gauge value without an explicit,
reviewable setup procedure.

16. Safety validation pipeline

Generation must follow this sequence:

Raw UI input
→ strict parsing
→ typed request
→ machine-profile validation
→ geometric planning
→ absolute trajectory calculation
→ limit and clearance validation
→ modal-state construction
→ G-code formatting
→ independent parse/simulation
→ semantic comparison
→ preview
→ explicit export

If any blocking validation fails, do not produce an exportable program.

16.1 Input validation

Reject:

Missing required input
Zero or negative length
Zero or negative feedrate
Zero or negative step size
Invalid RPM
Non-finite values
Values outside confirmed profile limits
Reversed ranges unless direction is explicitly represented
Unknown starting position when limit validation requires it
Unconfirmed axis direction
Unconfirmed required M-code
Contradictory options
A step count exceeding the configured safe maximum
A generated file exceeding controller limits
16.2 Trajectory validation

For every motion block:

Resolve it to an absolute position.
Verify all involved axes.
Verify start and target positions.
Check configured soft limits.
Check operation-specific clearance rules.
Track modal coordinate mode.
Track active feedrate.
Track spindle request state.
Track coolant request state when configured.
Track dresser state when configured.

Validate the complete segment where practical, not merely its endpoint.

16.3 Semantic round-trip validation

After formatting G-code:

Parse the generated text using an independent parser path.
Reconstruct all modal state.
Reconstruct the absolute trajectory.
Compare it with the planned trajectory.
Compare the final state and final positions.
Block export if they differ.

Do not validate generated text using only the same string-building logic that
created it.

17. Output requirements

Generated programs must be deterministic.

The same request, machine profile, and application version must generate
identical output.

A normal output should contain:

Program identification comment
Generator version
Machine model
Machine-profile version
Units
Feed mode
Initial coordinate mode
Clearly separated setup, grinding, and shutdown sections
Explicit feeds for different movement categories
Spindle stop
G90 restoration
M30

Do not include timestamps in the G-code itself if they make deterministic tests
impossible. Put changing metadata in a separate generation report if needed.

Use conservative, readable expanded code by default.

Loops, parameters, IF, GOTO, and subroutines may be supported only when:

Syntax is confirmed for the installed Fagor control.
The generated behavior can be simulated.
Expansion and maximum iteration count are known.
The preview shows the complete resulting trajectory.
Tests compare compact and expanded forms.

Readable repeated blocks are preferred over an elegant loop that is harder for
an operator to audit.

18. Comments and injection prevention

User-entered names and comments must never be able to inject executable blocks.

Sanitize or reject:

Newlines
Carriage returns
Semicolons used as syntax delimiters
Control characters
Program delimiters
Unsupported Unicode
Strings that can escape a comment and create another block

Do not concatenate untrusted text directly into executable G-code.

Generated executable words must come from typed internal objects, not arbitrary
user strings.

An expert raw-code feature, if ever added, must be isolated, visibly unsafe,
disabled by default, and excluded from ordinary safety guarantees.

19. Machine-profile requirements

Machine configuration must be stored in a versioned schema.

Each safety-critical setting must support:

Value
Unit
Confirmation status
Source
Date confirmed
Notes
Profile version

A missing limit must be unknown, not an enormous artificial default.

Loading an older profile must not silently introduce defaults for new
safety-critical fields.

If migration cannot preserve meaning, require operator review.

Validate profile consistency, including:

Minimum less than maximum
Direction mapping is complete
Feed ranges are positive
RPM range is ordered
Safe coordinates fall within limits
M-codes have valid syntax
No two mutually exclusive functions accidentally share a code
Required shutdown codes are present when their features are enabled

Corrupt or partially parsed profiles must fail closed.

20. User-interface requirements

The UI must prioritize preventing operator misunderstanding.

Before export, display:

Machine model and profile version
Coordinate mode
Unit system
X direction convention
Start position
Minimum and maximum position reached by each axis
Final position of every axis
Total stroke count
Total cross-step count
Total vertical infeed
Estimated program distance
Spindle command and RPM
Coolant status
Dresser status
All warnings and overrides
Full generated G-code

Use separate visual states for:

Validated
Warning
Blocking error
Unconfirmed machine data
Expert override

Do not use Safe as an absolute claim. Prefer wording such as:

Validation passed for configured limits
Machine interlocks not simulated
Operator review required

Changing an input must invalidate any previous validation or export approval.

Do not allow stale preview data to be exported after inputs or machine profile
values change.

The export button must operate on the exact validated snapshot shown in the
preview.

21. Export and file handling

Export must be atomic where supported:

Generate into memory.
Validate.
Write to a temporary file.
Flush and close it.
Replace or rename to the destination.

Do not leave a partially written CNC program with a normal executable filename.

If overwriting an existing program:

Show the exact path.
Require confirmation.
Prefer a backup or recoverable version.
Never silently overwrite.

The filename must be sanitized for Windows and for the controller’s confirmed
naming restrictions.

File encoding and line endings must be machine-profile settings.

Until controller-transfer testing confirms otherwise:

Prefer conservative ASCII-compatible G-code.
Avoid Persian text inside exported CNC programs.
Keep UI translations separate from machine program text.
Do not assume UTF-8 is accepted by the CNC.
Do not assume .nc is the required extension.
22. Testing requirements

A change to generation, validation, machine profiles, units, axes, or export is
incomplete without tests.

22.1 Unit tests

Test at minimum:

G90 trajectory generation
G91 trajectory generation
G90/G91 physical equivalence
Positive and negative X directions
Rightward X mapped to negative X
Alternating stroke endpoints
Cross-step after every stroke
Cross-step after an out-and-back cycle
Exact step divisibility
Final partial step
Step larger than remaining distance
No extra final step
One-stroke operation
One-cycle operation
Zero dimensions
Negative dimensions
Zero feedrate
Negative feedrate
Zero RPM
Values at limits
Values immediately outside limits
Unknown limits
Unknown starting position
Decimal rounding boundaries
Negative-zero normalization
Very small valid values
Very large values
NaN and infinity rejection
Persian/Arabic digits if the UI accepts them
Decimal comma rejection or intentional conversion
Persian Windows locale
English Windows locale
Repeated generation produces identical output
Final G90 restoration
M05 before M30
Correct feedrate on X movement
Correct feedrate on Y movement
Correct feedrate on Z movement
Correct feedrate on V movement
Sanitization of comments
Newline injection attempts
Corrupt machine-profile rejection
Unknown M-code blocking
Disabled optional features emitting no commands
22.2 Property-based tests

Where supported, generate many valid and invalid parameter combinations.

Required invariants include:

No emitted number is non-finite.
No emitted coordinate uses locale-dependent formatting.
No validated absolute position exceeds configured limits.
Final simulated positions equal planned final positions.
Every G91 program ends in G90.
Every spindle-start request is followed by M05 before M30.
Changing output mode does not change physical trajectory.
Cross-travel never exceeds the requested planned coverage.
Step count is finite and bounded.
Exported code can be parsed by the internal independent parser.
22.3 Golden tests

Maintain reviewed expected-output files for representative programs:

Simple X reciprocation
Multi-pass X/Z surface grinding
Remainder cross-step
Absolute version
Incremental version
Finish/spark-out cycle
Valid spindle request
Program without unconfirmed optional M-codes

Golden files must be intentionally reviewed when changed. Do not automatically
accept large snapshot changes without explaining them.

22.4 Integration tests

Test:

UI input to generated preview
Input modification invalidates preview
Machine-profile modification invalidates preview
Invalid input prevents export
Exported bytes match previewed bytes
Cancelled export creates no final file
Overwrite confirmation
Application restart and profile reload
Older-profile migration failure behavior
Locale-independent operation
Unexpected exceptions do not create a usable partial program
22.5 Regression tests

Every discovered generator bug must receive a regression test that fails before
the fix and passes afterward.

Do not remove a regression test simply because the implementation changes.

23. Review requirements for safety-critical changes

Treat these as high-risk changes:

Axis mapping
Axis sign mapping
Unit conversion
Coordinate-mode handling
Machine limits
Step/remainder calculations
Spindle or coolant commands
Dresser logic
Magnetic-chuck logic
Program termination
Export behavior
Machine-profile migrations
Parser/simulator behavior

For high-risk changes:

State the existing behavior.
State the proposed behavior.
Identify affected generated blocks.
Add boundary and regression tests.
Show representative before/after G-code.
Run the complete test suite.
Report anything that could not be tested.
Require operator review before real-machine use.

Do not hide safety-relevant changes inside formatting or refactoring commits.

24. Error-handling rules

Fail closed.

If the application cannot prove that export is valid under the configured
rules, block export and explain why.

Do not:

Replace invalid numbers with zero
Clamp motion silently
Guess a missing direction
Guess a missing limit
Ignore a failed profile load
Continue after parser/simulator disagreement
Export stale output after an exception
Swallow validation exceptions
Show success when file writing only partially completed

Errors must identify:

Field or generated block
Invalid value
Required condition
Whether the problem is an input, profile, calculation, or unknown-machine-data
issue

Do not expose low-level stack traces as the only operator message. Preserve
technical diagnostics for logs.

25. Logging and auditability

For each generation, make it possible to record:

Application version
Machine-profile version
Input snapshot
Validation results
Warnings
Expert overrides
Generated program hash
Export path
Export success or failure

Do not record passwords, API keys, or unrelated personal information.

If expert override exists, record exactly which blocking condition was
overridden. A generic ignore all warnings option is prohibited.

26. Security requirements

Treat project files, imported profiles, templates, and user comments as
untrusted input.

Prevent:

G-code injection
Path traversal
Arbitrary file overwrite
Loading executable content as configuration
Unsafe deserialization
Shell-command construction from user input
Secrets in source control
Automatic execution of downloaded files

Do not require administrator privileges unless a verified feature genuinely
requires them.

The basic G-code generator must not need network access.

27. Development-quality rules

Follow the existing project’s established style and architecture.

Prefer:

Small focused changes
Pure functions for calculations
Immutable validated request models where practical
Explicit types
Descriptive names
Structured validation results
Dependency injection for file and time services
Tests at the lowest useful layer
One authoritative implementation of trajectory mathematics

Avoid:

Duplicate coordinate calculations
Magic numbers
Hidden unit conversions
Boolean parameters with unclear meaning
Global mutable machine state
UI code containing safety calculations
Catch-all exceptions that continue execution
Comments that contradict code
Large unrelated refactors
Disabling tests to make a build pass

Do not add dependencies unless they provide clear value and are compatible with
the project license and Windows target.

28. Required commands and verification workflow

After changes, use the project’s actual build and test commands.

At minimum:

Restore dependencies.
Build in the normal development configuration.
Build in release configuration.
Run all unit tests.
Run all integration tests that are available.
Run formatting/static analysis.
Inspect the final diff.
Confirm no unrelated files changed.
Report commands run and their results.
Clearly state anything not tested.

If the repository lacks tests, create a test project appropriate to the existing
technology before modifying safety-critical generation behavior.

Never report all tests passed unless the commands actually completed
successfully.

29. Real-machine commissioning rules

Software validation does not replace machine commissioning.

Before using a newly generated program on the Danobat:

Back up the CNC programs and machine parameters.
Confirm the correct machine profile.
Reference/home the required axes using the approved machine procedure.
Confirm axis directions manually at low speed.
Confirm the program zero and current positions.
Verify fixtures, workpiece, wheel, guards, and clearances.
Review every generated block.
Simulate on the Fagor control if available.
Use dry run or graphics mode when available.
Run in single-block mode initially.
Use reduced feed override.
Keep a hand near feed hold/emergency stop.
Verify spindle and coolant separately.
Keep the wheel clear of the workpiece during the first motion test.
Confirm that the first move travels in the expected physical direction.
Stop immediately if displayed and physical motion disagree.

The application must include an operator-facing notice that machine interlocks
and physical setup are outside the generator’s software simulation.

30. Current known examples

An earlier incremental movement pattern was conceptually:

G71
G94
G91

S500 M03

G01 X600 F1000
G01 X-600
G01 Z-40 F200

M05
G90
M30

This is an example of syntax and sequencing only.

It must not be treated as a safe production program because the following are
not established by the example:

Actual starting coordinates
Confirmed axis limits
Workpiece location
Fixture location
Correct Z sign
Required overtravel
Safe feedrates
Safe spindle RPM for the installed wheel
Wheel clearance
Coolant command
Interlock state
Whether a 40 mm cross-step is appropriate
Whether one Z step per round trip is intended
Required number of cycles

Never place these example values into production defaults.

31. Prohibited actions

Never:

Invent Danobat M-codes.
Assume generic Fanuc behavior.
Treat Fagor as Fanuc.
Swap Y, Z, or V based on milling conventions.
Generate dresser movement on Y.
Bypass guards, doors, PLC logic, or spindle interlocks.
Hard-code unknown machine limits.
Interpret O02 = 1 as proof of spindle rotation.
Use the candidate wheel dimensions as installed-wheel facts.
Hide G91 state at program end.
Apply X feedrate accidentally to Y, Z, or V.
Use locale-dependent decimal formatting.
export when trajectory simulation disagrees with the plan.
Automatically send or execute generated code.
Tell the operator a program is guaranteed safe.
make a machine-specific assumption merely to finish a feature.

When required information is missing, stop and ask a precise question.

32. Definition of done

A feature is complete only when:

Requirements and machine assumptions are explicit.
No unconfirmed machine behavior is presented as fact.
Domain logic is separate from UI formatting.
Input validation is implemented.
Absolute trajectory validation is implemented.
Relevant machine limits are checked or clearly reported as unknown.
Generated code is independently parsed/simulated.
Planned and parsed trajectories match.
Final modal and axis states are displayed.
Blocking errors prevent export.
Relevant unit, boundary, regression, and integration tests pass.
The application builds successfully.
The final diff is reviewed.
Documentation is updated.
Remaining unknowns and real-machine tests are disclosed.

Passing this definition of done means the software passed its configured checks.
It does not guarantee safe machine operation without operator review and
commissioning.

33. Immediate project priorities

Unless the user specifies otherwise, proceed in this order:

Inspect and document the existing application architecture.
Establish a versioned Danobat RTM-2500 machine profile.
Build typed operation and motion-plan models.
Implement strict invariant numeric parsing and formatting.
Implement trajectory planning independent of G-code.
Implement complete absolute-position simulation.
Implement validation and blocking-error handling.
Implement the Fagor 8055M post-processor.
Implement independent round-trip parsing.
Add preview and final-state reporting.
Add safe atomic export.
Add comprehensive tests.
Add optional machine-specific functions only after confirmation.
Do not implement direct CNC transfer until generation and validation are
mature and separately reviewed.
34. Questions Codex must ask when necessary

Ask the user instead of guessing when work depends on:

Current application framework or intended framework
Exact machine soft limits
Axis positive directions
Starting coordinate/reference method
Workpiece zero definition
Installed wheel dimensions
Safe wheel RPM
Safe X, Y, Z, or V feedrates
Required cross-feed strategy
Actual material-removal/downfeed sequence
M-codes for coolant, chuck, hydraulics, or dresser
Program-file format and encoding
Fagor software version
Behavior confirmed only in a different machine/manual
Whether an operation will be tested offline or on the real machine

Questions should be specific and explain what behavior depends on the answer.

35. Final instruction

Accuracy and traceability are more important than completing a feature quickly.

When uncertain:

Preserve the uncertainty.
Block unsafe export.
Identify the missing evidence.
Ask the user for the relevant Danobat/Fagor document, known-good program, PLC
observation, or controlled machine test.
Add the confirmed result to the versioned machine profile and tests.

Never convert uncertainty into executable machine motion.
