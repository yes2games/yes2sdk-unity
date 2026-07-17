# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.6.0] - 2026-07-17

### Added
- **Mock ad popup in Play Mode.** `ShowInterstitial` and `ShowRewarded` now display a mock ad overlay in the Unity Editor with a countdown (3s interstitial, 5s rewarded) followed by Close / Claim Reward / Skip buttons, so pause-resume wiring and both reward outcomes can be tested by clicking instead of relying on instant callbacks. Toggle it under Play Mode Testing in the Build Window (on by default). Batch mode always uses the instant legacy flow so CI test runs never block, and disabling the toggle restores the previous behavior including the `"dismiss"` description convention.
- **Mock in-app purchases in Play Mode.** With the Play Mode Testing toggle on (default), `IAP.IsSupported()` returns true in the Editor, `GetCatalogAsync` returns a sample catalog, and `PurchaseAsync` opens a Buy / Cancel confirmation dialog that resolves with a realistic purchase payload (Buy) or a `UserCancelled` error (Cancel). Any product id is accepted, purchases persist for the current play session, and `GetPurchasesAsync` / `ConsumePurchaseAsync` operate on the session's purchase list.
- **`IsRewardedAdAvailable()` returns true in Play Mode** while the mock ad popup is enabled, so "watch ad" buttons gated on availability can be tested in the Editor.

## [2.5.2] - 2026-07-09

### Fixed
- **Package no longer emits "has no meta file … the asset will be ignored" on install.** A repo-only tooling folder shipped without Unity `.meta` files, which Unity flags when the package is installed as an immutable dependency. The folder is now hidden from Unity (Unity ignores `~`-suffixed folders) so it is excluded from import entirely.
- **`Yes2SDK.Version` now reports the real package version** (was stuck at `2.5.0`), so support-ticket logs and the Build Window header show the correct number again.

## [2.5.1] - 2026-07-08

### Added
- **Account-selection dialog events (Yandex).** `Yes2SDK.OnAccountDialogOpen` and `Yes2SDK.OnAccountDialogClose` fire when the platform's account-selection dialog opens and closes; pause gameplay and audio while it is open.

## [2.5.0] - 2026-06-24

### Added
- **Leaderboards.** `Yes2SDK.Leaderboard` is now functional (previously a stub). `GetLeaderboardAsync`, `SetScoreAsync`, `GetEntriesAsync`, and `GetPlayerEntryAsync` are wired through the SDK bridge; results are delivered to the success callback as JSON. Supported on Yandex; `IsSupported()` reports availability for the active platform.
- **Player stats.** `Yes2SDK.Stats` is now functional (previously a stub). `GetStatsAsync`, `SetStatsAsync`, and `IncrementStatsAsync` manage server-side numeric counters. Supported on Yandex.
- **Remote config: `Yes2SDK.Config`.** `GetFlagsAsync` fetches platform feature flags as a string→string map, with optional defaults; on platforms without remote config it returns the defaults you pass so the same code runs everywhere.
- **Rating prompt: `Yes2SDK.Review`.** `CanReviewAsync` checks eligibility and `RequestReviewAsync` shows the platform's in-game rating prompt (Yandex). `RequestReviewAsync` checks eligibility internally first, so it is safe to call directly; it no-ops gracefully where unsupported.
- **Player identity.** `Yes2SDK.Player` adds `GetUniqueIdAsync`, `GetIDsPerGameAsync`, `GetPayingStatusAsync`, `GetModeAsync`, and `GetPhotoAsync(size)`.
- **Banner status: `Yes2SDK.Banners.GetBannerStatusAsync()`** reports whether a sticky banner is currently showing.
- **Server time: `Yes2SDK.Game.GetServerTimeAsync()`** returns tamper-proof server time where available, falling back to local device time otherwise.
- **Device info: `Yes2SDK.Session.GetDeviceInfo()`** returns the device type and form-factor flags (mobile / desktop / tablet / TV) synchronously.

### Changed
- **`Leaderboard.SetScoreAsync` success callback now returns the resulting entry as JSON** (rank and score after submission), previously parameterless.

## [2.4.5] - 2026-06-19

### Added
- **In-app purchases.** `Yes2SDK.IAP` is now functional (previously a stub). `GetCatalogAsync`, `PurchaseAsync`, `GetPurchasesAsync`, and `ConsumePurchaseAsync` are wired through the SDK bridge to the platform payments API; results are delivered to the success callback as JSON. Call `GetPurchasesAsync` on launch so a returning player keeps the items they own. Supported on Yandex; `IsSupported()` reports availability for the active platform.
- **Durable saves: `Yes2SDK.Data.FlushAsync()` and `Yes2SDK.Data.SetStringAsync()`.** The synchronous `Set*` calls are batched on cloud-backed platforms (Yandex debounces cloud writes), so a save made right before the game closes could be lost. `FlushAsync` forces all pending writes to the backing store and reports confirmation; `SetStringAsync` writes a single key and awaits it. Both provide callback and `Task` (CancellationToken) overloads — call `FlushAsync` at checkpoints to guarantee progress is persisted.

