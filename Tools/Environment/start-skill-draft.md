# FirstGame cloud task startup

Use the existing checkout at `/workspace/firstgame`. Each cloud task is already isolated. Do not create a Git worktree unless the user explicitly requests one. Read the root and relevant game `AGENTS.md`, then `Docs/Development.md` and the current validation records before editing.

## Retained tools and runtime prerequisites

The prepared filesystem contains Unity **6000.3.26f1 (de8167e7440c)** with Windows Mono and WebGL modules, Unity CLI **1.0.0-beta.13**, the official Codex Unity plugin **0.1.8-beta**, .NET SDK **8.0.426**, Blender **4.3.2**, and workspace-local Xvfb. The two independent Unity projects are `Games/FantasyCards` and `Games/PathOfDawn`; both pin Pipeline **0.8.0-exp.1**.

```bash
cd /workspace/firstgame
source Tools/Environment/activate-shell.sh
python3 Tools/Environment/check-runtime.py
```

The runtime check prints only redacted status and must report `runtimeReady:true` with exit 0 before Editor work. Exit 4 means no active Editor license. The dependency installer deliberately allows that known external prerequisite to be absent; its exit 0 alone is not runtime readiness. Unexpected command/JSON/schema failures remain failures. Run `bash Tools/Environment/install.sh` only when retained tools are missing or installation needs repair. It verifies downloads and installs missing modules without replacing source files.

The current onboarding session successfully signed in through the browser, activated Unity Personal after the user's explicit eligibility/terms acknowledgement, and ran a real Editor project-creation/compilation smoke check with exit 0. Do not assume account tokens, machine-bound license state, background processes, or an Editor connection survive publication or a new task. Recheck actual state. Reuse an existing valid login/license. Do not ask again for consent already granted in the continuing session. If authentication expires, use the official browser `unity auth login` flow; never request credentials in chat or print/copy tokens or license files. Preserve license verification and perform the native check again after a meaningful state change.

## Processes that must be started again

For Editor UI/Play Mode and screenshots:

```bash
cd /workspace/firstgame
bash Tools/Environment/start-display.sh
source Tools/Environment/activate-display.sh
source Tools/Environment/activate-shell.sh
```

`start-display.sh` reuses the task's authenticated display when it is reachable, or starts Xvfb `:99` at 1920×1080 with TCP disabled. It verifies display access and GL. Expected renderer: Mesa llvmpipe / OpenGL 4.5. This is software rendering for functional verification, not a Windows PC performance measurement.

Before starting any Editor, inspect `unity editors running --format json` and the task's running processes. A project can have only one owner Editor at a time. Wait for that project's prepare/test/build operation to finish, or use its already-running Editor. Do not launch a second Editor against that project, remove its lock file, terminate another worker's operation, or modify a shared scene/prefab concurrently. Use explicit project paths whenever two Editors may exist.

When inspecting processes, print names and IDs rather than full command lines: the official CLI can pass an access token to its Editor child. Do not copy raw CLI logs into deliverable archives.

For a persistent GUI Editor after the project is free, choose the game the task concerns:

```bash
game_project=/workspace/firstgame/Games/FantasyCards
mkdir -p /workspace/firstgame/Artifacts/UnityLogs/FantasyCards
"$UNITY_EDITOR" -projectPath "$game_project" -force-glcore \
  -logFile /workspace/firstgame/Artifacts/UnityLogs/FantasyCards/interactive.log
```

Run that long-lived command through the terminal's yielded process/session facility so other tools remain usable. Do not add `-quit`. Use `Games/PathOfDawn` and its own log directory for the narrative game. A resident Editor holds a license seat until it exits.

## Official CLI / Pipeline readiness

After the GUI Editor opens, run these in another terminal after sourcing `activate-shell.sh`:

```bash
game_project=/workspace/firstgame/Games/FantasyCards
unity pipeline list --format json
unity status --until-ready --timeout 45 --project-path "$game_project" --format json
unity command --project-path "$game_project" --detail compact --format json
unity command editor_status --project-path "$game_project" --result-only --format json
unity command eval 'return UnityEngine.Application.unityVersion;' \
  --project-path "$game_project" --result-only --format json
```

Require actual command success and all matching status fields: `status:ready`, `compiling:false`, `domainReloadInProgress:false`, `playMode:stopped`, exact `projectPath`, and `unityVersion:6000.3.26f1`. Confirm the read-only eval actually returns `6000.3.26f1`; a PID, open port, package declaration, or zero exit without the expected result is insufficient. `editor_status` in Pipeline 0.8.0-exp.1 has no parameters; do not invent options such as `includeMemory`.

