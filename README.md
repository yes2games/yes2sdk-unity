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
3. Enter: `https://github.com/yes2games/yes2sdk-unity.git#v2.9.0` <!-- x-release-please-version -->
4. Click **Add**

> Pinning the URL with the release tag keeps the package hash stable across resolves. Bump the tag when a newer release ships. Without a tag, Package Manager re-resolves against `main` on every refresh and reports phantom diffs.

### Channels

```text
integration: https://github.com/yes2games/yes2sdk-unity.git#edge
production:  https://github.com/yes2games/yes2sdk-unity.git#vX.Y.Z
```

`edge` is a mutable Git tag that moves to the newest commit on `main` whose required CI went green, and it is a tag and nothing else — there is no GitHub Release and no prerelease behind it. Use it to integrate against unreleased work, never to ship. `vX.Y.Z` is a tag and a GitHub Release at the exact commit that was tested, and it is never rewritten — a content fix ships as a new SemVer rather than as a moved tag. That is the only thing a shipping game should resolve.

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

> The package's EditMode tests stay hidden from the Test Runner until the project opts in. See [Automated tests](#automated-tests) for the one-line `testables` entry.

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

Save progress when the platform closes the game. `OnExitRequested` is raised only on platforms that report an exit, and the SDK flushes player data right after the handler returns:

```csharp
Yes2SDK.OnExitRequested += () =>
{
    // Synchronous writes only.
    Yes2SDK.Data.SetString("save", SerializeProgress());
    Yes2SDK.Data.SetInt("level", currentLevel);
};
```

> Async work started in the handler (`SetStringAsync`, `FlushAsync`, a coroutine) is not awaited and may never finish. `PlayerPrefs.Save()` is not a substitute either: write through `Yes2SDK.Data` so the flush picks it up.

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

> **Resume in `onError` as well as `afterAd`. This is required, not optional.** An ad that fails (no fill, blocked, timed out) ends with `onError` and nothing else. Some platforms still send `afterAd` after a no-fill, but the SDK drops it, because the ad already ended at `onError`. A game that resumes only in `afterAd` stays paused after every failed ad. Each ad ends with exactly one of `afterAd` or `onError`, so resuming in both never runs twice.

#### Rewarded ad firing order

The callbacks fire in this order. Pay attention — getting it wrong silently breaks reward logic:

```text
beforeAd      → pause game (always)
(ad shown)
adViewed      → grant reward (ONLY fires if the player watched the full ad)
   — or —
adDismissed   → no reward (fires if the player skipped/closed early)
afterAd       → resume game (fires whether the player watched or dismissed)
```

If the ad fails instead, only `onError` fires, with no `afterAd` after it (see above).

**Exactly one outcome per ad.** Every rewarded ad reports exactly one of `adViewed`, `adDismissed` or `onError`, whatever the platform sends. If the platform completes the ad without reporting whether it was watched, the binding reports it as `adDismissed` (no reward) just before `afterAd` and logs a warning. It never reports such an ad as `adViewed`. A second outcome for the same ad is dropped with a warning, so a dismissed ad never also grants a reward. You can settle your reward flow on the outcome callback alone.

> ⚠️ **Do NOT grant rewards in `afterAd`.** `afterAd` fires for both completion *and* dismissal — granting rewards there gives them away on skip. Always grant in `adViewed`.

> `afterAd` is **last**, and it is what completes the ad: the outcome arrives first, then `afterAd`. A new ad can be started from `afterAd`, but not from `adViewed` or `adDismissed`, because the previous ad is still in flight there.

#### Format support

- `Ads.IsInterstitialSupported()` / `Ads.IsRewardedSupported()` — whether the platform serves that ad format at all. Use these to feature-gate ad UI up front instead of hard-coding which platforms have which format. Both report `false` when the injected runtime is older than the checks, so a gate never sends the game into a call the platform cannot serve. In the Editor both report `true`, because the mock serves either format.
- Banner support is a separate check on its own module: `Banners.IsSupported()`.

```csharp
// Hide the whole offer where the format is absent, rather than failing on click.
doubleCoinsButton.gameObject.SetActive(Yes2SDK.Ads.IsRewardedSupported());
```

