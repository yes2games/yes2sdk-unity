mergeInto(LibraryManager.library, {

    Yes2SDK_Auth_IsSupportedJS__deps: ['$__y2h'],
    Yes2SDK_Auth_IsSupportedJS: function() {
        if (!__y2h.has('auth')) return false;
        try { return window.Yes2SDK.auth.isSupported() ? 1 : 0; }
        catch (e) { return 0; }
    },

    Yes2SDK_Auth_GetCurrentUserAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Auth_GetCurrentUserAsyncJS: function() {
        if (!__y2h.has('auth')) {
            __y2h.sendError('OnGetCurrentUserError', 'NotInitialized', 'Yes2SDK Auth module not loaded', 'Yes2SDK.Auth.GetCurrentUserAsync');
            return;
        }

        window.Yes2SDK.auth.getCurrentUserAsync()
            .then(function(user) {
                SendMessage('Bridge', 'OnGetCurrentUserSuccess', user ? JSON.stringify(user) : 'null');
            })
            .catch(__y2h.handleCatch('OnGetCurrentUserError', 'GetCurrentUser failed', 'Yes2SDK.Auth.GetCurrentUserAsync'));
    },

    Yes2SDK_Auth_SignInAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Auth_SignInAsyncJS: function() {
        if (!__y2h.has('auth')) {
            __y2h.sendError('OnSignInError', 'NotInitialized', 'Yes2SDK Auth module not loaded', 'Yes2SDK.Auth.SignInAsync');
            return;
        }

        window.Yes2SDK.auth.signInAsync()
            .then(function(user) {
                SendMessage('Bridge', 'OnSignInSuccess', JSON.stringify(user));
            })
            .catch(__y2h.handleCatch('OnSignInError', 'SignIn failed', 'Yes2SDK.Auth.SignInAsync'));
    },

    Yes2SDK_Auth_GetTokenAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Auth_GetTokenAsyncJS: function() {
        if (!__y2h.has('auth')) {
            __y2h.sendError('OnGetTokenError', 'NotInitialized', 'Yes2SDK Auth module not loaded', 'Yes2SDK.Auth.GetTokenAsync');
            return;
        }

        window.Yes2SDK.auth.getTokenAsync()
            .then(function(token) {
                SendMessage('Bridge', 'OnGetTokenSuccess', token || '');
            })
            .catch(__y2h.handleCatch('OnGetTokenError', 'GetToken failed', 'Yes2SDK.Auth.GetTokenAsync'));
    },

    Yes2SDK_Auth_ShowAccountLinkPromptAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Auth_ShowAccountLinkPromptAsyncJS: function() {
        if (!__y2h.has('auth')) {
            __y2h.sendError('OnAccountLinkError', 'NotInitialized', 'Yes2SDK Auth module not loaded', 'Yes2SDK.Auth.ShowAccountLinkPromptAsync');
            return;
        }

        window.Yes2SDK.auth.showAccountLinkPromptAsync()
            .then(function(result) {
                SendMessage('Bridge', 'OnAccountLinkSuccess', result ? 'true' : 'false');
            })
            .catch(__y2h.handleCatch('OnAccountLinkError', 'AccountLink failed', 'Yes2SDK.Auth.ShowAccountLinkPromptAsync'));
    },

    // ---- registration prompt and isAuthenticated ----
    // Core's showRegistrationPrompt and its handle actions are synchronous and
    // throw directly, so every entry point below catches: a throw into wasm
    // stops the game loop. Open handles are kept by the C# prompt id.
    $__y2reg: {
        prompts: {},
        warn: function(message, error) {
            try {
                if (window.__y2 && window.__y2.warn) window.__y2.warn('[Auth] ' + message, error);
            } catch (e) {}
        }
    },

    // Returns '' on success, or error JSON {code, message, context} on any failure.
    Yes2SDK_Auth_ShowRegistrationPromptJS__deps: ['$__y2h', '$__y2reg'],
    Yes2SDK_Auth_ShowRegistrationPromptJS: function(promptId, optionsJsonPtr) {
        var context = 'Yes2SDK.Auth.ShowRegistrationPrompt';
        try {
            if (!__y2h.has('auth')) {
                return __y2h.returnStr(JSON.stringify({ code: 'NotInitialized', message: 'Yes2SDK Auth module not loaded', context: context }));
            }
            if (typeof window.Yes2SDK.auth.showRegistrationPrompt !== 'function') {
                return __y2h.returnStr(JSON.stringify({ code: 'FeatureNotSupported', message: 'Registration prompt is not supported on the current platform', context: context }));
            }
            var raw = UTF8ToString(optionsJsonPtr);
            var parsed = raw ? JSON.parse(raw) : {};
            var options = {};
            if (parsed && typeof parsed === 'object') {
                if (parsed.theme !== undefined && parsed.theme !== null) options.theme = parsed.theme;
                if (parsed.data !== undefined && parsed.data !== null) options.data = parsed.data;
                if (typeof parsed.message === 'string') options.message = parsed.message;
            }
            var closed = false;
            options.onClose = function() {
                if (closed) return;
                closed = true;
                delete __y2reg.prompts[promptId];
                try {
                    SendMessage('Bridge', 'OnRegistrationPromptClose', String(promptId));
                } catch (e) {
                    __y2reg.warn('registration prompt close could not reach the game', e);
                }
            };
            var handle = window.Yes2SDK.auth.showRegistrationPrompt(options);
            if (!closed) __y2reg.prompts[promptId] = handle;
            return __y2h.returnStr('');
        } catch (error) {
            return __y2h.returnStr(JSON.stringify({
                code: (error && error.code) || 'Unknown',
                message: (error && error.message) || 'Registration prompt failed',
                context: context
            }));
        }
    },

    Yes2SDK_Auth_RegistrationPromptLoginJS__deps: ['$__y2reg'],
    Yes2SDK_Auth_RegistrationPromptLoginJS: function(promptId) {
        try {
            var handle = __y2reg.prompts[promptId];
            if (!handle || typeof handle.login !== 'function') {
                __y2reg.warn('registration prompt ' + promptId + ' is not open; login ignored');
                return;
            }
            handle.login();
        } catch (error) {
            __y2reg.warn('registration prompt login failed', error);
        }
    },

    Yes2SDK_Auth_RegistrationPromptCloseJS__deps: ['$__y2reg'],
    Yes2SDK_Auth_RegistrationPromptCloseJS: function(promptId) {
        try {
            var handle = __y2reg.prompts[promptId];
            delete __y2reg.prompts[promptId];
            if (!handle || typeof handle.close !== 'function') {
                __y2reg.warn('registration prompt ' + promptId + ' is not open; close ignored');
                return;
            }
            handle.close();
        } catch (error) {
            __y2reg.warn('registration prompt close failed', error);
        }
    },

    // False before init, when the method is missing, or on any error.
    Yes2SDK_Auth_IsAuthenticatedJS__deps: ['$__y2h'],
    Yes2SDK_Auth_IsAuthenticatedJS: function() {
        try {
            if (!__y2h.has('auth') || typeof window.Yes2SDK.auth.isAuthenticated !== 'function') return 0;
            return window.Yes2SDK.auth.isAuthenticated() ? 1 : 0;
        } catch (error) {
            return 0;
        }
    }
    // ---- end registration prompt and isAuthenticated ----

});
