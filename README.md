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
3. Enter: `https://github.com/yes2games/yes2sdk-unity.git#v2.1.3`
4. Click **Add**

> Pinning the URL with `#v2.1.3` keeps the package hash stable across resolves. Bump the tag (e.g. `#v2.2.0`) to upgrade. Without a tag, Package Manager re-resolves against `main` on every refresh and reports phantom diffs.

### Via Local Folder

1. Clone or download the repository
2. Open **Window > Package Manager**
3. Click **+** > **Add package from disk...**
4. Navigate to the `Unity/` folder and select `package.json`

### Initial Setup

1. Open **Yes2SDK > Build Window** in the Unity menu bar
2. Click **Install Template** — this installs the `Yes2SDK-SuperSDK` WebGL template into your project
3. The status indicator changes from "Setup Pending" to "Ready"

> After updating the SDK package, click **Reinstall Template** in the Settings section to copy changes into your project.

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

These modules add extra player-facing features. They are **not guaranteed** to be available at runtime — guard with `IsSupported()` where the module exposes it (currently `Auth` and `Player`), and always handle `FeatureNotSupported` errors gracefully. Don't make your core gameplay depend on them.

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

`Friends` does not yet expose `IsSupported()` — handle the `FeatureNotSupported` error instead, and hide friends UI on platforms that reject the call.

```csharp
Yes2SDK.Friends.ListFriendsAsync(
    page: 0, size: 10,
    onSuccess: page => {
        foreach (var friend in page.Friends)
            Debug.Log($"{friend.Username} ({friend.Id})");
    },
    onError: err => {
        if (err.ErrorCode == ErrorCode.FeatureNotSupported)
            HideFriendsUI();          // platform doesn't support friends
        else
            Debug.LogError(err);
    }
);
```

### Banners

Container-based display ads. Different from `Ads.ShowBanner`.

```csharp
Yes2SDK.Banners.ShowBanner("sidebar-left", BannerSize.Medium_300x250);
Yes2SDK.Banners.HideBanner("sidebar-left");
Yes2SDK.Banners.HideAllBanners();
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
Yes2SDK.Score.AddScore(150f);
Yes2SDK.Score.SubmitScore("encrypted-score-string");
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
2. Click **Apply Settings** — sets the WebGL template and build configuration
3. Click **Build WebGL** or **Build and Run**
4. Zip the build output folder
5. Upload the zip to the **Yes2Games Dashboard**
6. Run through the QA Inspector; when everything is green, **Request Review**

### Build Configuration

| Setting | Value |
|---------|-------|
| Template | Yes2SDK-SuperSDK |
| Compression | Disabled |
| Code Stripping | Medium |
| Exception Support | None |

#### Where to set these — depends on your Unity version

- **Unity 2021–2022**: *Edit > Project Settings > Player > WebGL* (project-wide).
- **Unity 6+**: *File > Build Profiles* — select your WebGL profile, then click **Player Settings** at the bottom of the profile panel. Settings apply only to that profile, so make sure the profile you build is the one with these values.

> Unity 6 moved WebGL settings from project-wide Player Settings into per-profile Build Profiles. If you set values in the old location on Unity 6+, the build will pick up the *profile's* defaults instead and your settings are silently ignored.

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

## Architecture

```
C# Runtime  ->  .jslib bridge  ->  window.Yes2SDK.*
                                          ^
                          injected by the Yes2Games Dashboard
```

- **C# Runtime** (`Runtime/`) — static facade + module classes. Uses `[DllImport("__Internal")]` for WebGL, mock fallbacks via `#if UNITY_WEBGL && !UNITY_EDITOR`.
- **jslib Bridges** (`Plugins/`) — JavaScript files that call `window.Yes2SDK.*` and return results via `SendMessage('Bridge', ...)`.
- **WebGL Template** (`Assets/WebGLTemplates/Yes2SDK-SuperSDK/`) — bare HTML template. Does not define `window.Yes2SDK` — the dashboard injects the SuperSDK Core (`yes2sdk.umd.js`) at upload time.

---

## License

Proprietary. See LICENSE for details.