Support is not readiness: these ask whether the format exists on the platform, `IsRewardedAdAvailable()` below asks whether an ad looks ready right now.

#### Concurrent ad guard + readiness

- `Ads.IsAdShowing()` — returns `true` while a `ShowInterstitial` or `ShowRewarded` is in flight (between the call and `afterAd`/error). Calling `Show*` again while one is already showing is rejected immediately — `onError` fires with `ErrorCode.InvalidParams` and a message starting `"Another ad is already in flight (AdAlreadyShowing)…"`.
- **An ad that never completes is released automatically.** If the platform does not start the ad within 30 seconds, or starts it and does not finish within 3 minutes of play (time in a hidden tab does not count), the ad is released and `onError` fires with `ErrorCode.Timeout`. `IsAdShowing()` then returns `false` and the next `Show*` runs normally. A late callback from the released ad is ignored. Resume your game in `onError` as well as `afterAd`, so a timed-out ad does not leave it paused. The Editor mock popup has no timeout.
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

> `Analytics.LogLevelStart` / `LogLevelEnd` also mark gameplay start / stop. Calling both pairs is safe: repeated starts or stops are ignored, so the platform sees one signal per transition.

### Data (required)

Key-value storage. Persists across sessions automatically.

```csharp
Yes2SDK.Data.SetInt("highScore", 1500);
Yes2SDK.Data.SetString("playerName", "Hero");

int score = Yes2SDK.Data.GetInt("highScore", defaultValue: 0);

bool exists = Yes2SDK.Data.HasKey("highScore");
Yes2SDK.Data.DeleteKey("highScore");
```

The `Set*` calls are fire-and-forget, and some platforms batch cloud writes (Yandex debounces them). When you need to know progress is actually stored, such as at a checkpoint, after a purchase, or before the game may close, use the confirmed variants:

```csharp
// Write one value and wait for the platform to confirm it
Yes2SDK.Data.SetStringAsync("save", json,
    onSuccess: saved => { if (!saved) ShowSaveWarning(); },
    onError:   err   => ShowSaveWarning());

// Push every pending write to the backing store
Yes2SDK.Data.FlushAsync(onSuccess: saved => Debug.Log($"Flushed: {saved}"));

// Task overloads take a CancellationToken
bool ok = await Yes2SDK.Data.FlushAsync(CancellationToken.None);
```

In the Editor both complete immediately with `true` (`PlayerPrefs` is saved on the spot). Overlapping calls are fine: each one reports its own result.

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

// Data from the link that opened the game, such as referral share data or
// registration prompt data. "{}" or an empty dictionary when there is none.
string entryJson = Yes2SDK.Session.GetEntryPointData();
Dictionary<string, object> entry = Yes2SDK.Session.GetEntryPointDataDictionary();
if (entry.TryGetValue("roomId", out object roomId)) JoinRoom(roomId as string);
```

In the dictionary, nested objects come back as `JObject`, arrays as `JArray`, numbers as `long` or `double`, and date-like strings stay strings.

> Treat session info as a hint, not a guarantee. Don't branch your core game logic on it.

---

## Optional APIs

These modules add extra player-facing features. They are **not guaranteed** to be available at runtime: guard with `IsSupported()` (available on `Auth`, `Friends`, `Banners`, `Score`, `Player`, `IAP`, `Referrals` and `Notifications`), and always handle `FeatureNotSupported` errors gracefully. Don't make your core gameplay depend on them.

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

// Synchronous: false for guests, before init, on platforms without accounts, and on any error.
bool registered = Yes2SDK.Auth.IsAuthenticated();
```

#### Registration prompt

`ShowRegistrationPrompt` shows the platform's sign-up prompt to a guest and returns a handle you wire to your own buttons. When the prompt cannot be shown (player already registered, invalid `Message`, SDK not initialized, platform without a prompt) it returns null and calls `onError` right away.