## [2.4.3] - 2026-06-11

### Added
- **"Use Yes2SDK build pipeline" toggle in the Build Window.** Yes2SDK's build guard runs on every WebGL build in the project, so a build driven by another platform's pipeline — using its own WebGL template — was failed by the template check (`expected 'PROJECT:Yes2SDK-SuperSDK'`). The new toggle (default **on**, so existing projects are unchanged) lets you turn off Yes2SDK build management when building for a non-Yes2SDK platform: the template guard and build-mode override are skipped and the build proceeds with that platform's template. The SDK stays installed; turn the toggle back on for Yes2Games builds.

### Fixed
- **`Yes2SDK.Version` now reports the real package version.** The runtime constant was stuck at `2.4.0` and had drifted behind `package.json`, so the Build Window header and any game code reading `Yes2SDK.Version` showed the wrong number. It now tracks the package version.

## [2.4.2] - 2026-06-10

### Fixed
- **`Player.IsDataSupported()` and `Player.IsConnectedPlayersSupported()` now report the real platform capability.** Both flags were hardcoded in C# to a CrazyGames-only check, which had drifted out of sync with the JS SDK. `IsDataSupported()` returned `false` on every platform except CrazyGames, so games that gate save/load on it skipped persistence on Yandex, YouTube, GameDistribution, and Poki — even though the SDK persists data on all of them (platform cloud save where available, local web storage otherwise). `IsConnectedPlayersSupported()` inversely returned `true` on CrazyGames, where the feature is not actually available. Both methods now delegate to the JS SDK so capability tracks the active platform adapter instead of being duplicated and stale.

### Changed
- **Player data save/load now works in the editor.** The editor/standalone mock for `Player.GetDataAsync` / `SetDataAsync` / `FlushDataAsync` previously returned `FeatureNotSupported`, which did not match the persistence the SDK provides in WebGL builds. The mock is now backed by `PlayerPrefs` (single merged JSON blob, same shape as the runtime store) and `IsDataSupported()` returns `true` in the editor, so save/load can be exercised in Play Mode without a WebGL build.

## [2.4.1] - 2026-05-29

### Fixed
- **`OnAudioEnabledChange` now reports the correct state under IL2CPP Code Stripping = High.** The `{ "enabled": ... }` payload from the JS bridge was being deserialized via Newtonsoft.Json, whose reflection-based deserializer can throw internally under aggressive stripping even with `link.xml` preservation. The exception was swallowed and the value defaulted to `true`, so mute toggles always surfaced as unmuted to the game. Replaced with a small manual parse so the value is preserved regardless of strip level. Required for YouTube cert (#14) on builds that ship with Code Stripping at High.

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
- The `IsSupported()`, `IsRewardedAdAvailable()`, and `LogLevelEnd` duration additions depend on a matching Yes2SDK Core update.

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
- **Single template**: Platform-specific WebGL templates removed. Games now use a single `Yes2SDK-SuperSDK` template; per-platform builds are produced via the Yes2Games Dashboard at upload time.
- **No inline `window.Yes2SDK`**: The template no longer defines `window.Yes2SDK` — it's provided at runtime by the dashboard upload flow.
- **Platform selection moved to dashboard**: Unity Editor no longer has a platform dropdown. Build once → upload → select platforms there.

### Added
- `Yes2SDK-SuperSDK` WebGL template
- 10 module APIs exposed via `window.Yes2SDK.*`:
  - `.ads`, `.session`, `.analytics`, `.player`, `.auth` (existing)
  - `.data`, `.game`, `.banners`, `.friends`, `.score` (new)
- Platform support: Poki, CrazyGames, Yandex Games, Game Distribution, YouTube Playables
- QA Inspector — validates SDK integration before platform submission

### Removed
- `Yes2SDK` (Debug) WebGL template
- `Yes2SDK-Poki` WebGL template
- `Yes2SDK-CrazyGames` WebGL template
- `Yes2SDK-Yandex` WebGL template
- Platform enum (`TargetPlatform`) from BuildConfig — single config now
- Platform dropdown from Editor window

### Changed
- Editor window simplified: single "Build WebGL" button with workflow hint (Build → Zip → Upload to Dashboard)
- `BuildConfig` reduced to a single `Default` configuration (no compression, medium stripping)
- `Yes2SDKInstaller` installs only one template instead of four
- Package version bumped to 2.0.0-alpha.1

### Retained
- All C# Runtime modules (Ads, Analytics, Session, Player, Data, Auth, Game, Banners, Friends, Score)
- All jslib bridges (12 files)
- `Yes2SDKPlatformInit.jslib` postset bootstrap (CrazyGames HTML init)
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
