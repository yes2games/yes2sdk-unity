# Yes2SDK for Unity

A unified WebGL SDK that lets you integrate once and publish to **Poki**, **CrazyGames**, **Yandex Games**, **Game Distribution**, and **YouTube Playables** with a single codebase.

Yes2SDK wraps platform-specific APIs behind a common C# interface. Write your integration code once, build a single WebGL output, upload to the Yes2SDK Dashboard, and the dashboard handles platform-specific SDK injection and bundling.

## Requirements

- Unity 2021.3 or newer
- WebGL build target
- Dependency: `com.unity.nuget.newtonsoft-json` (>= 3.2.1)

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

After installing the package:

1. Open **Yes2SDK > Build Window** in the Unity menu bar
2. Click **Install Template** — this installs the `Yes2SDK-SuperSDK` WebGL template to your project
3. The status indicator at the bottom changes from "Setup Pending" to "Ready"

> **After editing templates in the package**, click **Reinstall Template** in the Settings section to copy changes into your project.

---

## Quick Start

This is the **minimum integration** required to publish on all supported platforms.

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

                // 2. Signal loading progress (call repeatedly as assets load)
                Yes2SDK.Yes2SDK.SetLoadingProgress(100);

                // 3. Tell the platform the game is ready to play
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

All platforms **require** this initialization flow. Without it, your game won't load on any platform.

---

## Cross-Platform API (All Platforms)

The following APIs work identically on all supported platforms and represent the **recommended integration surface** for maximum reach. If you only implement these, your game will pass review on every platform.

### Lifecycle (mandatory)

Every game **must** call these:

```csharp
// Initialize — call once at startup
Yes2SDK.Yes2SDK.InitializeAsync(onSuccess, onError);

// Loading progress — call as your game loads (0-100)
Yes2SDK.Yes2SDK.SetLoadingProgress(progress);

// Start game — call when loading is complete and game is playable
Yes2SDK.Yes2SDK.StartGameAsync(onSuccess, onError);
```

**Pause/Resume** — subscribe to know when the platform pauses your game (e.g., during an ad):

```csharp
Yes2SDK.Yes2SDK.OnPause += () => { Time.timeScale = 0; AudioListener.pause = true; };
Yes2SDK.Yes2SDK.OnResume += () => { Time.timeScale = 1; AudioListener.pause = false; };
```

### Ads (mandatory)

All platforms require ad integration. Call interstitial ads between levels or natural break points. Call rewarded ads when the player opts in for a reward.

```csharp
// Interstitial — between levels, menu transitions, etc.
Yes2SDK.Yes2SDK.Ads.ShowInterstitial(
    placement: "level-end",
    description: "Between levels",
    beforeAd: () => PauseGame(),
    afterAd: () => ResumeGame(),
    onError: err => ResumeGame()   // always resume even on error
);

// Rewarded — player chooses to watch for a reward
Yes2SDK.Yes2SDK.Ads.ShowRewarded(
    placement: "extra-life",
    description: "Extra life reward",
    beforeAd: () => PauseGame(),
    afterAd: () => ResumeGame(),
    adDismissed: () => { /* no reward */ },
    adViewed: () => GiveReward(),
    onError: err => ResumeGame()
);
```

### Gameplay Tracking (mandatory)

Platforms use this to understand player engagement. **All platforms require it.**

```csharp
// When gameplay begins (level start, round start, etc.)
Yes2SDK.Yes2SDK.Game.GameplayStart();

// When gameplay ends (level complete, game over, back to menu, etc.)
Yes2SDK.Yes2SDK.Game.GameplayStop();
```

> On Poki, `GameplayStart`/`GameplayStop` are also triggered by `Analytics.LogLevelStart`/`LogLevelEnd` — you can use either, but don't call both.

### Analytics (recommended)

```csharp
Yes2SDK.Yes2SDK.Analytics.LogEvent("custom-event", new Dictionary<string, object> {
    { "key", "value" }
});

Yes2SDK.Yes2SDK.Analytics.LogLevelStart("level-1");
Yes2SDK.Yes2SDK.Analytics.LogLevelEnd("level-1", score: 1500, success: true);
Yes2SDK.Yes2SDK.Analytics.LogScore(1500);
```

