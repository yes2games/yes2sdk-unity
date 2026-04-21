# Yes2SDK for Unity

A single SDK for your Unity WebGL game. Integrate once against Yes2SDK, submit through the Yes2Games Dashboard, and the Yes2Games team handles the rest.

## Requirements

- Unity 2021.3 or newer
- WebGL build target
- `com.unity.nuget.newtonsoft-json` (>= 3.2.1)

## Installation

### Via Git URL (recommended)

1. Open **Window > Package Manager**
2. Click **+** > **Add package from git URL...**
3. Enter the repository URL for the Unity package
4. Click **Add**

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

This is the **minimum integration** your game must have.

```csharp
using Yes2SDK;

public class GameManager : MonoBehaviour
{
    void Start()
    {
        // 1. Initialize the SDK
        Yes2SDK.Yes2SDK.InitializeAsync(
            onSuccess: () =>
            {
                Debug.Log("SDK ready");

                // 2. Report loading progress as your assets load
                Yes2SDK.Yes2SDK.SetLoadingProgress(100);

                // 3. Tell the SDK the game is playable
                Yes2SDK.Yes2SDK.StartGameAsync(
                    onSuccess: () => Debug.Log("Game started"),
                    onError: err => Debug.LogError(err)
                );
            },
            onError: err => Debug.LogError(err)
        );
    }
}
```

Without this flow your game won't be accepted for review.

---

## Core API

Implement everything in this section. Together these cover what Yes2Games needs to validate and monetize your game.

### Lifecycle (required)

```csharp
// Call once at startup
Yes2SDK.Yes2SDK.InitializeAsync(onSuccess, onError);

// Call as your game loads (0-100)
Yes2SDK.Yes2SDK.SetLoadingProgress(progress);

// Call when loading finishes and the game is playable
Yes2SDK.Yes2SDK.StartGameAsync(onSuccess, onError);
```

Handle pause / resume so your game reacts when the SDK pauses you (e.g. during an ad):

```csharp
Yes2SDK.Yes2SDK.OnPause  += () => { Time.timeScale = 0; AudioListener.pause = true;  };
Yes2SDK.Yes2SDK.OnResume += () => { Time.timeScale = 1; AudioListener.pause = false; };
```

### Ads (required)

Interstitial ads run at natural break points. Rewarded ads run only when the player opts in.

```csharp
Yes2SDK.Yes2SDK.Ads.ShowInterstitial(
    placement: "level-end",
    description: "Between levels",
    beforeAd: () => PauseGame(),
    afterAd:  () => ResumeGame(),
    onError:  err => ResumeGame()   // always resume, even on error
);

Yes2SDK.Yes2SDK.Ads.ShowRewarded(
    placement: "extra-life",
    description: "Extra life reward",
    beforeAd:    () => PauseGame(),
    afterAd:     () => ResumeGame(),
    adDismissed: () => { /* no reward */ },
    adViewed:    () => GiveReward(),
    onError:     err => ResumeGame()
);
```

### Gameplay Tracking (required)

Tells Yes2Games when an active round begins and ends. Also call `GameplayStop()` before any ad and `GameplayStart()` after.

```csharp
Yes2SDK.Yes2SDK.Game.GameplayStart();
Yes2SDK.Yes2SDK.Game.GameplayStop();
```

> `Analytics.LogLevelStart` / `LogLevelEnd` can also trigger gameplay start / stop — use either pair, but don't call both.

### Data (required)

Key-value storage. Persists across sessions automatically.

```csharp
Yes2SDK.Yes2SDK.Data.SetInt("highScore", 1500);
Yes2SDK.Yes2SDK.Data.SetString("playerName", "Hero");

int score = Yes2SDK.Yes2SDK.Data.GetInt("highScore", defaultValue: 0);

bool exists = Yes2SDK.Yes2SDK.Data.HasKey("highScore");
Yes2SDK.Yes2SDK.Data.DeleteKey("highScore");
```