```csharp
void OfferRegistration()
{
    // Save first: the platform may reload the game after registration.
    Yes2SDK.Data.SetString("save", SerializeProgress());
    Yes2SDK.Data.FlushAsync(onSuccess: _ => ShowSignUpPanel());
}

void ShowSignUpPanel()
{
    if (Yes2SDK.Auth.IsAuthenticated()) return;

    RegistrationPrompt prompt = Yes2SDK.Auth.ShowRegistrationPrompt(
        new RegistrationPromptOptions
        {
            Theme = RegistrationPromptTheme.Dark,
            Data  = new Dictionary<string, object> { { "from", "save-slot" } }
        },
        onClose: () => HideSignUpPanel(),
        onError: err => HideSignUpPanel());
    if (prompt == null) return;   // onError already ran

    signUpButton.onClick.AddListener(prompt.Login);
    notNowButton.onClick.AddListener(() => { prompt.Close(); HideSignUpPanel(); });
}
```

- `Login()` hands off to the platform's registration flow, which may reload the game, so save before you prompt. Registration can finish outside the game: check `IsAuthenticated()` the next time the game opens. `Data` comes back through `Session.GetEntryPointData()` after registration.
- `onClose` fires at most once per prompt, when the platform reports it closed. Hide your own UI yourself after `Close()` rather than waiting for it. `Login()` and `Close()` on a closed prompt (`IsOpen == false`) log a warning and do nothing.
- `Message` is optional. When set it must be at most 140 characters and contain `{{registrationCode}}` exactly once.
- When you show your own prompt, turn off automatic login reminders in the Yes2Games Dashboard so the player is not asked twice.

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

### Referrals

Share a referral link and list the players who joined through it.

```csharp
if (Yes2SDK.Referrals.IsSupported())
{
    Yes2SDK.Referrals.ShareAsync(
        new ReferralShareOptions("party_mode_v1")
        {
            Title = "Play with me",
            Text  = "Join my party",
            Data  = new Dictionary<string, object> { { "inviter", playerId } }
        },
        onSuccess: result => { if (!result.Canceled) ShowThanks(); },
        onError:   err    => Debug.LogWarning(err));

    Yes2SDK.Referrals.ListAsync(
        onSuccess: list =>
        {
            // Send list.SignedRequest to your server and grant rewards from what it verifies.
            if (list.Referrals.TryGetValue("party_mode_v1", out var joined))
                Debug.Log($"{joined.Count} players joined");
        },
        onError: err => Debug.LogWarning(err));
}
```