### Session Info (recommended)

```csharp
string locale = Yes2SDK.Yes2SDK.Session.GetLocale();   // e.g. "en", "fr"
string device = Yes2SDK.Yes2SDK.Session.GetDevice();    // "desktop" or "mobile"
```

> `GetCountry()` is only available on CrazyGames and Yandex. On other platforms it returns an empty string.

### Data Storage (mandatory)

Simple key-value storage that works on all platforms (localStorage on Poki, cloud storage on CrazyGames, platform API on Yandex, PlayerPrefs in Editor).

```csharp
// Save
Yes2SDK.Yes2SDK.Data.SetInt("highScore", 1500);
Yes2SDK.Yes2SDK.Data.SetString("playerName", "Hero");

// Load
int score = Yes2SDK.Yes2SDK.Data.GetInt("highScore", defaultValue: 0);

// Check & delete
bool exists = Yes2SDK.Yes2SDK.Data.HasKey("highScore");
Yes2SDK.Yes2SDK.Data.DeleteKey("highScore");
```

---

## Platform-Specific APIs (CrazyGames Only)

These features are available only on CrazyGames. On other platforms they return `FeatureNotSupported` or are silently ignored. Use `IsSupported` checks where available.

### Auth

```csharp
if (Yes2SDK.Yes2SDK.Auth.IsSupported())
{
    Yes2SDK.Yes2SDK.Auth.GetCurrentUserAsync(
        onSuccess: user => Debug.Log($"User: {user.Name}, authenticated: {user.IsAuthenticated}"),
        onError: err => Debug.LogError(err)
    );

    Yes2SDK.Yes2SDK.Auth.SignInAsync(
        onSuccess: user => Debug.Log($"Signed in as {user.Name}"),
        onError: err => Debug.LogError(err)
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

### Banners (Display Ads)

Container-based display ads at fixed positions. Different from `Ads.ShowBanner`.

```csharp
Yes2SDK.Yes2SDK.Banners.ShowBanner("sidebar-left", BannerSize.Medium_300x250);
Yes2SDK.Yes2SDK.Banners.HideBanner("sidebar-left");
Yes2SDK.Yes2SDK.Banners.HideAllBanners();
```

### Game Extras

```csharp
Yes2SDK.Yes2SDK.Game.HappyTime();     // CG "happy moment" signal

Yes2SDK.Yes2SDK.Game.InviteLinkAsync(
    new Dictionary<string, string> { { "roomId", "abc123" } },
    onSuccess: link => Debug.Log($"Invite: {link}")
);

Yes2SDK.Yes2SDK.Game.ShowInviteButton(new Dictionary<string, string> { { "roomId", "abc123" } });
Yes2SDK.Yes2SDK.Game.HideInviteButton();

GameSettings settings = Yes2SDK.Yes2SDK.Game.GetSettings(); // { DisableChat, MuteAudio }
Yes2SDK.Yes2SDK.Game.OnSettingsChanged += s => ApplySettings(s);

