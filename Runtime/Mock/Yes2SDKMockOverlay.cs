#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

namespace Yes2SDK
{
    /// <summary>
    /// Editor-only mock popup shown in Play Mode for ads and IAP purchases,
    /// so integrations can be tested interactively before a platform build.
    ///
    /// Ads: a full-screen overlay with a forced countdown (3s interstitial,
    /// 5s rewarded) and then Close / Claim Reward / Skip buttons. Callbacks
    /// fire through the same internal Invoke* entry points the WebGL bridge
    /// uses, so callback ordering and the in-flight guard behave exactly like
    /// a platform build.
    ///
    /// IAP: a Buy / Cancel confirmation dialog; Buy resolves PurchaseAsync
    /// with a realistic purchase payload, Cancel with a UserCancelled error.
    ///
    /// Drawn with IMGUI so the Runtime assembly needs no uGUI reference.
    /// The countdown uses realtime, not scaled time, because games typically
    /// pause (Time.timeScale = 0) in beforeAd.
    ///
    /// While a popup is visible the game behind it is shielded from input,
    /// like a platform's DOM ad overlay: the EventSystem is disabled (uGUI /
    /// Input System UI clicks), legacy Input polling is reset before game
    /// scripts run each frame, and unused IMGUI events are consumed. Direct
    /// new-Input-System device polling (e.g. Keyboard.current) is the one
    /// path that cannot be suppressed without a hard Input System dependency.
    /// </summary>
    // Execution order far ahead of game scripts so Update/FixedUpdate can
    // clear legacy input before anything else polls it, and OnGUI sees
    // events first.
    [DefaultExecutionOrder(-32000)]
    internal class Yes2SDKMockOverlay : MonoBehaviour
    {
        private enum Kind { None, Interstitial, Rewarded, Purchase }

        private const float InterstitialSeconds = 3f;
        private const float RewardedSeconds = 5f;

        private static Yes2SDKMockOverlay _instance;

        private Kind _kind = Kind.None;
        private string _placement;
        private float _canCloseAt;

        private string _productId;
        private string _productTitle;
        private string _productPrice;
        private string _developerPayload;

        // Input shield state. _suppressInputUntilFrame keeps swallowing
        // legacy input for one frame after the popup closes so the dismissing
        // click can't leak into the game as a fire/jump/etc.
        private Behaviour _disabledEventSystem;
        private CursorLockMode _restoreCursorLock;
        private bool _restoreCursorVisible;
        private int _suppressInputUntilFrame = -1;

        private GUIStyle _badgeStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _adTitleStyle;
        private GUIStyle _adBodyStyle;
        private GUIStyle _adHintStyle;

        // Popup scale for the current game view resolution. IMGUI draws in
        // raw pixels, so without this the popup renders tiny on QHD/4K game
        // views. Reference size 1280x720 = the layout constants at scale 1;
        // never scaled below 1 so small Free Aspect windows keep the base
        // size. Styles are rebuilt when the scale changes (view resized).
        private float _uiScale = 1f;
        private float _stylesScale = -1f;

        #region Static entry points (called from Yes2SDKAds / Yes2SDKIAP)

        /// <summary>True while any mock popup is on screen.</summary>
        internal static bool IsBusy => _instance != null && _instance._kind != Kind.None;

        /// <summary>Show the interstitial popup. Returns false if another popup is open.</summary>
        internal static bool ShowInterstitial(string placement)
        {
            var overlay = GetOrCreate();
            if (overlay._kind != Kind.None) return false;

            overlay._kind = Kind.Interstitial;
            overlay._placement = placement;
            overlay._canCloseAt = Time.realtimeSinceStartup + InterstitialSeconds;
            overlay.AcquireInputShield();
            Yes2SDKAds.InvokeInterstitialBeforeAd();
            return true;
        }

