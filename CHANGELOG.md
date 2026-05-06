# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.4.0] - 2026-05-05

YouTube Playables certification readiness. Surfaces the lifecycle and audio-state APIs that YouTube cert reviewers test for. Required for any Unity game shipping to YouTube — without these, games cannot satisfy YouTube cert integration requirements #14, #21, and #22.

### Added
- **`Yes2SDK.OnAudioEnabledChange`** event (`Action<bool>`) — fires when the platform's mute state changes. Required by YouTube cert (#14): the game MUST update its audio state to match.
- **`Yes2SDK.Session.IsAudioEnabled()`** — read current platform audio state. Required by YouTube cert (#14) so the game can set its initial mute state at startup. Returns `true` on platforms without a native signal (Poki, CrazyGames, Yandex, GameDistribution).
- **`Platform.YouTube`** and **`Platform.GameDistribution`** values added to the `Platform` enum. Detection wired through `Yes2SDK.GetPlatform()`.

### Fixed
- **`Yes2SDK.OnPause` / `Yes2SDK.OnResume` events now actually fire.** They were declared in 2.3.0 but the JS bridge never sent the SendMessage to invoke them — the events were dead code. Now wired through `Yes2SDK.on('pause' / 'resume', ...)` in the JS bridge after init succeeds. Required by YouTube cert (#21, #22): pause/resume must come from the SDK signal, not `document.visibilitychange` or any other web API.

### Notes
- Subscribe to `OnPause`, `OnResume`, `OnAudioEnabledChange` AFTER `InitializeAsync` has called back successfully. Subscribing earlier is fine (events are static), but events won't fire until the JS bridge has wired them.
- Internal pause-aware data flush + 3 MiB save data guard from Yes2SDK Core 2.0.0-alpha.1 are active for Unity games at runtime — the JS bundle is shared. No Unity-side change needed for those.

### Build Window
- **`Clean Build` button** — wipes the WebGL output folder, then runs a fresh build. Use before YouTube / cert submissions to guarantee a clean upload artifact, or when switching Build Modes (Production / Production Safe / Diagnostic) to avoid pollution from the prior mode's leftover files.
- **`Clear Build Cache` link** (footer) — clears Unity's WebGL incremental build cache (`Library/Bee/artifacts/WebGL`, `Library/PlayerDataCache`, `Library/il2cpp_cache`). Last-resort fix for "build appears to be corrupted" or mismatched-assembly errors that survive a Clean Build. Slower next build (+2-5 min) but does not require a full project reimport.

### Sample integration

```csharp
using Yes2SDK;
using UnityEngine;

public class GameRoot : MonoBehaviour
{
    void Start()
    {
        Yes2SDK.OnPause += OnPlatformPause;
        Yes2SDK.OnResume += OnPlatformResume;
        Yes2SDK.OnAudioEnabledChange += OnPlatformAudioChanged;

        Yes2SDK.InitializeAsync(
            onSuccess: () =>
            {
                // Cert #14: read initial audio state
                if (!Yes2SDK.Session.IsAudioEnabled())
                    AudioListener.volume = 0f;

                Yes2SDK.StartGameAsync();
            }
        );
    }

    void OnPlatformPause()        { Time.timeScale = 0f; AudioListener.pause = true; }
    void OnPlatformResume()       { Time.timeScale = 1f; AudioListener.pause = false; }
    void OnPlatformAudioChanged(bool enabled) { AudioListener.volume = enabled ? 1f : 0f; }
}
```

## [2.3.0] - 2026-04-30

### Fixed
- **`Yes2Log.cs.meta` GUID collision with `com.unity.ai.assistant`** (#39) — meta file shipped with a hand-typed placeholder GUID that collided with the AI Assistant package. Unity silently ignored one of the two, so projects using both packages failed to compile with `CS0103: Yes2Log does not exist`. Regenerated to a proper random GUID.
- **WebGL template `setLoadingProgress` log spam** (#42) — `index.html` forwarded every Unity progress tick to the SDK, which the dashboard inspector logged as "Loading progress" hundreds of times per build. Throttled to forward only when the integer percentage changes — at most 101 calls per build.

### Changed
- **Build Window no longer auto-overrides Player Settings on every build** (#40). The previous version called `BuildConfig.Default.ApplySettings()` inside `BuildGame()`, silently resetting Exception Support / Compression / Stripping / Template each time the user clicked Build. The new Build Window has a collapsible **WebGL Settings** panel that inline-edits Player Settings (no Apply step) and a **Reset to recommended** button for opt-in re-apply. `Yes2SDKBuildGuard` still enforces the `Yes2SDK-SuperSDK` template — the only truly mandatory setting.
- **New `Build Mode` dropdown** with Production / Production Safe / Diagnostic options. Production Safe forces Exception Support to `Explicitly Thrown` for one build (useful when Player Settings is `None`). Diagnostic forces `Full With Stacktrace`. Both restore Player Settings after the build via `IPostprocessBuildWithReport`.
- **`BuildConfig.Default.exceptionSupport`** flipped from `None` → `ExplicitlyThrownExceptionsOnly` (#41). `None` is smaller but breaks projects where a dependency uses `try/catch` (Newtonsoft.Json, etc.). The new default is ~10% larger but compatible with the .NET ecosystem.

### Documentation
- README Build Configuration table reworked: added Memory Size row, expanded Notes column, prominent warning explaining `None` vs `Explicitly Thrown`.
- New "Build Mode for diagnostics" subsection.
- "Where to set these" updated to lead with the Build Window's Settings panel.

### Behaviour notes for users on upgrade

- Existing builds will get `ExplicitlyThrownExceptionsOnly` for Exception Support unless overridden — ~10% larger but more robust.
- The Build Window now respects custom Player Settings rather than silently overwriting them. Verify your active settings via *Yes2SDK > Build Window > WebGL Settings*.
- The `Apply Settings` link has been renamed **Reset to recommended** and moved into the WebGL Settings foldout.

## [2.2.0] - 2026-04-29

### Added
- **`Task`-returning overloads on every async API** with `CancellationToken` support — `InitializeAsync`, `StartGameAsync`, `Auth.*Async`, `Friends.ListFriendsAsync`, `Game.InviteLinkAsync`, `Player.*Async`. Errors throw `Yes2SDKException` (whose `ErrorCode` mirrors the underlying `Error.ErrorCode`), so callers can `try/catch` with timeout via `CancellationTokenSource(TimeSpan)`. Closes #26 and the init-timeout part of #33.
- **`Ads.IsAdShowing()`** — returns `true` while a `ShowInterstitial`/`ShowRewarded` is in flight. Concurrent `Show*` calls are now rejected immediately with `ErrorCode.InvalidParams` (message tagged `AdAlreadyShowing`) instead of putting the SDK in an undefined state. Closes the in-flight-guard part of #27.
- **`Ads.IsRewardedAdAvailable()`** — best-effort readiness check; returns `true` while the platform's ad module is loaded. Treat as a UI hint — `ShowRewarded` can still fail with `noFill`. Closes #27.
- **`IsSupported()` on Friends, Banners, Score** modules — gate Optional APIs UI consistently with the existing Auth/Player pattern. Underlying truth matches the Core SDK's per-platform support: Friends/Banners on CrazyGames only; Score on CrazyGames + Yandex (sticky) + YouTube. Closes #22.
- **`Analytics.LogLevelEnd` `durationSeconds` parameter** — optional `float` defaulting to `-1f` (omitted). Useful for racing / time-attack games. The jslib bridge omits the field when negative. Closes #28.
- **WebGL build-time guard** (`Yes2SDKBuildGuard.cs`) — fails WebGL builds early when the `Yes2SDK-SuperSDK` template is missing or not selected, so silent CI breakage stops shipping broken games. Closes #18 (already merged in 2.1.3 → main as part of feat/build-guard).

### Changed
- **Editor window** trimmed down. Removed the dead "Show Debug Logs" toggle (the runtime logger never read it), the redundant Build Configuration display rows, the workflow hint block, and the always-on Setup section that took space even after install. Footer now pulls the version from `Yes2SDK.Version` instead of a hardcoded string.

### Documentation
- README adds an `await`-friendly section showing Task-based usage with `CancellationToken` timeouts.
- New "Running alongside other SDKs" section covering init order, single-owner pause/resume, single-owner ads, namespace collisions, and init-timeout patterns. Partial fix for #33.
- Optional APIs section (Friends/Banners/Score) now uses the `IsSupported()` guard pattern.
- Ads section combines `IsAdShowing()` + `IsRewardedAdAvailable()` into a single button-state recipe.
- Analytics example shows the new `durationSeconds` parameter on `LogLevelEnd`.

### Notes
- Async data setters with success boolean (#30) need a Bridge callback round-trip in the jslib layer; ships in a follow-up.
- The `IsSupported()`, `IsRewardedAdAvailable()`, and `LogLevelEnd` duration additions depend on the Core SDK round-4 build (yes2sdk-core feat/round-4-feedback-fixes) being live in the dashboard's `sdk-dist/`.

## [2.1.3] - 2026-04-14

### Fixed
- **CrazyGames wrapper wrongly created on GameDistribution** — `Yes2SDKPlatformInit.jslib` now checks `window.__yes2sdkConfig.platform` before creating the CG wrapper. GD and CrazyGames share Azerion infrastructure, so `window.CrazyGames.SDK` is present on `revision.gamedistribution.com`. Previously, if `yes2sdk.umd.js` was slow or blocked for any reason, the postset would replace the GD adapter with a CG wrapper. Now the postset exits immediately when the dashboard config specifies any platform other than `crazygames`.

## [2.1.2] - 2026-04-13

### Fixed
- **WebGL template popup suppressed** — `createUnityInstance().catch()` in `Yes2SDK-SuperSDK/index.html` previously called `alert(message)` on any Unity runtime error. Now logs to `console.error` only — no popup ever reaches the player.

## [2.1.1] - 2026-04-13

### Fixed
- **CrazyGames SDK crash on non-CG domains** — init code no longer blindly loads CrazyGames SDK from CDN when `window.Yes2SDK` is missing. Now checks for actual CG signals (namespace, hostname, referrer) before attempting detection. On non-CG domains (dashboard inspector, localhost, GameDistribution, Poki, etc.) the SDK reports a clear error instead of crashing with `sdkDisabled`.
- **SDK errors no longer show browser popups** — added global error boundary that catches platform SDK errors before Unity's error handler can trigger `alert()` dialogs. All SDK errors now log to console only.
- **Partial dashboard injection detection** — when `window.__yes2sdkConfig` exists but `yes2sdk.umd.js` failed to load, reports a specific `SDKLoadFailed` error instead of falling into CrazyGames detection.
- **try-catch on all critical paths** — `doInit()`, `onSDKAvailable()`, `startGameAsync()`, and all ad entry points (interstitial, rewarded, banner) now catch synchronous throws from platform SDKs.

## [2.1.0] - 2026-04-12

### Added
- Branded Yes2Games loading screen — animated logo with breathing glow, shimmer progress bar, and smooth fade-out
- Logo embedded as base64 in WebGL template (zero network requests, ~26KB)
- Three-phase animation: entrance (fade + scale) → breathing loop (glow pulse) → completion (pulse + fade-out)

### Changed
- `Yes2SDK-SuperSDK` WebGL template background from `#000` to `#0a0a0a`
- Loading progress bar replaced with branded `Y2GLoader` controller

## [2.0.0] - 2026-03-19

### Breaking Changes
- **SuperSDK pipeline**: Platform-specific WebGL templates removed. Games now use a single bare template (`Yes2SDK-SuperSDK`) and the Yes2SDK Dashboard handles platform-specific SDK injection.
- **No inline `window.Yes2SDK`**: The template no longer defines `window.Yes2SDK`. The SuperSDK Core (`yes2sdk.umd.js`) is injected by the dashboard build pipeline.
- **Platform selection moved to dashboard**: Unity Editor no longer has a platform dropdown. Build once → upload to dashboard → select platforms there.

### Added
- `Yes2SDK-SuperSDK` WebGL template — bare template compatible with the SuperSDK dashboard pipeline
- SuperSDK Core TypeScript SDK (`@yes2sdk/core` v2.0.0-alpha.1) with 5 platform adapters:
  - Poki, CrazyGames, Yandex Games, Game Distribution, YouTube Playables
- 10 module APIs exposed via `window.Yes2SDK.*`:
  - `.ads`, `.session`, `.analytics`, `.player`, `.auth` (existing)
  - `.data`, `.game`, `.banners`, `.friends`, `.score` (new)
- Yes2SDK Dashboard — web app for build management, SDK injection, and QA testing
- QA Inspector — built-in tool to validate SDK integration before platform submission
- Debug platform adapter with postMessage bridge for Inspector communication

### Removed
- `Yes2SDK` (Debug) WebGL template — replaced by `Yes2SDK-SuperSDK`
- `Yes2SDK-Poki` WebGL template — platform bundling now handled by dashboard
- `Yes2SDK-CrazyGames` WebGL template — platform bundling now handled by dashboard
- `Yes2SDK-Yandex` WebGL template — platform bundling now handled by dashboard
- Platform enum (`TargetPlatform`) from BuildConfig — single config now
- Platform dropdown from Editor window

### Changed
- Editor window simplified: single "Build WebGL" button with workflow hint (Build → Zip → Upload to Dashboard)
- `BuildConfig` reduced to a single `Default` configuration (no compression, medium stripping)
- `Yes2SDKInstaller` installs only one template instead of four
- Package version bumped to 2.0.0-alpha.1
- Package description updated to reflect SuperSDK pipeline

### Retained
- All C# Runtime modules (Ads, Analytics, Session, Player, Data, Auth, Game, Banners, Friends, Score)
- All jslib bridges (12 files) — these call `window.Yes2SDK.*` which is now provided by the injected SuperSDK
- `Yes2SDKPlatformInit.jslib` postset bootstrap — still needed for CrazyGames HTML replacement
- Build optimization tools: Sprite Atlas, KTX2 compression, Texture Swap, Screenshot capture

## [1.1.1] - 2026-03-03

### Added
- Poki screenshot capture tool — capture Game View screenshots resized to Poki store format (800x480 + 100x56 JPG) directly from Play Mode
- Customizable keyboard shortcut for screenshot capture (default: F12), rebindable from the Tools tab
- Per-slot preview and delete buttons for screenshot management
- Smart screenshot index that fills empty slots first before overwriting
- `Yes2SDK/Capture Screenshot` menu item as fallback trigger

### Changed
- Extracted shared jslib helpers (`$__y2` utilities) to reduce duplication across 11 jslib bridge files (~310 lines removed)
- Replaced Bridge.cs callback boilerplate with delegate routing (~220 lines removed)
- Consolidated 7 stub modules into shared `Yes2SDKStubModule` base class (~250 lines removed)

### Fixed
- `Yes2SDK.Version` property now correctly returns `1.1.1` (was stuck at `1.0.0`)
- Prevented duplicate Poki SDK `gameLoadingStart`/`gameLoadingFinished` lifecycle calls

## [1.1.0] - 2026-03-02

### Added
- Sprite Atlas automation tool — scan for loose sprites and create WebGL-optimized atlases to reduce draw calls
- KTX2 texture compression tool — convert textures to Basis Universal for GPU-optimal runtime transcoding
- KTX2 runtime loader component (`Yes2SDKKtx2Image`) with optional `com.unity.cloud.ktx` integration
- Texture swap tool — replace texture references in scenes/prefabs with KTX2 runtime loaders to reduce build size
- `YES2SDK_KTX` scripting define when `com.unity.cloud.ktx` is installed
- IL2CPP link preservation for KTX assembly

### Changed
- Editor window refactored from single scrollable layout to three tabs: **Build**, **Optimization**, **Tools**
- Foldout sections replaced with always-visible content per tab
- Selected tab persists across window reopens via EditorPrefs

## [1.0.0] - 2026-02-07

### Added
- Unified C# API wrapping WebGL platform SDKs (Poki, CrazyGames) behind a single interface
- Full module support: Ads, Analytics, Session, Player, Data, Auth, Game, Banners, Friends, Score
- WebGL templates for Poki, CrazyGames, and Debug (Editor/local testing)
- Editor window (Yes2SDK > Settings) for template installation, platform selection, and build configuration
- CrazyGames SDK async detection with polling and CDN fallback
- Mock implementations for Editor testing
- jslib bridges for all active modules
- `Yes2SDKPlatformInit.jslib` postset bootstrap for CrazyGames HTML replacement