- `Reference` is required: a stable campaign key that groups the conversions. Null options or an empty reference fail with `InvalidParams` right away.
- `Data` reaches the invited player through `Session.GetEntryPointData()`.
- `ImageDataUrl` takes a PNG, JPEG or WebP base64 data URL of at most 2 MB (see `Yes2SDKImage.ToPngDataUrl` under [Notifications](#notifications)).
- A closed share dialog is a success with `Canceled == true`, not an error.
- `ListAsync` groups `ReferralConversion`s (`PlayerId`, `JoinedAt`) by reference. A reference nobody joined through is absent. Verify `SignedRequest` on your server before granting a reward.

### Banners

There are two banner surfaces and they are not interchangeable. Pick by the
shape of banner the platform actually offers:

| Surface | Shape | Use when |
|---|---|---|
| `Ads.ShowBanner(BannerPosition)` | One banner, placed top or bottom by the platform. No id, no size. | The platform offers a single sticky banner and accepts no container or size. |
| `Banners.ShowBanner(id, BannerSize)` | Many banners, each in a named container at a size you choose. | The platform renders display ads into containers you lay out yourself. |

They are separate end to end: separate callbacks, separate platform mappings,
and no shared state. That last part is why mixing them breaks rather than merely
duplicating: hiding on one surface can clear banners the other surface placed,
and a refresh can miss one, because each surface tracks only what it placed
itself. Pick one surface per game.

Support for either surface varies by platform, and passing a container or size
to a platform that has neither means those arguments are ignored. Check the
per-method support tables before you commit to one:
[Ads](https://developer.yes2games.com/docs/api/ads),
[Banners](https://developer.yes2games.com/docs/api/banners).

Only the container surface has a support probe. `Banners.IsSupported()` answers
before you call; the position-based surface has no equivalent, so an unsupported
platform reports itself through `onError` after the fact.

```csharp
// Container-based: many banners, explicit sizes.
if (Yes2SDK.Banners.IsSupported())
{
    Yes2SDK.Banners.ShowBanner("sidebar-left", BannerSize.Medium_300x250);
    Yes2SDK.Banners.HideBanner("sidebar-left");
    Yes2SDK.Banners.HideAllBanners();
}

// Position-based: one sticky banner, platform picks the size.
Yes2SDK.Ads.ShowBanner(BannerPosition.Bottom,
    onShown: () => Debug.Log("Banner shown"),
    onError: err => Debug.LogError(err));
Yes2SDK.Ads.HideBanner();
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

### In-App Purchases

`Yes2SDK.IAP.IsSupported()` tells you at runtime whether the current platform can take payments. Check it before showing a shop, a "buy" button, or any mechanic that depends on paid items. When it returns `false`, hide that UI instead of letting the player hit an error.

Currently only **Yandex** supports IAP. On every other platform `IsSupported()` returns `false` and the calls fail through `onError`. On Yandex, payments must also be enabled for your game in the Yandex Games console, otherwise the calls fail with a platform error.

```csharp
if (Yes2SDK.IAP.IsSupported())
{
    // On launch: restore what the player already owns, and finish any
    // consumable purchase that was paid for but not yet granted.
    Yes2SDK.IAP.GetPurchasesAsync(
        onSuccess: purchasesJson => RestorePurchases(Purchase.ListFromJson(purchasesJson)),
        onError:   err => Debug.LogWarning(err));

    // Build the shop from the platform catalog (prices are localized).
    Yes2SDK.IAP.GetCatalogAsync(
        onSuccess: catalogJson => BuildShop(catalogJson),              // JSON array
        onError:   err => HideShop());
}
else
{
    HideShop();
}

// When the player taps "buy":
Yes2SDK.IAP.PurchaseAsync("gems_100",
    onSuccess: purchaseJson =>
    {
        var purchase = Purchase.FromJson(purchaseJson);
        GrantItem(purchase.ProductId);
        // Sandbox purchases move no real money: grant the item, but keep it
        // out of your revenue reporting.
        if (purchase.IsSandbox) MarkAsTestPurchase(purchase);
        // Consumables must be consumed so they can be bought again.
        Yes2SDK.IAP.ConsumePurchaseAsync(purchase.PurchaseToken);
    },
    onError: err =>
    {
        // Yandex reports a closed payment dialog the same way as a failed
        // payment, so keep this message neutral ("Purchase not completed").
        // Platforms that report a closed checkout separately send it as
        // err.ErrorCode == ErrorCode.UserCancelled.
        if (err.ErrorCode == ErrorCode.UserCancelled) return;
        ShowPurchaseNotCompleted();
    });
```

- Results arrive as JSON strings. Parse a purchase with `Purchase.FromJson(json)` and a `GetPurchasesAsync` array with `Purchase.ListFromJson(json)`; both return null or an empty list and log a warning on bad input. A `Purchase` carries `ProductId`, `PurchaseToken`, `PaymentId`, `PurchaseTime` (ISO 8601), `DeveloperPayload`, `SignedRequest` and `IsSandbox`. `GetCatalogAsync` returns a top-level JSON array, which `JsonUtility` cannot parse on its own: wrap it (`JsonUtility.FromJson<Wrapper>("{\"items\":" + json + "}")`) or use Newtonsoft. Each catalog product carries `productId`, `title`, `description`, `price` (formatted) and `priceCurrencyCode`.
- `IsSandbox` is true when no real money changed hands (a sandbox tester or the platform simulator, and always in the Editor mock). Grant the item, but keep it out of revenue reporting.
- `SignedRequest` is the platform's signed proof of the purchase, or null when the platform provides none. Verify it on your server before granting value for anything that matters.
- Recover incomplete purchases on startup with `GetPurchasesAsync` (see the launch example above), then grant and consume them.
- Grant the item before consuming it, and save progress (see `Data.FlushAsync`) so a closed tab can't lose a paid item. Any purchase that was paid but not consumed comes back from `GetPurchasesAsync` on the next launch.
- In the Editor, IAP is mocked in Play Mode (see [Editor Testing](#editor-testing)), so you can test your shop and your `IsSupported()` gating without a platform build.

#### Subscriptions

Subscriptions have their own check, `IAP.IsSubscriptionSupported()`, and typed results.

```csharp
if (Yes2SDK.IAP.IsSubscriptionSupported())
{
    // On every launch: the list is the source of truth for entitlements.
    Yes2SDK.IAP.GetSubscriptionsAsync(
        onSuccess: subscriptions =>
        {
            foreach (Subscription sub in subscriptions)
            {
                if (sub.IsActive) GrantVip(sub);                    // never re-offer it
                else ShowOffer(sub, showTrial: sub.TrialEligible);
            }
        },
        onError: err => HideSubscriptions());
}

// When the player taps "subscribe":
Yes2SDK.IAP.SubscribeAsync("vip_monthly",
    onSuccess: result => { if (result.IsSubscribed) GrantVip(result.Subscription); },
    onError:   err    => ShowPurchaseNotCompleted());
```

- Grant access when `IsActive` is true. Show trial copy only when `TrialEligible` is true. `IntroOffer` and `RetentionOffer` are null when there is none. `IsSandbox` and `SignedRequest` work as on `Purchase`.
- A closed checkout is a success with `result.Status == SubscribeStatus.Cancelled`, not an error.
- Never offer a subscription the player already holds: `SubscribeAsync` fails with `err.Code == "IAP_ALREADY_PURCHASED"`.
- Guests may get an empty list, and `SubscribeAsync` fails with `err.Code == "PLAYER_NOT_AUTHENTICATED"`. Offer a [registration prompt](#registration-prompt) instead.
- `CancelSubscriptionAsync(productId)` reports `true` when the player confirmed and `false` when they dismissed the dialog. The player keeps access until the end of the billing period, so do not revoke it at once.
- `ClaimRetentionOfferAsync(productId)` applies `RetentionOffer` to a subscription the player holds and returns the refreshed `Subscription`. Repeating a claim is safe. A player who does not hold it gets `err.Code == "INVALID_OPERATION"`.

### Notifications

```csharp
if (Yes2SDK.Notifications.IsSupported())
{
    Yes2SDK.Notifications.ScheduleAsync(
        new NotificationOptions
        {
            Id              = "daily-reward",   // scheduling the same id again replaces it
            Title           = "Your reward is ready",
            Body            = "Come back and claim today's chest.",
            ScheduledInDays = 1,                // or DelaySeconds: exactly one of the two
            CtaText         = "Claim",
            Priority        = NotificationPriority.High,
            ImageDataUrl    = Yes2SDKImage.ToPngDataUrl(chestTexture),
            Data            = new Dictionary<string, object> { { "reward", "chest" } }
        },
        onSuccess: scheduled => Debug.Log($"Scheduled {scheduled.Id} at {scheduled.ScheduledAt}"),
        onError:   err       => Debug.LogWarning(err));

    Yes2SDK.Notifications.CancelAsync("daily-reward");
}
```

| Option | Rule |
|---|---|
| `Title` | Required, at most 200 characters. |
| `Body` | 1 to 2000 characters. |
| `DelaySeconds` | Positive, at most 7 days. Set this or `ScheduledInDays`, exactly one. |
| `ScheduledInDays` | Whole days from now, 0 to 7. The platform picks the best time inside that day. |
| `Id` | Optional. Generated when null; reusing an id replaces that notification. An empty string is rejected. |
| `CtaText` | Optional button label, 1 to 50 characters. |
| `Priority` | `Low`, `Medium` (default), `High` or `Critical`. |
| `ImageAssetId` / `ImageDataUrl` | At most one. `ImageDataUrl` is a PNG, JPEG or WebP base64 data URL with a lowercase prefix, at most 2 MiB. |
| `IconUrl` | Optional icon URL, on platforms that use one. |
| `Data` | Optional data handed back to the game when the player opens the notification. |

- `Yes2SDKImage.ToPngDataUrl(texture)` builds an `ImageDataUrl` from a readable texture. It returns null and logs a warning when the texture is not readable (enable Read/Write) or encodes to more than 2 MiB; a null image is simply not sent.
- Options that break these rules fail with `InvalidParams`. On platforms that only notify registered players, a guest gets `err.Code == "PLAYER_NOT_AUTHENTICATED"`.
- Keep `ScheduledNotification.Id` to cancel later. `CancelAllAsync()` cancels every notification the game scheduled (some platforms only reach the ones scheduled this session).
- The older `ScheduleAsync(title, body, delaySec, dataJson, ...)` overload still works and passes the new id to `onSuccess`. Prefer `NotificationOptions`.

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
- **Ads show a fullscreen mock ad in Play Mode**: a countdown (3s interstitial, 5s rewarded), then **Close Ad** (fires `afterAd`), or **Claim Reward** (fires `adViewed`) / **Skip** (fires `adDismissed`) for rewarded ads. This lets you verify pause-resume wiring and both reward outcomes by clicking. The ad fills the game view and scales with its resolution, landscape or portrait. While the popup is up, input to the game behind it is blocked (uGUI clicks, legacy `Input` axis/button polling), matching how a real ad overlay behaves. Direct new Input System device polling (e.g. `Keyboard.current`) is not suppressed.
- **IAP is mocked in Play Mode**: `IsSupported()` returns true, `GetCatalogAsync` returns a sample catalog, and `PurchaseAsync` opens a Buy / Cancel dialog. Any product id is accepted, so you can test with your real ids. Purchases last for the current play session.
- **Failures can be simulated**: the Ad result dropdown (No fill / Ad blocked / Error) makes ad calls fire `onError` with platform-shaped error codes, and Fail purchases makes `PurchaseAsync` fail, so error handling is testable without a platform build.
- **Subscriptions follow the IAP mock**: `SubscribeAsync` opens a Subscribe / Close dialog and `CancelSubscriptionAsync` a confirm dialog. The mock applies the platform rules (guest, already held, not held) so those error paths are testable.
- **Player is registered** sets whether the mock player is signed in (off, a guest, by default). It drives `IsAuthenticated()`, the subscription list and the guest errors. The registration prompt is mocked without UI: `Login()` registers the player for the current play session and `Close()` closes the prompt.
- **Entry point data (JSON)** is what `Session.GetEntryPointData()` returns in Play Mode. Only a valid JSON object is saved.
- **Mock referrals and notifications** turns on those mocks. **Referral share result** picks Shared / Cancelled / Error, and **Referral conversions** sets how many players joined through each shared reference; with 0, references are left out of `ListAsync` results, as on a real platform.
- **Simulate exit request** (Play Mode only) raises `OnExitRequested`, then saves game data the way the platform flush does.
- `Data` uses `PlayerPrefs`
- Other optional APIs return `FeatureNotSupported`

The mocks can be turned off under **Yes2SDK > Build Window > Play Mode Testing**. With the ad popup off, ad callbacks fire instantly with no UI (pass `"dismiss"` as the rewarded description to trigger `adDismissed`). Batch-mode runs (CI) always use the instant flow.

For richer simulation (specific locales, network conditions, event log capture), use the **QA Inspector** in the Yes2Games Dashboard.

### Automated tests

The package ships EditMode tests covering the ad callback contract: callback order, and the in-flight teardown that keeps one bad ad from blocking every later one. They run on the instant flow, so they need no rendering and work in batch mode.

To see them in a consuming project, add the package to `testables` in that project's `Packages/manifest.json`, and add the test framework there too — the package does not depend on it, because a shipping game has no reason to inherit test infrastructure:

```json
{
  "dependencies": {
    "com.unity.test-framework": "1.4.6"
  },
  "testables": [
    "com.yes2games.yes2sdk"
  ]
}
```

`ci~/consumer` is the committed minimal project that does exactly this, and it is what the required CI lanes run the tests through.

They then appear under **Window > General > Test Runner > EditMode**. Headless:

```bash
Unity -batchmode -nographics -projectPath <project>   -runTests -testPlatform EditMode -testResults results.xml
```

`testables` is a project-manifest field, so a package cannot opt itself in; each consuming project adds the line.

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

`err.ErrorCode` is the typed code to branch on. It reads both the SDK's own codes and the platform runtime's codes (such as `PLATFORM_ERROR` or `NETWORK_FAILURE`), while `err.Code` keeps the original string for logging.

| Code | When it fires | Recommended handling |
|------|---------------|----------------------|
| `NotInitialized` | An API was called before `InitializeAsync` succeeded. | Wait for init to complete first; never call SDK methods from `Awake()` without checking `Yes2SDK.IsInitialized`. |
| `InvalidParams` | A required parameter was null/empty or out of range. | Treat as a programmer error — fix the call site. |
| `FeatureNotSupported` | The optional API isn't available on this platform (e.g. Friends on Poki). | Hide the related UI; fall back to a non-platform alternative. Always pre-check with `IsSupported()` for optional APIs. |
| `PlatformError` | The underlying platform SDK rejected the call. | Log `err.Message` and `err.Context` for support; treat the call as failed. |
| `NetworkError` | A platform call failed network-side (timeout, offline, server error). | Retry with backoff. Don't retry indefinitely. |
| `RateLimited` | Too many calls in a short window (e.g. ad spam protection). | Back off and try again later — don't retry immediately. |
| `UserCancelled` | The player closed/dismissed a flow (e.g. login dialog, rewarded ad, purchase checkout). | Not an error in the usual sense - silently respect the player's choice, no toast. |
| `Unknown` | The error didn't match any of the above. | Log everything (`err.Code`, `err.Message`, `err.Context`) and treat as a hard failure. |
| `Timeout` | Raised by the SDK's ad watchdog: an interstitial or rewarded ad never started, or started and never finished. | Treat the ad as failed and resume the game. The next `Show*` works normally. |

---

## Running alongside other SDKs

Real games often ship with multiple platform SDKs in the same build (Yes2SDK + Poki + Yandex + Playgama, etc.). A few ground rules to keep them from stepping on each other:

- **Init order.** Initialize Yes2SDK first. Yes2SDK figures out which actual platform is hosting the game and routes through it — initializing your own platform SDK directly first can race with Yes2SDK's detection.
- **One owner for pause / resume.** Pick one SDK to drive `Time.timeScale` and `AudioListener.pause`. If both Yes2SDK and another SDK call resume/pause, you'll get oscillation. Recommended: subscribe to `Yes2SDK.OnPause` / `OnResume` and ignore the other SDK's equivalent.
- **One owner for ads.** Don't call ads via two SDKs in the same session — the platform almost always rejects the second call. Pick the SDK that targets the platform you're actually hosted on.
- **Namespace collisions.** If you have your own `Platform` type, qualify the Yes2SDK enum (`Yes2SDK.Platform`) at the call site or use a `using` alias (`using Y2 = Yes2SDK;`). C# resolves namespace-vs-type ambiguity by full qualification.
- **Init timeout.** If you depend on Yes2SDK init completing before your other SDK's flow, wrap `InitializeAsync` in a `CancellationToken` with a timeout (see [`await`-friendly overloads](#await-friendly-overloads)) so your game doesn't hang on a wedged JS bridge.

---

## Contributing

After cloning, point Git at the committed hooks once:

```bash
git config core.hooksPath .githooks
```

`.githooks/pre-push` then refuses a direct push to the default branch, so changes go through a pull request and face `YES2 Unity Required CI`.

**It is a soft guard, not enforcement.** It fires only in a clone that has run the command above, and `git push --no-verify` bypasses it outright. Nothing protects `main` server-side: this organization's plan and the deliberately uniform cross-SDK contract mean there is no branch protection, no ruleset and no CODEOWNERS gate on any Yes2 SDK (yes2games/yes2dashboard#141 sections 3 and 14). Read the checklist as "a mistake is caught", never as "`main` is protected".

---

## License

MIT — see [LICENSE](LICENSE).