        /// <summary>Show the rewarded popup. Returns false if another popup is open.</summary>
        internal static bool ShowRewarded(string placement)
        {
            var overlay = GetOrCreate();
            if (overlay._kind != Kind.None) return false;

            overlay._kind = Kind.Rewarded;
            overlay._placement = placement;
            overlay._canCloseAt = Time.realtimeSinceStartup + RewardedSeconds;
            overlay.AcquireInputShield();
            Yes2SDKAds.InvokeRewardedBeforeAd();
            return true;
        }

        /// <summary>Show the purchase dialog. Returns false if another popup is open.</summary>
        internal static bool ShowPurchase(string productId, string developerPayload)
        {
            var overlay = GetOrCreate();
            if (overlay._kind != Kind.None) return false;

            var product = Yes2SDKMockIAP.FindProduct(productId);
            overlay._kind = Kind.Purchase;
            overlay._productId = productId;
            overlay._productTitle = product?.Title ?? productId;
            overlay._productPrice = product?.Price ?? "$0.99 (mock price)";
            overlay._developerPayload = developerPayload;
            overlay.AcquireInputShield();
            return true;
        }

        private static Yes2SDKMockOverlay GetOrCreate()
        {
            // Destroyed-on-play-exit instances compare equal to null, so this
            // also recovers when Domain Reload is disabled.
            if (_instance == null)
            {
                var go = new GameObject("Yes2SDKMockOverlay");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<Yes2SDKMockOverlay>();
            }
            return _instance;
        }

        #endregion

        #region Completion

        // Clear state and release the input shield BEFORE invoking user
        // callbacks, so a callback that immediately shows another popup
        // starts from a clean slate.
        private void Hide()
        {
            _kind = Kind.None;
            ReleaseInputShield();
            _suppressInputUntilFrame = Time.frameCount + 1;
        }

        private void CompleteInterstitial()
        {
            Hide();
            Yes2SDKAds.InvokeInterstitialAfterAd();
        }

        private void CompleteRewarded(bool viewed)
        {
            Hide();
            // Same ordering as the platform flow: afterAd (resume the game),
            // then the reward outcome.
            Yes2SDKAds.InvokeRewardedAfterAd();
            if (viewed)
            {
                Yes2SDKAds.InvokeRewardedAdViewed();
            }
            else
            {
                Yes2SDKAds.InvokeRewardedAdDismissed();
            }
        }

        private void CompletePurchase(bool confirmed)
        {
            string productId = _productId;
            string payload = _developerPayload;
            Hide();

            if (confirmed)
            {
                Yes2SDKIAP.InvokePurchaseSuccess(Yes2SDKMockIAP.RecordPurchase(productId, payload));
            }
            else
            {
                Yes2SDKIAP.InvokePurchaseError(new Error
                {
                    Code = "UserCancelled",
                    Message = "Purchase cancelled by user (mock)",
                    Context = "Yes2SDK.IAP.PurchaseAsync"
                });
            }
        }

        #endregion

        #region Input shield

