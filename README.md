# Yes2SDK for Unity

[![Version](https://img.shields.io/github/v/tag/yes2games/yes2sdk-unity?label=version)](https://github.com/yes2games/yes2sdk-unity/releases)
[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-blue)](https://unity.com/)

A single SDK for your Unity WebGL game. Integrate once against Yes2SDK, submit through the Yes2Games Dashboard, and the Yes2Games team handles the rest.

The current SDK version is also exposed at runtime via `Yes2SDK.Version` (string), so you can log it for support tickets.

## Requirements

- Unity 2021.3 or newer (Unity 6 supported — see [Building](#building))
- WebGL build target
- `com.unity.nuget.newtonsoft-json` (>= 3.2.1)

## Installation

### Via Git URL (recommended)

1. Open **Window > Package Manager**
2. Click **+** > **Add package from git URL...**
3. Enter: `https://github.com/yes2games/yes2sdk-unity.git#v2.5.0`
4. Click **Add**

> Pinning the URL with `#v2.5.0` keeps the package hash stable across resolves. Bump the tag when a newer release ships. Without a tag, Package Manager re-resolves against `main` on every refresh and reports phantom diffs.

### Via Local Folder

1. Clone or download the repository
2. Open **Window > Package Manager**
3. Click **+** > **Add package from disk...**
4. Navigate to the `Unity/` folder and select `package.json`

### Initial Setup

1. Open **Yes2SDK > Build Window** in the Unity menu bar
2. Click **Install Template** — this installs the `Yes2SDK-SuperSDK` WebGL template into your project
3. The status indicator changes from "Setup Pending" to "Ready"

> After updating the SDK package, click **Reinstall Template** in the Build Window to copy changes into your project.

---

## Quick Start

This is the **minimum integration** your game must have. The lifecycle has three distinct stages — don't chain them together:

```text
App launch         → InitializeAsync   (SDK ready)
Splash + loading   → SetLoadingProgress(0..100) as assets load
Game playable      → StartGameAsync    (scene ready, accepting input)
```

```csharp
using System.Collections;
using UnityEngine;
using Yes2SDK;

public class GameManager : MonoBehaviour
{
    void Start()
    {
        // Stage 1 — at app launch, initialize the SDK.
        Yes2SDK.InitializeAsync(
            onSuccess: () =>
            {
                Debug.Log("SDK ready");
                StartCoroutine(LoadAndStart());
            },
            onError: err => Debug.LogError(err)
        );
    }

    IEnumerator LoadAndStart()
    {
        // Stage 2 — load your assets and report progress (0..100).
        for (int p = 0; p <= 100; p += 10)
        {
            Yes2SDK.SetLoadingProgress(p);
            yield return new WaitForSeconds(0.05f); // replace with real loading work
        }

        // Stage 3 — splash done, scene loaded, game is playable.
        Yes2SDK.StartGameAsync(
            onSuccess: () => Debug.Log("Game started"),
            onError: err => Debug.LogError(err)
        );
    }
}
```

> **Don't call `StartGameAsync` directly inside the `InitializeAsync` success handler.** The platform's loading bar treats `StartGameAsync` as "the game is playable now" — calling it before the player has anything to interact with shows a misleading 100% loading state.

Without this flow your game won't be accepted for review.

---

## Core API

Implement everything in this section. Together these cover what Yes2Games needs to validate and monetize your game.

### Lifecycle (required)

```csharp
// Call once at startup
Yes2SDK.InitializeAsync(onSuccess, onError);

// Call as your game loads (0-100)
Yes2SDK.SetLoadingProgress(progress);

// Call when loading finishes and the game is playable
Yes2SDK.StartGameAsync(onSuccess, onError);
```

> **Important:** these three calls fire in three distinct stages — *don't chain them*. `InitializeAsync` is at app launch. `SetLoadingProgress` is updated as your assets load. `StartGameAsync` runs **only when the game is actually playable** (splash gone, scene loaded, accepting input). See [Quick Start](#quick-start) for the full pattern.

#### `await`-friendly overloads

Every `*Async` method has a `Task`-returning overload that takes a `CancellationToken`. Pass `CancellationToken.None` for no timeout, or a real token for cancellation/timeout support. Errors are thrown as `Yes2SDKException` (whose `ErrorCode` matches the `Error.ErrorCode` of the underlying failure):

```csharp
using System.Threading;
using System.Threading.Tasks;
using Yes2SDK;

// Cancel init if it hangs for more than 10 seconds
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
try
{
    await Yes2SDK.InitializeAsync(cts.Token);
    Debug.Log("SDK ready");
}
catch (Yes2SDKException ex) when (ex.ErrorCode == ErrorCode.NetworkError)
{
    // Platform unreachable — fall back or retry with backoff
}
catch (TaskCanceledException)
{
    // Init didn't complete inside the timeout
}
```

The callback-based form is unchanged and still preferred for short-lived game-loop call sites where allocating Tasks is overkill.

Handle pause / resume so your game reacts when the SDK pauses you (e.g. during an ad):

```csharp
Yes2SDK.OnPause  += () => { Time.timeScale = 0; AudioListener.pause = true;  };
Yes2SDK.OnResume += () => { Time.timeScale = 1; AudioListener.pause = false; };
```

### Ads (required)

Interstitial ads run at natural break points. Rewarded ads run only when the player opts in.

> **Always wrap ad calls in `Game.GameplayStop()` / `Game.GameplayStart()`.** Platforms count "active gameplay seconds" for monetization — leaving gameplay running during an ad inflates those numbers and is grounds for rejection. See [Gameplay Tracking](#gameplay-tracking-required) for the full rule.

```csharp
Yes2SDK.Game.GameplayStop();              // before the ad
Yes2SDK.Ads.ShowInterstitial(
    placement: "level-end",
    description: "Between levels",
    beforeAd: () => PauseGame(),
    afterAd:  () => { ResumeGame(); Yes2SDK.Game.GameplayStart(); },
    onError:  err => { ResumeGame(); Yes2SDK.Game.GameplayStart(); }
);

Yes2SDK.Game.GameplayStop();              // before the ad
Yes2SDK.Ads.ShowRewarded(
    placement: "extra-life",
    description: "Extra life reward",
    beforeAd:    () => PauseGame(),
    afterAd:     () => { ResumeGame(); Yes2SDK.Game.GameplayStart(); },
    adDismissed: () => { /* no reward — see firing order below */ },
    adViewed:    () => GiveReward(),
    onError:     err => { ResumeGame(); Yes2SDK.Game.GameplayStart(); }
);
```

#### Rewarded ad firing order

The callbacks fire in this order. Pay attention — getting it wrong silently breaks reward logic:

```text
beforeAd      → pause game (always)
(ad shown)
afterAd       → resume game (always — fires whether the player watched or dismissed)
adViewed      → grant reward (ONLY fires if the player watched the full ad)
   — or —
adDismissed   → no reward (fires if the player skipped/closed early)
```

> ⚠️ **Do NOT grant rewards in `afterAd`.** `afterAd` fires for both completion *and* dismissal — granting rewards there gives them away on skip. Always grant in `adViewed`.

#### Concurrent ad guard + readiness

- `Ads.IsAdShowing()` — returns `true` while a `ShowInterstitial` or `ShowRewarded` is in flight (between the call and `afterAd`/error). Calling `Show*` again while one is already showing is rejected immediately — `onError` fires with `ErrorCode.InvalidParams` and a message starting `"Another ad is already in flight (AdAlreadyShowing)…"`.
- `Ads.IsRewardedAdAvailable()` — best-effort check whether a rewarded ad appears available right now. Most platform SDKs don't expose explicit readiness, so this returns `true` as long as the platform's ad module is loaded; the actual `ShowRewarded` call can still fail with `noFill`. Use it as a hint, not a guarantee.

```csharp
rewardButton.interactable =
    !Yes2SDK.Ads.IsAdShowing() && Yes2SDK.Ads.IsRewardedAdAvailable();
```

### Gameplay Tracking (required)

Tells Yes2Games when an active round begins and ends. Also call `GameplayStop()` before any ad and `GameplayStart()` after.

```csharp
Yes2SDK.Game.GameplayStart();
Yes2SDK.Game.GameplayStop();
```

> `Analytics.LogLevelStart` / `LogLevelEnd` can also trigger gameplay start / stop — use either pair, but don't call both.

### Data (required)

Key-value storage. Persists across sessions automatically.

```csharp
Yes2SDK.Data.SetInt("highScore", 1500);
Yes2SDK.Data.SetString("playerName", "Hero");

int score = Yes2SDK.Data.GetInt("highScore", defaultValue: 0);

bool exists = Yes2SDK.Data.HasKey("highScore");
Yes2SDK.Data.DeleteKey("highScore");
```

### Analytics (recommended)

```csharp
Yes2SDK.Analytics.LogEvent("custom-event", new Dictionary<string, object> {
    { "key", "value" }
});

Yes2SDK.Analytics.LogLevelStart("level-1");
Yes2SDK.Analytics.LogLevelEnd("level-1", score: 1500, success: true);
// Time-based games (racing, time-attack) can include duration:
Yes2SDK.Analytics.LogLevelEnd("level-1", score: 1500, success: true, durationSeconds: 87.3f);
Yes2SDK.Analytics.LogScore(1500);
```

### Session (recommended)

```csharp
string locale = Yes2SDK.Session.GetLocale();  // e.g. "en", "fr"
string device = Yes2SDK.Session.GetDevice();   // "desktop" or "mobile"
```

> Treat session info as a hint, not a guarantee. Don't branch your core game logic on it.

---

## Optional APIs

These modules add extra player-facing features. They are **not guaranteed** to be available at runtime — guard with `IsSupported()` (available on `Auth`, `Friends`, `Banners`, `Score`, and `Player`), and always handle `FeatureNotSupported` errors gracefully. Don't make your core gameplay depend on them.

### Auth

```csharp
if (Yes2SDK.Auth.IsSupported())
{
    Yes2SDK.Auth.GetCurrentUserAsync(
        onSuccess: user => Debug.Log($"User: {user.Name}, authenticated: {user.IsAuthenticated}"),
        onError:   err  => Debug.LogError(err)
    );

    Yes2SDK.Auth.SignInAsync(
        onSuccess: user => Debug.Log($"Signed in as {user.Name}"),
        onError:   err  => Debug.LogError(err)
    );
}
```

### Friends

```csharp
if (Yes2SDK.Friends.IsSupported())
{
    Yes2SDK.Friends.ListFriendsAsync(
        page: 0, size: 10,
        onSuccess: page => {
            foreach (var friend in page.Friends)
                Debug.Log($"{friend.Username} ({friend.Id})");
        },
        onError: err => Debug.LogError(err)
    );
}
```

### Banners

Container-based display ads. Different from `Ads.ShowBanner`.

```csharp
if (Yes2SDK.Banners.IsSupported())
{
    Yes2SDK.Banners.ShowBanner("sidebar-left", BannerSize.Medium_300x250);
    Yes2SDK.Banners.HideBanner("sidebar-left");
    Yes2SDK.Banners.HideAllBanners();
}
```

### Game Extras

`HappyTime()` signals to the platform that the player just hit a positive moment — level cleared, achievement unlocked, boss defeated. Some platforms use this signal to time monetization prompts and rate requests so they don't interrupt frustrating moments. Call it sparingly, only on genuine highs.

```csharp
Yes2SDK.Game.HappyTime();

Yes2SDK.Game.InviteLinkAsync(
    new Dictionary<string, string> { { "roomId", "abc123" } },
    onSuccess: link => Debug.Log($"Invite: {link}")
);

Yes2SDK.Game.ShowInviteButton(new Dictionary<string, string> { { "roomId", "abc123" } });
Yes2SDK.Game.HideInviteButton();

GameSettings settings = Yes2SDK.Game.GetSettings();
Yes2SDK.Game.OnSettingsChanged += s => ApplySettings(s);

Yes2SDK.Game.CopyToClipboard("https://...");
```

### Score

```csharp
if (Yes2SDK.Score.IsSupported())
{
    Yes2SDK.Score.AddScore(150f);
    Yes2SDK.Score.SubmitScore("encrypted-score-string");
}
```

### Player Data

```csharp
if (Yes2SDK.Player.IsDataSupported())
{
    Yes2SDK.Player.SetDataAsync("{\"level\":5}", onSuccess: () => {});
    Yes2SDK.Player.GetDataAsync(new[] { "level" }, onSuccess: json => {});
    Yes2SDK.Player.FlushDataAsync();
}

if (Yes2SDK.Player.IsConnectedPlayersSupported())
{
    Yes2SDK.Player.GetConnectedPlayersAsync(onSuccess: json => {});
}
```

---

## Integration Checklist

Your build is ready for review when:

- [ ] `InitializeAsync` is called at startup
- [ ] `SetLoadingProgress` is called as assets load
- [ ] `StartGameAsync` is called when the game is playable
- [ ] `OnPause` / `OnResume` are handled (mute audio, pause gameplay)
- [ ] Interstitial ads run at natural break points
- [ ] Rewarded ads grant reward **only** in `adViewed`
- [ ] `Game.GameplayStop()` is called before every ad; `Game.GameplayStart()` after
- [ ] Gameplay resumes in `afterAd` AND `onError`
- [ ] `Data` is used for persistent player data

The QA Inspector in the Yes2Games Dashboard validates all of this automatically.

---

## Building

1. Open **Yes2SDK > Build Window**
2. Click **Build WebGL** (or **Build and Run** to launch in browser).

The output folder is what you upload to the **Yes2Games Dashboard** — the dashboard handles SDK injection, platform bundling, and walks you through the QA Inspector and review request.

### Build Configuration

| Setting | Recommended | Notes |
|---|---|---|
| Template | `Yes2SDK-SuperSDK` | Required. The build-time guard fails the build with any other template. |
| Compression | Disabled | Required for dashboard upload — the CDN doesn't currently send `Content-Encoding` headers. |
| Code Stripping | Medium | Balances build size and AOT safety. |
| Exception Support | Explicitly Thrown Exceptions Only | See note below. |
| Memory Size | 256 MB+ | Most games need at least 256–512 MB. Smaller heaps trigger a generic "unspecified error" at boot. |

> ⚠️ **Why not `Exception Support: None`?**
>
> `None` produces the smallest build, but Unity WebGL's `None` mode strips exception infrastructure entirely — even an exception caught by a `try/catch` aborts the wasm. This breaks any code path where a dependency uses `try/catch` as control flow (Newtonsoft.Json — which the SDK itself imports — third-party Unity asset packages, save systems, etc.).
>
> Most games should ship with **Explicitly Thrown Exceptions Only**: about 10% larger build but compatible with the .NET ecosystem. Use `None` only after auditing your full dependency graph.

#### Where to set these

- **Yes2SDK Build Window** (recommended) — *Yes2SDK > Build Window > WebGL Settings* (collapsible panel). Edits write to Player Settings live, no Apply step. Click **Reset to recommended** to apply all the values from the table above at once.
- **Unity 2021–2022**: *Edit > Project Settings > Player > WebGL* (project-wide).
- **Unity 6+**: *File > Build Profiles* — select your WebGL profile, then click **Player Settings** at the bottom of the profile panel. Settings apply only to that profile.

> Unity 6 moved WebGL settings from project-wide Player Settings into per-profile Build Profiles. The Yes2SDK Build Window's Settings panel reads and writes the **project-wide** Player Settings. If you have an active Build Profile with overrides for these settings, the profile takes precedence at build time — edit those overrides via *File > Build Profiles*. The panel will surface a notice when an active profile is detected so you don't edit values that are then ignored.

#### Build Mode for diagnostics

The Build Window has a **Build Mode** dropdown for one-off overrides without changing your Player Settings:

- **Production** — use Player Settings as-is. Default for shipping builds.
- **Production Safe** — temporarily forces Exception Support to `Explicitly Thrown` for one build. Useful when your Player Settings is `None` but you need a build that catches third-party throws.
- **Diagnostic** — temporarily forces `Full With Stacktrace`. Use to capture real C# class/method names in browser console errors when chasing a crash.

The override saves and restores Player Settings around each build, so it never leaves your project in an unexpected state.

---

## Editor Testing

In the Unity Editor, SDK calls run against mock implementations:

- `InitializeAsync` / `StartGameAsync` succeed immediately
- Ads simulate the full callback flow (`beforeAd` → `afterAd` → `adViewed`)
- `Data` uses `PlayerPrefs`
- Optional APIs return `FeatureNotSupported`

For richer simulation — forced errors, specific locales, ad failure modes — use the **QA Inspector** in the Yes2Games Dashboard.

---

## Error Handling

All async methods accept an `onError` callback with an `Error` struct:

```csharp
public struct Error
{
    public string Code;
    public string Message;
    public string Context;
    public ErrorCode ErrorCode;
}

public enum ErrorCode
{
    NotInitialized, InvalidParams, FeatureNotSupported, PlatformError,
    NetworkError, RateLimited, UserCancelled, Unknown
}
```

Use `ErrorCode` for control flow:

```csharp
onError: err => {
    if (err.ErrorCode == ErrorCode.FeatureNotSupported)
        // gracefully hide the feature
    else
        Debug.LogError(err);
}
```

### Error code reference

| Code | When it fires | Recommended handling |
|------|---------------|----------------------|
| `NotInitialized` | An API was called before `InitializeAsync` succeeded. | Wait for init to complete first; never call SDK methods from `Awake()` without checking `Yes2SDK.IsInitialized`. |
| `InvalidParams` | A required parameter was null/empty or out of range. | Treat as a programmer error — fix the call site. |
| `FeatureNotSupported` | The optional API isn't available on this platform (e.g. Friends on Poki). | Hide the related UI; fall back to a non-platform alternative. Always pre-check with `IsSupported()` for optional APIs. |
| `PlatformError` | The underlying platform SDK rejected the call. | Log `err.Message` and `err.Context` for support; treat the call as failed. |
| `NetworkError` | A platform call failed network-side (timeout, offline, server error). | Retry with backoff. Don't retry indefinitely. |
| `RateLimited` | Too many calls in a short window (e.g. ad spam protection). | Back off and try again later — don't retry immediately. |
| `UserCancelled` | The player closed/dismissed a flow (e.g. login dialog, rewarded ad). | Not an error in the usual sense — silently respect the player's choice, no toast. |
| `Unknown` | The error didn't match any of the above. | Log everything (`err.Code`, `err.Message`, `err.Context`) and treat as a hard failure. |

---

## Running alongside other SDKs

Real games often ship with multiple platform SDKs in the same build (Yes2SDK + Poki + Yandex + Playgama, etc.). A few ground rules to keep them from stepping on each other:

- **Init order.** Initialize Yes2SDK first. Yes2SDK figures out which actual platform is hosting the game and routes through it — initializing your own platform SDK directly first can race with Yes2SDK's detection.
- **One owner for pause / resume.** Pick one SDK to drive `Time.timeScale` and `AudioListener.pause`. If both Yes2SDK and another SDK call resume/pause, you'll get oscillation. Recommended: subscribe to `Yes2SDK.OnPause` / `OnResume` and ignore the other SDK's equivalent.
- **One owner for ads.** Don't call ads via two SDKs in the same session — the platform almost always rejects the second call. Pick the SDK that targets the platform you're actually hosted on.
- **Namespace collisions.** If you have your own `Platform` type, qualify the Yes2SDK enum (`Yes2SDK.Platform`) at the call site or use a `using` alias (`using Y2 = Yes2SDK;`). C# resolves namespace-vs-type ambiguity by full qualification.
- **Init timeout.** If you depend on Yes2SDK init completing before your other SDK's flow, wrap `InitializeAsync` in a `CancellationToken` with a timeout (see [`await`-friendly overloads](#await-friendly-overloads)) so your game doesn't hang on a wedged JS bridge.

---

## License

MIT — see [LICENSE](LICENSE).