Yes2SDK.Yes2SDK.Game.CopyToClipboard("https://...");
```

### Score

```csharp
Yes2SDK.Yes2SDK.Score.AddScore(150f);
Yes2SDK.Yes2SDK.Score.SubmitScore("encrypted-score-string");
```

### Player Data & Social (via Player module)

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

## Platform Support Matrix

| Feature | Poki | CrazyGames | Yandex | Game Distribution | YouTube | Editor |
|---------|:----:|:----------:|:------:|:-----------------:|:-------:|:------:|
| **Lifecycle** (Init, StartGame, Loading) | yes | yes | yes | yes | yes | mock |
| **Ads** (Interstitial, Rewarded) | yes | yes | yes | yes | yes | mock |
| **Ads** (Banner via `Ads.ShowBanner`) | yes | yes | yes | yes | -- | mock |
| **Analytics** | yes | yes | yes | yes | yes | mock |
| **Session** (Locale, Device, Orientation) | yes | yes | yes | yes | yes | defaults |
| **Session** (Country) | -- | yes | yes | -- | -- | -- |
| **Data** (Key-Value Storage) | localStorage | cloud | platform API | localStorage | localStorage | PlayerPrefs |
| **Game** (GameplayStart/Stop) | yes | yes | yes | yes | yes | mock |
| **Game** (HappyTime, Invite, Settings) | -- | yes | -- | -- | -- | mock |
| **Player** (GetPlayer) | anonymous | full | full | anonymous | anonymous | anonymous |
| **Player** (Data, Social) | -- | yes | -- | -- | -- | -- |
| **Auth** | -- | yes | -- | -- | -- | -- |
| **Banners** (Multi-Size Display) | -- | yes | -- | -- | -- | mock |
| **Friends** | -- | yes | -- | -- | -- | -- |
| **Score** | log only | yes | -- | -- | -- | mock |

**yes** = fully supported | **mock** = simulated for testing | **--** = not available / returns `FeatureNotSupported` | **anonymous** = returns anonymous player info

---

## Mandatory Integration Checklist

Use this checklist to ensure your game will pass review on all platforms.

- [ ] Call `InitializeAsync` at startup
- [ ] Call `SetLoadingProgress` during loading
- [ ] Call `StartGameAsync` when loading completes
- [ ] Handle `OnPause` / `OnResume` (mute audio, pause game)
- [ ] Show interstitial ads at natural break points (level transitions, menus)
- [ ] Show rewarded ads with proper `adViewed` / `adDismissed` handling
- [ ] Always resume the game in `afterAd` AND `onError` callbacks
- [ ] Call `Game.GameplayStart()` when a round/level begins
- [ ] Call `Game.GameplayStop()` when a round/level ends or player returns to menu
- [ ] Use `Data` module for saving/loading player data

---

## Building

Yes2SDK uses the **SuperSDK pipeline** — you build once and the dashboard handles platform-specific bundling.

1. Open **Yes2SDK > Build Window**
2. Click **Apply Settings** — this sets the WebGL template and build configuration
3. Click **Build WebGL** or **Build and Run**
4. Zip the build output folder
5. Upload to the **Yes2SDK Dashboard**
6. Select target platforms in the dashboard
7. Download platform-specific bundles (or publish directly)

### Build Configuration

| Setting | Value |
|---------|-------|
| Template | Yes2SDK-SuperSDK |
| Compression | Disabled (platforms handle CDN delivery) |
| Code Stripping | Medium |
| Exception Support | None |

> There is no per-platform build configuration in Unity. The dashboard injects the correct platform SDK at upload time.

---

## Editor Testing

In the Unity Editor, all SDK calls work with mock implementations:

- `InitializeAsync` / `StartGameAsync` succeed immediately
- Ads simulate the full callback flow (beforeAd -> afterAd -> adViewed)
- `Data` module uses `PlayerPrefs`
- Platform-specific APIs return `FeatureNotSupported`

---

## Error Handling

All async methods accept an `onError` callback with an `Error` struct:

```csharp
public struct Error
{
    public string Code;       // e.g. "FeatureNotSupported"
    public string Message;    // human-readable description
    public string Context;    // additional context
    public ErrorCode ErrorCode; // parsed enum
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
C# Runtime  ->  .jslib bridge  ->  window.Yes2SDK.*  ->  Platform SDK
                                          ^
                            injected by Yes2SDK Dashboard
```

- **C# Runtime** (`Runtime/`): Static facade + module classes. Uses `[DllImport("__Internal")]` for WebGL, mock fallbacks via `#if UNITY_WEBGL && !UNITY_EDITOR`.
- **jslib Bridges** (`Plugins/`): 12 JavaScript files that call `window.Yes2SDK.*` and return results via `SendMessage('Bridge', ...)`.
- **WebGL Template** (`Assets/WebGLTemplates/Yes2SDK-SuperSDK/`): Bare HTML template. Does not define `window.Yes2SDK` — the dashboard injects the SuperSDK Core (`yes2sdk.umd.js`) and the selected platform adapter at build time.
- **Dashboard Pipeline**: Upload build zip -> dashboard injects SDK -> select target platforms -> download platform-ready bundles.

---

## License

Proprietary. See LICENSE file for details.