        private void AcquireInputShield()
        {
            // uGUI and Input System UI clicks route through the EventSystem;
            // disabling it makes game buttons behind the overlay inert, the
            // same way a platform's DOM ad overlay swallows pointer events.
            // Looked up via reflection so the SDK takes no hard uGUI
            // dependency (EventSystem derives from Behaviour, so the returned
            // instance can be driven without referencing its type).
            _disabledEventSystem = FindActiveEventSystem();
            if (_disabledEventSystem != null)
            {
                _disabledEventSystem.enabled = false;
            }

            // Games that lock the cursor (FPS controls) could never click the
            // popup buttons; unlock while the popup is up, restore after.
            _restoreCursorLock = Cursor.lockState;
            _restoreCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ReleaseInputShield()
        {
            // Destroyed-while-open (scene change) leaves this fake-null;
            // nothing to restore then.
            if (_disabledEventSystem != null)
            {
                _disabledEventSystem.enabled = true;
            }
            _disabledEventSystem = null;

            Cursor.lockState = _restoreCursorLock;
            Cursor.visible = _restoreCursorVisible;
        }

        private static Behaviour FindActiveEventSystem()
        {
            var type = Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.UI");
            var currentProperty = type?.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            var current = currentProperty?.GetValue(null) as Behaviour;
            return current != null && current.enabled ? current : null;
        }

        // DefaultExecutionOrder(-32000) runs these before game scripts, so
        // legacy Input polling (GetAxis / GetButton / GetKey / mouse buttons)
        // reads as idle everywhere behind the popup for the whole frame.
        // FixedUpdate too: physics-rate scripts poll before Update runs.
        private void Update()
        {
            if (_kind != Kind.None || Time.frameCount <= _suppressInputUntilFrame)
            {
                Input.ResetInputAxes();
            }
        }

        private void FixedUpdate()
        {
            if (_kind != Kind.None || Time.frameCount <= _suppressInputUntilFrame)
            {
                Input.ResetInputAxes();
            }
        }

        private void OnDestroy()
        {
            if (_kind != Kind.None)
            {
                ReleaseInputShield();
            }
        }

        #endregion

        #region IMGUI

        private void OnGUI()
        {
            if (_kind == Kind.None) return;

            GUI.depth = -1000;

            // Scale from the view's short edge so portrait views scale up
            // the same way landscape ones do (720 short-edge = scale 1).
            _uiScale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) / 720f);
            EnsureStyles(_uiScale);

            // Dim the whole screen.
            var previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;

            Rect cardRect;
            if (_kind == Kind.Purchase)
            {
                // Purchase prompts are dialogs on real platforms; keep a
                // centered modal.
                float cardWidth = Mathf.Min(480f * _uiScale, Screen.width - 32f * _uiScale);
                float cardHeight = 260f * _uiScale;
                cardRect = new Rect(
                    (Screen.width - cardWidth) / 2f,
                    (Screen.height - cardHeight) / 2f,
                    cardWidth,
                    cardHeight);
            }
            else
            {
                // Real interstitials are fullscreen; fill the view minus a
                // slim margin, whatever the aspect ratio or orientation.
                float margin = 16f * _uiScale;
                cardRect = new Rect(
                    margin,
                    margin,
                    Screen.width - margin * 2f,
                    Screen.height - margin * 2f);
            }

            GUI.Box(cardRect, GUIContent.none);
            float pad = 20f * _uiScale;
            var inner = new Rect(
                cardRect.x + pad,
                cardRect.y + pad * 0.75f,
                cardRect.width - pad * 2f,
                cardRect.height - pad * 1.5f);
            GUILayout.BeginArea(inner);

            if (_kind == Kind.Purchase)
            {
                DrawPurchase();
            }
            else
            {
                DrawAd();
            }

            GUILayout.EndArea();

            // Consume any input event our controls didn't use, so other
            // OnGUI-based game UI behind the popup stays inert (this script's
            // OnGUI runs first thanks to DefaultExecutionOrder).
            var e = Event.current;
            if (e.type == EventType.MouseDown || e.type == EventType.MouseUp
                || e.type == EventType.MouseDrag || e.type == EventType.ScrollWheel
                || e.type == EventType.KeyDown || e.type == EventType.KeyUp)
            {
                e.Use();
            }
        }