An Editor still importing may report `STATUS_PIPELINE_LOAD_PENDING`. Inspect its log and wait for the current import/compile to finish. Diagnose compile errors or Safe Mode before retrying. A persistent batch Editor can serve Pipeline even if `unity status` does not list it; in that case, the project-targeted command catalog, `editor_status`, and eval are the functional readiness checks. Do not change connection settings or display credentials just because a sandboxed query cannot see a process; verify its execution context.

Pipeline is already pinned in both manifests. Only repair a missing package through `unity pipeline install --project-path "$game_project" --package-version 0.8.0-exp.1`; inspect the manifest diff. Do not upgrade it during ordinary startup. If experimental CLI/Pipeline fails, continue with supported Editor scripts and batch commands while retaining the exact failure evidence.

## Useful project verification

Select checks for the changed project and requested scope, rather than running every build on every task:

```bash
cd /workspace/firstgame
bash Tools/Validation/run-core-tests.sh
bash Tools/Build/unity-task.sh FantasyCards prepare
bash Tools/Build/unity-task.sh FantasyCards edit-tests
bash Tools/Build/unity-task.sh FantasyCards play-tests
```

Use `PathOfDawn` for that project. `Tools/Build/unity-task.sh` also supports `profiles`, `windows`, and `web`. It preserves runner exit status and checks actual XML test results. Do not start these batch operations while an interactive Editor owns the same project. Keep the two expensive WebGL builds sequential unless resource evidence supports concurrent execution. The host reports five logical CPUs, but its verified cgroup budget is four CPUs and 32 GiB RAM, with software graphics; recheck disk before builds.

Use Xvfb `import -window root <absolute-png-path>` or an actual browser screenshot of the built WebGL player to capture the complete UI. Pipeline's `screenshot` implementation renders a Camera to a RenderTexture and can omit ScreenSpaceOverlay Canvas UI; it is not interchangeable with whole-screen capture. Distinguish Blender renders, empty display diagnostics, actual Unity play screenshots, and actual WebGL screenshots. Never present a proxy asset as final art or infer 60fps from software-rendered tests.

For an existing current Web build, the browser check starts its own loopback-only HTTP server and closes it afterward:

```bash
python3 Tools/Environment/install-browser.py
python3 Tools/Validation/web-smoke.py --game FantasyCards --tessdata /workspace/tools/tesseract
```

Use `PathOfDawn` for the narrative player. The prepared base image has Chromium 154.0.8037.57, Python Playwright 1.62.0, Pillow 12.3.0, and Tesseract 5.5.0. `install-browser.py` checks these tools and installs or verifies Korean/English OCR data under `/workspace/tools/tesseract` against an official pinned commit and SHA256 hashes. It was tested both with missing OCR data and on repeat runs. It does not change browser profiles or upgrade packages; missing base packages require the documented repair in `Docs/Environment.md`. Run software browser checks after expensive native/Web compilation has finished, and close each test browser before starting another. Capture timeouts occurred while first builds and software rendering ran together during onboarding. The smoke script uses a new isolated browser context, actual pointer/keyboard input and screenshots, and reports under `Artifacts/Validation/WebSmoke/<Game>/`. A single-game invocation replaces `summary.json` for that invocation; read each game's report when summarizing both. Software SwiftShader timings do not establish target-device performance. Do not publish localhost links to the user.

The final FantasyCards Web smoke passed with the default headless SwiftShader renderer. PathOfDawn encountered a screenshot timeout while Unity and its WebGL context remained responsive; the same final player passed with X11/Mesa llvmpipe and a 720p interaction viewport. For that reproducible narrative check, use the authenticated display and these tested options:

```bash
cd /workspace/firstgame
bash Tools/Environment/start-display.sh
source Tools/Environment/activate-display.sh
python3 Tools/Validation/web-smoke.py --game PathOfDawn --renderer x11-mesa \
  --interaction-resolution 1280x720 --tessdata /workspace/tools/tesseract
```

This still captures actual title and saved-game canvases at both 1280×720 and 1920×1080. The runner records the observed renderer, build hashes, request methods/statuses/timing and all original diagnostics. UnityCache can complete a data-file HTTP 304 revalidation with a transport abort; classify only when the exact URL has the expected cache log and the functional restore passes. Keep genuine unexpected network failures fatal. Preserve failed attempt reports before rerunning, and do not turn a screenshot timeout into an assertion of either a game crash or a successful check. Internal shader support diagnostics remain on these software renderers; actual GPU quality and performance need separate validation.

Report dependency installation, account/license readiness, actual Pipeline connection, native Unity tests, target builds, and real player manipulation as separate outcomes. Keep unrun, failed, skipped, and passed checks distinct. Configuration draft saving does not publish a new snapshot or prove a subsequent task works.
