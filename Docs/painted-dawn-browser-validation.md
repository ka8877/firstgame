# Painted Dawn browser validation

The `painted-dawn-2026-10-24` revision changes PathOfDawn only. Run
`Tools/Validation/painted-dawn-smoke.py` against its final Web build, sequentially
at desktop 1280×720 and Android browser emulation 844×390. The latter is not a
physical mobile device or a performance benchmark. Source the existing display
activation script before using `--renderer x11-mesa`.

```bash
source Tools/Environment/activate-display.sh
python Tools/Validation/painted-dawn-smoke.py --profile desktop --output Artifacts/Validation/PaintedDawn/Desktop720 --pause-on-ocr-failure
python Tools/Validation/painted-dawn-smoke.py --profile mobile-landscape --output Artifacts/Validation/PaintedDawn/Mobile844 --pause-on-ocr-failure
```

Supply `--tessdata` or `FIRSTGAME_TESSDATA` when Korean OCR models are outside
the system search path. A public run adds `--base-url` and first compares every
published game runtime file to the exact final local build. Use a separate
output directory for the public report. Previous attempts are archived, never
silently overwritten.

The suite reuses the existing passive audio observer, browser transport and
original screenshot capture. It uses actual mouse/keyboard or touch input;
it does not call Unity methods, read gameplay state, inject saves or alter
canvas geometry. It walks through the opening, shore choice, stone room,
manual save/full page reload, second choice and witness ending. Native tests
cover the other routes and stage internals.

Original frames retain the shore and room, all three illustrated speaking
characters, actual walking frames, scenery gesture, restored room and ending.
The functional report leaves `visualReview.status` pending. Direct visual review
must separately assess the exact original screenshots and runtime hashes:
character faces, alpha cutout edges, foot placement, dialogue focus, background
composition, readable UI and mobile clipping. A nonblank or changed-pixel check
does not certify visual quality or walk animation quality.

Only critical transitions use exact Korean OCR: dialogue completion, choices,
interaction prompts, room-arrival dialogue, save acknowledgement and ending. Decorative
title/name OCR cannot block the art review. If OCR cannot read a live transition,
the optional bounded pause accepts a recorded region/segmentation correction
while preserving the exact expected text. It cannot turn an assumed scene into
a pass.

Audio evidence retains the seven BGM windows (pre-gesture, started, mute,
unmute, reduced/restored volume and after Continue) and five opening voice
windows (replayed, restarted, next line, prior line cancelled, natural end).
The former duplicate muted-voice opening is not repeated. Passive PCM proves
browser audio output, not physical speaker playback or spoken-word accuracy.

Release staging must record per-game revisions. FantasyCards keeps its existing
`expanded-demo-2026-10-23` runtime fingerprint and prior reports explicitly;
those reports are reused evidence, not newly executed tests. Its previous
1080-profile incompleteness remains visible. New PathOfDawn evidence must match
the painted revision's actual final files and cannot reuse the older 3D frames.

## Recorded local results — 2026-10-24 revision

| Profile | Functional checks | Directly reviewed original frames | Result |
| --- | ---: | ---: | --- |
| Desktop 1280×720 | 53/53 | 17 | Passed; visual regression review approved |
| Android browser emulation 844×390 | 55/55 | 16 | Passed; visual regression review approved |

The final reports are
`Artifacts/Validation/PaintedDawn/Desktop720/PathOfDawn/report.json` and
`Artifacts/Validation/PaintedDawn/Mobile844/PathOfDawn/report.json`. Each review
records the exact five runtime files and each reviewed original PNG's SHA-256.
The parent agent also directly inspected the key final compositions.

Both runs completed the two real choices and witness ending, room movement,
manual save/full reload/Continue, seven BGM windows and five voice windows.
There were no unhandled script, console or HTTP errors. The recorded Unity
HTTP 304 transport abort is classified only with its exact loader cache-success
log, matching data URL and successful persisted-room restoration.

The mobile run needed one bounded live OCR correction for `다시 듣기`: narrowing
the complete visible text region excluded the rounded button outline and
produced the exact expected Korean text. The expected string remained unchanged;
the original screenshot, attempts and recovery JSON remain in the report.

Direct review confirmed the corrected intro HUD visibility, planted rear idle
pose and nearby name-badge hiding, along with all three character faces,
walking frames, scenery movement, both backgrounds and unclipped dialogue/UI.
An earlier diagnostic desktop run is archived with its failed classifier result
and `needs-fix` visual review. It is not release evidence. These results do not
certify physical mobile performance, human listening quality or user artistic
approval. Public URL verification is recorded separately after deployment.

## Recorded public mobile result

The deployed [PathOfDawn player](https://ka8877.github.io/firstgame/games/path-of-dawn/)
passed **61/61 checks** at the 844×390 mobile profile. The five public runtime
files matched the final local build byte-for-byte before browser input. Actual
touch movement, scenery drag, both choices, witness ending, room save/reload,
seven BGM windows and five voice windows passed with normal TLS verification.
Sixteen original public-browser frames received a separate direct visual review.

Evidence is frozen in
`Artifacts/Validation/PaintedDawn/PublicMobile844/PathOfDawn/report.json`.
The same small replay-label OCR region needed one recorded exact-text live
correction; there were no gameplay-state substitutions or new runtime changes.
The local reports above were not modified by public verification. FantasyCards
was not replayed; its unchanged runtime and earlier evidence remain explicitly
separate.
