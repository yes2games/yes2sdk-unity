mergeInto(LibraryManager.library, {

    Yes2SDK_Auth_IsSupportedJS: function() {
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.auth !== 'undefined') {
            return window.Yes2SDK.auth.isSupported() ? 1 : 0;
        }
        return 0;
    },

    Yes2SDK_Auth_GetCurrentUserAsyncJS: function() {
        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.auth === 'undefined') {
            var errorJson = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Auth module not loaded',
                context: 'Yes2SDK.Auth.GetCurrentUserAsync'
            });
            SendMessage('Bridge', 'OnGetCurrentUserError', errorJson);
            return;
        }

        window.Yes2SDK.auth.getCurrentUserAsync()
            .then(function(user) {
                SendMessage('Bridge', 'OnGetCurrentUserSuccess', user ? JSON.stringify(user) : 'null');
            })
            .catch(function(error) {
                var errorJson = JSON.stringify({
                    code: (error && error.code) || 'Unknown',
                    message: (error && error.message) || 'GetCurrentUser failed',
                    context: 'Yes2SDK.Auth.GetCurrentUserAsync'
                });
                SendMessage('Bridge', 'OnGetCurrentUserError', errorJson);
            });
    },

    Yes2SDK_Auth_SignInAsyncJS: function() {
        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.auth === 'undefined') {
            var errorJson = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Auth module not loaded',
                context: 'Yes2SDK.Auth.SignInAsync'
            });
            SendMessage('Bridge', 'OnSignInError', errorJson);
            return;
        }

        window.Yes2SDK.auth.signInAsync()
            .then(function(user) {
                SendMessage('Bridge', 'OnSignInSuccess', JSON.stringify(user));
            })
            .catch(function(error) {
                var errorJson = JSON.stringify({
                    code: (error && error.code) || 'Unknown',
                    message: (error && error.message) || 'SignIn failed',
                    context: 'Yes2SDK.Auth.SignInAsync'
                });
                SendMessage('Bridge', 'OnSignInError', errorJson);
            });
    },

    Yes2SDK_Auth_GetTokenAsyncJS: function() {
        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.auth === 'undefined') {
            var errorJson = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Auth module not loaded',
                context: 'Yes2SDK.Auth.GetTokenAsync'
            });
            SendMessage('Bridge', 'OnGetTokenError', errorJson);
            return;
        }

        window.Yes2SDK.auth.getTokenAsync()
            .then(function(token) {
                SendMessage('Bridge', 'OnGetTokenSuccess', token || '');
            })
            .catch(function(error) {
                var errorJson = JSON.stringify({
                    code: (error && error.code) || 'Unknown',
                    message: (error && error.message) || 'GetToken failed',
                    context: 'Yes2SDK.Auth.GetTokenAsync'
                });
                SendMessage('Bridge', 'OnGetTokenError', errorJson);
            });
    },

    Yes2SDK_Auth_ShowAccountLinkPromptAsyncJS: function() {
        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.auth === 'undefined') {
            var errorJson = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Auth module not loaded',
                context: 'Yes2SDK.Auth.ShowAccountLinkPromptAsync'
            });
            SendMessage('Bridge', 'OnAccountLinkError', errorJson);
            return;
        }

        window.Yes2SDK.auth.showAccountLinkPromptAsync()
            .then(function(result) {
                SendMessage('Bridge', 'OnAccountLinkSuccess', result ? 'true' : 'false');
            })
            .catch(function(error) {
                var errorJson = JSON.stringify({
                    code: (error && error.code) || 'Unknown',
                    message: (error && error.message) || 'AccountLink failed',
                    context: 'Yes2SDK.Auth.ShowAccountLinkPromptAsync'
                });
                SendMessage('Bridge', 'OnAccountLinkError', errorJson);
            });
    }

});
