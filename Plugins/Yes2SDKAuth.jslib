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
    }

});