        private void DrawAd()
        {
            bool rewarded = _kind == Kind.Rewarded;

            GUILayout.Label(rewarded ? "MOCK REWARDED AD" : "MOCK INTERSTITIAL AD", _badgeStyle);
            GUILayout.Label($"placement: {_placement}", _hintStyle);

            // Centered "creative" in the middle of the fullscreen ad.
            GUILayout.FlexibleSpace();
            GUILayout.Label(rewarded ? "Watch this ad to earn a reward" : "Advertisement", _adTitleStyle);
            GUILayout.Space(6f * _uiScale);
            GUILayout.Label("This is a mock ad from Yes2SDK. Real ads only show on platform builds.", _adBodyStyle);
            GUILayout.FlexibleSpace();

            float buttonHeight = 40f * _uiScale;
            // Cap button widths so they stay button-sized on wide views but
            // still fit two-abreast in narrow portrait ones.
            float availableWidth = Screen.width - 80f * _uiScale;
            float pairWidth = Mathf.Min(220f * _uiScale, (availableWidth - 8f * _uiScale) / 2f);
            float singleWidth = Mathf.Min(320f * _uiScale, availableWidth);

            float remaining = _canCloseAt - Time.realtimeSinceStartup;
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (remaining > 0f)
            {
                GUI.enabled = false;
                GUILayout.Button($"Ad playing... {Mathf.CeilToInt(remaining)}s", _buttonStyle,
                    GUILayout.Width(singleWidth), GUILayout.Height(buttonHeight));
                GUI.enabled = true;
            }
            else if (rewarded)
            {
                if (GUILayout.Button("Claim Reward (adViewed)", _buttonStyle,
                    GUILayout.Width(pairWidth), GUILayout.Height(buttonHeight)))
                {
                    CompleteRewarded(viewed: true);
                }
                GUILayout.Space(8f * _uiScale);
                if (GUILayout.Button("Skip (adDismissed)", _buttonStyle,
                    GUILayout.Width(pairWidth), GUILayout.Height(buttonHeight)))
                {
                    CompleteRewarded(viewed: false);
                }
            }
            else
            {
                if (GUILayout.Button("Close Ad (afterAd)", _buttonStyle,
                    GUILayout.Width(singleWidth), GUILayout.Height(buttonHeight)))
                {
                    CompleteInterstitial();
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(8f * _uiScale);
            GUILayout.Label(
                rewarded
                    ? "Claim fires afterAd then adViewed (grant the reward). Skip fires afterAd then adDismissed."
                    : "Close fires afterAd. Pause your game in beforeAd, resume in afterAd.",
                _adHintStyle);
        }

        private void DrawPurchase()
        {
            GUILayout.Label("MOCK PURCHASE", _badgeStyle);
            GUILayout.Space(10f * _uiScale);

            GUILayout.Label(_productTitle, _titleStyle);
            GUILayout.Label($"Product id: {_productId}", _bodyStyle);
            GUILayout.Label($"Price: {_productPrice}", _bodyStyle);
            if (!string.IsNullOrEmpty(_developerPayload))
            {
                GUILayout.Label($"Developer payload: {_developerPayload}", _bodyStyle);
            }

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Buy", _buttonStyle, GUILayout.Height(34f * _uiScale)))
            {
                CompletePurchase(confirmed: true);
            }
            if (GUILayout.Button("Cancel (UserCancelled)", _buttonStyle, GUILayout.Height(34f * _uiScale)))
            {
                CompletePurchase(confirmed: false);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f * _uiScale);
            GUILayout.Label("Mock purchases last for this play session only.", _hintStyle);
        }

        private void EnsureStyles(float scale)
        {
            if (_badgeStyle != null && Mathf.Approximately(scale, _stylesScale)) return;
            _stylesScale = scale;

            // Fonts are scaled directly (rather than via GUI.matrix) so text
            // rasterizes at the target size and stays crisp on 4K views.
            _badgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(11f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.4f) }
            };
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(16f * scale),
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(12f * scale),
                wordWrap = true,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(10f * scale),
                wordWrap = true,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(13f * scale)
            };

            // Centered variants for the fullscreen ad layout.
            _adTitleStyle = new GUIStyle(_titleStyle)
            {
                fontSize = Mathf.RoundToInt(22f * scale),
                alignment = TextAnchor.UpperCenter
            };
            _adBodyStyle = new GUIStyle(_bodyStyle)
            {
                fontSize = Mathf.RoundToInt(13f * scale),
                alignment = TextAnchor.UpperCenter
            };
            _adHintStyle = new GUIStyle(_hintStyle)
            {
                alignment = TextAnchor.UpperCenter
            };
        }

        #endregion
    }
}
#endif
