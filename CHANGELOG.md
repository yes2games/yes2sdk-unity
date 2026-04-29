# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
