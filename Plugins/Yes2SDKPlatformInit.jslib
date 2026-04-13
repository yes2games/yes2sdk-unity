mergeInto(LibraryManager.library, {

    // -----------------------------------------------------------------------
    // CrazyGames platform bootstrap via __postset
    //
    // CrazyGames replaces the game's HTML template with their own loader on
    // upload, so the inline <script> that normally defines window.Yes2SDK
    // never runs. This postset creates the CG wrapper inside framework.js
    // which IS preserved.
    //
    // Guard 1: skip if the template already created it (local testing / Poki / Debug)
    // Guard 2: only create when the CrazyGames SDK is present
    // -----------------------------------------------------------------------
    $__yes2PlatformInit__postset: 'window.__yes2PlatformInit = __yes2PlatformInit; __yes2PlatformInit();',
    $__yes2PlatformInit: function() {
        // Guard 1: template already set up the wrapper
        if (typeof window.Yes2SDK !== 'undefined') return;

        // Ensure logger exists (fallback if __y2 postset hasn't run yet)
        if (typeof window.__y2 === 'undefined') {
            var S = 'background:#6C5CE7;color:#fff;padding:2px 6px;border-radius:3px;font-weight:bold';
            function m(f) {
                return function() {
                    var a = [].slice.call(arguments);
                    a.unshift('%c[Yes2SDK]%c ', S, '');
                    console[f].apply(console, a);
                };
            }
            window.__y2 = { log: m('log'), warn: m('warn'), error: m('error') };
        }

        // Guard 2: if yes2sdk-data.js config explicitly names a non-CG platform, never
        // create a CG wrapper. GD/CrazyGames share Azerion infrastructure so
        // window.CrazyGames.SDK can be present on revision.gamedistribution.com — without
        // this guard, the postset would wrongly replace the GD SDK with a CG wrapper.
        var cfgPlatform = window.__yes2sdkConfig && window.__yes2sdkConfig.platform;
        if (cfgPlatform && cfgPlatform !== 'crazygames') {
            window.__y2.log('PlatformInit: dashboard config says platform is "' + cfgPlatform +
                '", skipping CG wrapper creation.');
            return;
        }

        // Guard 3: not on CrazyGames — check multiple possible globals
        var hasCG = (typeof window.CrazyGames !== 'undefined' && window.CrazyGames.SDK);
        if (!hasCG) {
            window.__y2.log('PlatformInit: window.CrazyGames.SDK not found, skipping CG wrapper.',
                'CrazyGames:', typeof window.CrazyGames,
                'CrazyGamesAds:', typeof window.CrazyGamesAds);
            return;
        }
        window.__y2.log('PlatformInit: CrazyGames SDK detected, creating wrapper via postset/lazy init');

        // ===================================================================
        // CrazyGames wrapper — mirrors Yes2SDK-CrazyGames/index.html exactly
        // ===================================================================
        window.Yes2SDK = {
            _initialized: false,
            _platform: 'crazygames',
            _sdk: null,
            _loadingStarted: false,
            _settingsListeners: [],

            initializeAsync: function() {
                var self = this;
                window.__y2.log('initializeAsync called');
                if (typeof window.CrazyGames === 'undefined' || !window.CrazyGames.SDK) {
                    window.__y2.error('CrazyGames SDK not found');
                    return Promise.reject({ code: 'NotInitialized', message: 'CrazyGames SDK not found' });
                }
                window.__y2.log('CrazyGames SDK found, calling init...');
                self._sdk = window.CrazyGames.SDK;
                // Pass wrapper info like official CG Unity SDK (crazySDK.jslib)
                var initOptions = {
                    wrapper: {
                        engine: 'unity',
                        sdkVersion: '1.0.0'
                    }
                };
                return self._sdk.init(initOptions).then(function() {
                    self._initialized = true;
                    window.__y2.log('Initialized for CrazyGames');
                }).catch(function(error) {
                    window.__y2.error('CrazyGames init failed:', error);
                    return Promise.reject(error);
                });
            },

            startGameAsync: function() {
                if (this._sdk) {
                    this._sdk.game.loadingStop();
                }
                window.__y2.log('Game started');
                return Promise.resolve();
            },

            setLoadingProgress: function(progress) {
                if (this._sdk && !this._loadingStarted) {
                    this._sdk.game.loadingStart();
                    this._loadingStarted = true;
                }
            },

            performHapticFeedback: function() {
                // CrazyGames doesn't support haptic feedback
            },

            getPlatform: function() {
                return this._platform;
            },

            get isInitialized() {
                return this._initialized;
            },

            // Ads module - wraps CrazyGames SDK ad methods
            ads: {
                showInterstitial: function(placement, callbacks) {
                    callbacks = callbacks || {};
                    var sdk = window.Yes2SDK._sdk;
                    if (!sdk) {
                        if (callbacks.noFill) callbacks.noFill();
                        if (callbacks.afterAd) callbacks.afterAd();
                        return Promise.resolve();
                    }

                    if (callbacks.beforeAd) callbacks.beforeAd();

                    return new Promise(function(resolve) {
                        sdk.ad.requestAd('midgame', {
                            adStarted: function() {
                                window.__y2.log('Midgame ad started');
                            },
                            adFinished: function() {
                                window.__y2.log('Midgame ad finished');
                                if (callbacks.afterAd) callbacks.afterAd();
                                resolve();
                            },
                            adError: function(error) {
                                window.__y2.warn('Midgame ad error:', error);
                                if (callbacks.noFill) callbacks.noFill();
                                if (callbacks.afterAd) callbacks.afterAd();
                                resolve();
                            }
                        });
                    });
                },

                showRewarded: function(placement, callbacks) {
                    callbacks = callbacks || {};
                    var sdk = window.Yes2SDK._sdk;
                    if (!sdk) {
                        if (callbacks.noFill) callbacks.noFill();
                        if (callbacks.afterAd) callbacks.afterAd();
                        return Promise.resolve();
                    }

                    if (callbacks.beforeAd) callbacks.beforeAd();

                    return new Promise(function(resolve) {
                        sdk.ad.requestAd('rewarded', {
                            adStarted: function() {
                                window.__y2.log('Rewarded ad started');
                            },
                            adFinished: function() {
                                window.__y2.log('Rewarded ad finished');
                                if (callbacks.adViewed) callbacks.adViewed();
                                if (callbacks.afterAd) callbacks.afterAd();
                                resolve();
                            },
                            adError: function(error) {
                                window.__y2.warn('Rewarded ad error:', error);
                                if (error && (error.code === 'unfilled' || error.code === 'adblock')) {
                                    if (callbacks.noFill) callbacks.noFill();
                                } else {
                                    if (callbacks.adDismissed) callbacks.adDismissed();
                                }
                                if (callbacks.afterAd) callbacks.afterAd();
                                resolve();
                            }
                        });
                    });
                },

                showBanner: function(position) {
                    var sdk = window.Yes2SDK._sdk;
                    if (!sdk || !sdk.banner) {
                        return Promise.reject({ code: 'NotInitialized', message: 'SDK not initialized' });
                    }
                    var containerId = 'yes2sdk-banner-' + position;
                    return sdk.banner.requestBanner({ id: containerId, width: 728, height: 90 });
                },

                hideBanner: function() {
                    var sdk = window.Yes2SDK._sdk;
                    if (sdk && sdk.banner) {
                        sdk.banner.clearAllBanners();
                    }
                },

                isAdBlocked: function() {
                    return false;
                }
            },

            // Analytics module - maps to CrazyGames gameplay events
            analytics: {
                logEvent: function(name, paramsJson) {
                    window.__y2.log('Event:', name, paramsJson);
                },
                logLevelStart: function(level) {
                    window.__y2.log('Level start:', level);
                    var sdk = window.Yes2SDK._sdk;
                    if (sdk && sdk.game) sdk.game.gameplayStart();
                },
                logLevelEnd: function(level, score, success) {
                    window.__y2.log('Level end:', level, score, success);
                    var sdk = window.Yes2SDK._sdk;
                    if (sdk && sdk.game) sdk.game.gameplayStop();
                },
                logScore: function(score, level) {
                    window.__y2.log('Score:', score, level);
                },
                logTutorialStart: function() {
                    window.__y2.log('Tutorial start');
                },
                logTutorialEnd: function() {
                    window.__y2.log('Tutorial end');
                },
                logPurchase: function(productId, price, currency) {
                    window.__y2.log('Purchase:', productId, price, currency);
                }
            },

            // Session module - maps to CrazyGames user/system info
            session: {
                _data: {},
                _systemInfo: null,

                _getSystemInfo: function() {
                    if (this._systemInfo) return this._systemInfo;
                    var sdk = window.Yes2SDK._sdk;
                    if (sdk && sdk.user && sdk.user.systemInfo) {
                        this._systemInfo = sdk.user.systemInfo;
                    }
                    return this._systemInfo || {};
                },

                getLocale: function() {
                    var info = this._getSystemInfo();
                    return (info && info.countryCode) || navigator.language || 'en';
                },

                getCountry: function() {
                    var info = this._getSystemInfo();
                    return (info && info.countryCode) || '';
                },

                getDevice: function() {
                    var info = this._getSystemInfo();
                    if (info && info.device && info.device.type) return info.device.type;
                    var ua = navigator.userAgent || '';
                    if (/tablet|ipad|playbook|silk/i.test(ua)) return 'tablet';
                    if (/mobile|iphone|ipod|android|blackberry|opera mini|iemobile/i.test(ua)) return 'mobile';
                    return 'desktop';
                },

                getOrientation: function() {
                    return window.innerWidth > window.innerHeight ? 'landscape' : 'portrait';
                },

                getTrafficSource: function() {
                    var sdk = window.Yes2SDK._sdk;
                    var source = {};
                    try {
                        if (sdk && sdk.game && sdk.game.getInviteParam) {
                            source.utm_source = sdk.game.getInviteParam('utm_source') || '';
                            source.utm_medium = sdk.game.getInviteParam('utm_medium') || '';
                            source.utm_campaign = sdk.game.getInviteParam('utm_campaign') || '';
                        }
                    } catch(e) {}
                    return JSON.stringify({ referrer: document.referrer || '', params: source });
                },

                getEntryPointData: function() {
                    var sdk = window.Yes2SDK._sdk;
                    var params = {};
                    try {
                        if (sdk && sdk.game && sdk.game.getInviteParam) {
                            var roomId = sdk.game.getInviteParam('roomId');
                            var inviterId = sdk.game.getInviteParam('inviterId');
                            if (roomId) params.roomId = roomId;
                            if (inviterId) params.inviterId = inviterId;
                        }
                    } catch(e) {}
                    return JSON.stringify(params);
                },

                setSessionData: function(dataJson) {
                    try {
                        this._data = JSON.parse(dataJson);
                    } catch(e) {
                        window.__y2.warn('Invalid session data JSON:', dataJson);
                    }
                },

                getEntryPointAsync: function() {
                    var sdk = window.Yes2SDK._sdk;
                    var source = 'crazygames';
                    try {
                        if (sdk && sdk.game && sdk.game.getInviteParam) {
                            source = sdk.game.getInviteParam('source') || 'crazygames';
                        }
                    } catch(e) {}
                    return Promise.resolve(source);
                }
            },

            // Player module - maps to CrazyGames user + data APIs
            player: {
                getPlayerAsync: function() {
                    var sdk = window.Yes2SDK._sdk;
                    if (!sdk || !sdk.user) {
                        return Promise.resolve({ id: 'anonymous', name: null, photo: null });
                    }
                    return sdk.user.getUser().then(function(user) {
                        return {
                            id: user.username || 'anonymous',
                            name: user.username || null,
                            photo: user.profilePictureUrl || null
                        };
                    }).catch(function() {
                        return { id: 'anonymous', name: null, photo: null };
                    });
                },

                getDataAsync: function(keysJson) {
                    var sdk = window.Yes2SDK._sdk;
                    if (!sdk || !sdk.data) {
                        return Promise.reject({ code: 'FeatureNotSupported', message: 'Data not available', context: 'player.getDataAsync' });
                    }
                    try {
                        var keys = JSON.parse(keysJson);
                        var result = {};
                        var promises = keys.map(function(key) {
                            return sdk.data.getItem(key).then(function(value) {
                                result[key] = value;
                            });
                        });
                        return Promise.all(promises).then(function() {
                            return JSON.stringify(result);
                        });
                    } catch(e) {
                        return Promise.reject({ code: 'InvalidParams', message: e.message, context: 'player.getDataAsync' });
                    }
                },

                setDataAsync: function(dataJson) {
                    var sdk = window.Yes2SDK._sdk;
                    if (!sdk || !sdk.data) {
                        return Promise.reject({ code: 'FeatureNotSupported', message: 'Data not available', context: 'player.setDataAsync' });
                    }
                    try {
                        var data = JSON.parse(dataJson);
                        var promises = Object.keys(data).map(function(key) {
                            return sdk.data.setItem(key, typeof data[key] === 'string' ? data[key] : JSON.stringify(data[key]));
                        });
                        return Promise.all(promises).then(function() { return; });
                    } catch(e) {
                        return Promise.reject({ code: 'InvalidParams', message: e.message, context: 'player.setDataAsync' });
                    }
                },

                flushDataAsync: function() {
                    // CrazyGames auto-flushes
                    return Promise.resolve();
                },

                getConnectedPlayersAsync: function() {
                    return Promise.reject({
                        code: 'FeatureNotSupported',
                        message: 'Connected players are not supported on CrazyGames',
                        context: 'player.getConnectedPlayersAsync'
                    });
                },

                getSignedPlayerInfoAsync: function(payload) {
                    return Promise.reject({
                        code: 'FeatureNotSupported',
                        message: 'Signed player info is not supported on CrazyGames',
                        context: 'player.getSignedPlayerInfoAsync'
                    });
                }
            },

            // Data module - PlayerPrefs-style save/load via CrazyGames SDK data API
            data: {
                getInt: function(key, defaultValue) {
                    var sdk = window.Yes2SDK._sdk;
                    if (!sdk || !sdk.data) return defaultValue;
                    try {
                        var val = sdk.data.getItem(key);
                        if (val === null || val === undefined) return defaultValue;
                        var parsed = parseInt(val, 10);
                        return isNaN(parsed) ? defaultValue : parsed;
                    } catch(e) { return defaultValue; }
                },

                setInt: function(key, value) {
                    var sdk = window.Yes2SDK._sdk;
                    if (sdk && sdk.data) sdk.data.setItem(key, value.toString());
                },

                getFloat: function(key, defaultValue) {
                    var sdk = window.Yes2SDK._sdk;
                    if (!sdk || !sdk.data) return defaultValue;
                    try {
                        var val = sdk.data.getItem(key);
                        if (val === null || val === undefined) return defaultValue;
                        var parsed = parseFloat(val);
                        return isNaN(parsed) ? defaultValue : parsed;
                    } catch(e) { return defaultValue; }
                },

                setFloat: function(key, value) {
                    var sdk = window.Yes2SDK._sdk;
                    if (sdk && sdk.data) sdk.data.setItem(key, value.toString());
                },

                getString: function(key, defaultValue) {
                    var sdk = window.Yes2SDK._sdk;
                    if (!sdk || !sdk.data) return defaultValue;
                    try {
                        var val = sdk.data.getItem(key);
                        return (val === null || val === undefined) ? defaultValue : val;
                    } catch(e) { return defaultValue; }
                },

                setString: function(key, value) {
                    var sdk = window.Yes2SDK._sdk;
                    if (sdk && sdk.data) sdk.data.setItem(key, value);
                },

                hasKey: function(key) {
                    var sdk = window.Yes2SDK._sdk;
                    if (!sdk || !sdk.data) return false;
                    try {
                        return sdk.data.getItem(key) !== null;
                    } catch(e) { return false; }
                },

                deleteKey: function(key) {
                    var sdk = window.Yes2SDK._sdk;
                    if (sdk && sdk.data) sdk.data.removeItem(key);
                },

                deleteAll: function() {
                    var sdk = window.Yes2SDK._sdk;
                    if (sdk && sdk.data) sdk.data.clear();
                }
            },

            // Auth module - CrazyGames user authentication
            auth: {
                isSupported: function() {
                    var sdk = window.Yes2SDK._sdk;
                    return !!(sdk && sdk.user);
                },

                getCurrentUserAsync: function() {
                    var sdk = window.Yes2SDK._sdk;
                    if (!sdk || !sdk.user) {
                        return Promise.resolve(null);
                    }
                    return sdk.user.getUser().then(function(user) {
                        if (!user) return null;
                        return {
                            id: user.username || '',
                            name: user.username || '',
                            photo: user.profilePictureUrl || '',
                            isAuthenticated: true
                        };
                    }).catch(function() {
                        return null;
                    });
                },

                signInAsync: function() {
                    var sdk = window.Yes2SDK._sdk;
                    if (!sdk || !sdk.user) {
                        return Promise.reject({ code: 'FeatureNotSupported', message: 'Auth not available', context: 'auth.signInAsync' });
                    }
                    return sdk.user.showAuthPrompt().then(function(user) {
                        if (!user) {
                            return Promise.reject({ code: 'UserCancelled', message: 'User cancelled auth prompt', context: 'auth.signInAsync' });
                        }
                        return {
                            id: user.username || '',
                            name: user.username || '',
                            photo: user.profilePictureUrl || '',
                            isAuthenticated: true
                        };
                    });
                },

                getTokenAsync: function() {
                    var sdk = window.Yes2SDK._sdk;
                    if (!sdk || !sdk.user) {
                        return Promise.reject({ code: 'FeatureNotSupported', message: 'Auth not available', context: 'auth.getTokenAsync' });
                    }
                    return sdk.user.getUserToken();
                },

                showAccountLinkPromptAsync: function() {
                    var sdk = window.Yes2SDK._sdk;
                    if (!sdk || !sdk.user || !sdk.user.showAccountLinkPrompt) {
                        return Promise.reject({ code: 'FeatureNotSupported', message: 'Account link not available', context: 'auth.showAccountLinkPromptAsync' });
                    }
                    return sdk.user.showAccountLinkPrompt();
                }
            },

            // Game module - gameplay lifecycle, invite, settings, clipboard
            game: {
                gameplayStart: function() {
                    var sdk = window.Yes2SDK._sdk;
                    if (sdk && sdk.game) sdk.game.gameplayStart();
                },

                gameplayStop: function() {
                    var sdk = window.Yes2SDK._sdk;
                    if (sdk && sdk.game) sdk.game.gameplayStop();
                },

                happyTime: function() {
                    var sdk = window.Yes2SDK._sdk;
                    if (sdk && sdk.game) sdk.game.happytime();
                },

                inviteLink: function(paramsJson) {
                    var sdk = window.Yes2SDK._sdk;
                    if (!sdk || !sdk.game || !sdk.game.inviteLink) {
                        return Promise.reject({ code: 'FeatureNotSupported', message: 'Invite link not available', context: 'game.inviteLink' });
                    }
                    var params = {};
                    try { params = JSON.parse(paramsJson); } catch(e) {}
                    return sdk.game.inviteLink(params);
                },

                getInviteParam: function(key) {
                    var sdk = window.Yes2SDK._sdk;
                    if (!sdk || !sdk.game || !sdk.game.getInviteParam) return '';
                    return sdk.game.getInviteParam(key) || '';
                },

                showInviteButton: function(paramsJson) {
                    var sdk = window.Yes2SDK._sdk;
                    if (!sdk || !sdk.game || !sdk.game.showInviteButton) return;
                    var params = {};
                    try { params = JSON.parse(paramsJson); } catch(e) {}
                    sdk.game.showInviteButton(params);
                },

                hideInviteButton: function() {
                    var sdk = window.Yes2SDK._sdk;
                    if (sdk && sdk.game && sdk.game.hideInviteButton) sdk.game.hideInviteButton();
                },

                getSettings: function() {
                    var sdk = window.Yes2SDK._sdk;
                    if (!sdk || !sdk.game) return JSON.stringify({ disableChat: false, muteAudio: false });
                    return JSON.stringify({
                        disableChat: !!(sdk.game.disableChat),
                        muteAudio: !!(sdk.game.muteAudio)
                    });
                },

                addSettingsChangeListener: function(callback) {
                    window.Yes2SDK._settingsListeners.push(callback);
                    var sdk = window.Yes2SDK._sdk;
                    if (sdk && sdk.game && sdk.game.addEventListener) {
                        sdk.game.addEventListener('settingsChange', function() {
                            var settings = JSON.stringify({
                                disableChat: !!(sdk.game.disableChat),
                                muteAudio: !!(sdk.game.muteAudio)
                            });
                            callback(settings);
                        });
                    }
                },

                copyToClipboard: function(text) {
                    var sdk = window.Yes2SDK._sdk;
                    if (sdk && sdk.game && sdk.game.copyToClipboard) {
                        sdk.game.copyToClipboard(text);
                    }
                }
            },

            // Banners module - multi-size banner support via CrazyGames banner API
            banners: {
                _sizes: {
                    'Leaderboard_728x90': { width: 728, height: 90 },
                    'Medium_300x250': { width: 300, height: 250 },
                    'Mobile_320x50': { width: 320, height: 50 },
                    'Main_468x60': { width: 468, height: 60 },
                    'Large_Mobile_320x100': { width: 320, height: 100 }
                },

                showBanner: function(id, size, x, y) {
                    var sdk = window.Yes2SDK._sdk;
                    if (!sdk || !sdk.banner) {
                        return Promise.reject({ code: 'FeatureNotSupported', message: 'Banners not available', context: 'banners.showBanner' });
                    }
                    var dims = this._sizes[size] || { width: 728, height: 90 };
                    return sdk.banner.requestBanner({ id: id, width: dims.width, height: dims.height });
                },

                hideBanner: function(id) {
                    var sdk = window.Yes2SDK._sdk;
                    if (sdk && sdk.banner) sdk.banner.clearBanner(id);
                },

                hideAllBanners: function() {
                    var sdk = window.Yes2SDK._sdk;
                    if (sdk && sdk.banner) sdk.banner.clearAllBanners();
                },

                refreshBanners: function() {
                    var sdk = window.Yes2SDK._sdk;
                    if (sdk && sdk.banner && sdk.banner.refreshBanners) sdk.banner.refreshBanners();
                }
            },

            // Friends module - list friends via CrazyGames user API
            friends: {
                listFriendsAsync: function(page, size) {
                    var sdk = window.Yes2SDK._sdk;
                    if (!sdk || !sdk.user || !sdk.user.getMyFriends) {
                        return Promise.reject({ code: 'FeatureNotSupported', message: 'Friends not available', context: 'friends.listFriendsAsync' });
                    }
                    return sdk.user.getMyFriends().then(function(friends) {
                        var start = page * size;
                        var sliced = (friends || []).slice(start, start + size);
                        var mapped = sliced.map(function(f) {
                            return {
                                id: f.id || f.username || '',
                                username: f.username || '',
                                profilePictureUrl: f.profilePictureUrl || ''
                            };
                        });
                        return {
                            friends: mapped,
                            page: page,
                            size: size,
                            hasMore: start + size < (friends || []).length,
                            total: (friends || []).length
                        };
                    });
                }
            },

            // Score module - submit scores via CrazyGames user API
            score: {
                addScore: function(score) {
                    var sdk = window.Yes2SDK._sdk;
                    if (sdk && sdk.user && sdk.user.addScore) {
                        sdk.user.addScore(score);
                    } else {
                        window.__y2.log('Score:', score);
                    }
                },

                submitScore: function(encryptedScore) {
                    var sdk = window.Yes2SDK._sdk;
                    if (sdk && sdk.user && sdk.user.submitScore) {
                        sdk.user.submitScore(encryptedScore);
                    } else {
                        window.__y2.log('SubmitScore:', encryptedScore);
                    }
                }
            }
        };

        window.__y2.log('Yes2SDK wrapper loaded via postset, platform:', window.Yes2SDK._platform);
    }

});