### Analytics (recommended)

```csharp
Yes2SDK.Yes2SDK.Analytics.LogEvent("custom-event", new Dictionary<string, object> {
    { "key", "value" }
});

Yes2SDK.Yes2SDK.Analytics.LogLevelStart("level-1");
Yes2SDK.Yes2SDK.Analytics.LogLevelEnd("level-1", score: 1500, success: true);
Yes2SDK.Yes2SDK.Analytics.LogScore(1500);
```

### Session (recommended)

```csharp
string locale = Yes2SDK.Yes2SDK.Session.GetLocale();  // e.g. "en", "fr"
string device = Yes2SDK.Yes2SDK.Session.GetDevice();   // "desktop" or "mobile"
```

> Treat session info as a hint, not a guarantee. Don't branch your core game logic on it.

---

## Optional APIs

These modules add extra player-facing features. They are **not guaranteed** to be available at runtime — always guard with `IsSupported()` and handle `FeatureNotSupported` gracefully. Don't make your core gameplay depend on them.

### Auth

```csharp
if (Yes2SDK.Yes2SDK.Auth.IsSupported())
{
    Yes2SDK.Yes2SDK.Auth.GetCurrentUserAsync(
        onSuccess: user => Debug.Log($"User: {user.Name}, authenticated: {user.IsAuthenticated}"),
        onError:   err  => Debug.LogError(err)
    );

    Yes2SDK.Yes2SDK.Auth.SignInAsync(
        onSuccess: user => Debug.Log($"Signed in as {user.Name}"),
        onError:   err  => Debug.LogError(err)
    );
}
```

### Friends

```csharp
Yes2SDK.Yes2SDK.Friends.ListFriendsAsync(
    page: 0, size: 10,
    onSuccess: page => {
        foreach (var friend in page.Friends)
            Debug.Log($"{friend.Username} ({friend.Id})");
    },
    onError: err => Debug.LogError(err)
);
```

### Banners

Container-based display ads. Different from `Ads.ShowBanner`.

```csharp
Yes2SDK.Yes2SDK.Banners.ShowBanner("sidebar-left", BannerSize.Medium_300x250);
Yes2SDK.Yes2SDK.Banners.HideBanner("sidebar-left");
Yes2SDK.Yes2SDK.Banners.HideAllBanners();
```

### Game Extras

```csharp
Yes2SDK.Yes2SDK.Game.HappyTime();

Yes2SDK.Yes2SDK.Game.InviteLinkAsync(
    new Dictionary<string, string> { { "roomId", "abc123" } },
    onSuccess: link => Debug.Log($"Invite: {link}")
);

Yes2SDK.Yes2SDK.Game.ShowInviteButton(new Dictionary<string, string> { { "roomId", "abc123" } });
Yes2SDK.Yes2SDK.Game.HideInviteButton();

GameSettings settings = Yes2SDK.Yes2SDK.Game.GetSettings();
Yes2SDK.Yes2SDK.Game.OnSettingsChanged += s => ApplySettings(s);

Yes2SDK.Yes2SDK.Game.CopyToClipboard("https://...");
```

### Score

```csharp
Yes2SDK.Yes2SDK.Score.AddScore(150f);
Yes2SDK.Yes2SDK.Score.SubmitScore("encrypted-score-string");
```

### Player Data

```csharp
if (Yes2SDK.Yes2SDK.Player.IsDataSupported())
{
    Yes2SDK.Yes2SDK.Player.SetDataAsync("{\"level\":5}", onSuccess: () => {});
    Yes2SDK.Yes2SDK.Player.GetDataAsync(new[] { "level" }, onSuccess: json => {});
    Yes2SDK.Yes2SDK.Player.FlushDataAsync();
}

if (Yes2SDK.Yes2SDK.Player.IsConnectedPlayersSupported())
{
    Yes2SDK.Yes2SDK.Player.GetConnectedPlayersAsync(onSuccess: json => {});
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
