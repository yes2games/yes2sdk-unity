mergeInto(LibraryManager.library, {

    // Every notifications response is sent as "<requestId>|<payload>" so
    // Yes2SDKNotifications.cs can hand it to the call that made it, not to
    // whichever call came last.
    $__y2notif: {
        send: function(callback, requestId, payload) {
            SendMessage('Bridge', callback, requestId + '|' + payload);
        },
        sendError: function(callback, requestId, code, message, context) {
            __y2notif.send(callback, requestId, JSON.stringify({ code: code, message: message, context: context }));
        },
        handleCatch: function(callback, requestId, defaultMessage, context) {
            return function(error) {
                __y2notif.sendError(callback, requestId,
                    (error && error.code) || 'Unknown',
                    (error && error.message) || defaultMessage,
                    context);
            };
        },
        // Reports a missing SDK or module. Returns true when the call can go ahead.
        ready: function(callback, requestId, context) {
            if (!__y2h.has()) {
                __y2notif.sendError(callback, requestId, 'NotInitialized', 'Yes2SDK not loaded', context);
                return false;
            }
            if (!__y2h.has('notifications')) {
                __y2notif.sendError(callback, requestId, 'FEATURE_NOT_SUPPORTED',
                    'Notifications are not supported on the current platform', context);
                return false;
            }
            return true;
        }
    },
    $__y2notif__deps: ['$__y2h'],

    Yes2SDK_Notifications_IsSupportedJS__deps: ['$__y2h'],
    Yes2SDK_Notifications_IsSupportedJS: function() {
        try {
            if (!__y2h.has('notifications')) return 0;
            return window.Yes2SDK.notifications.isSupported() ? 1 : 0;
        } catch (e) {
            return 0;
        }
    },

    Yes2SDK_Notifications_ScheduleAsyncJS__deps: ['$__y2notif'],
    Yes2SDK_Notifications_ScheduleAsyncJS: function(requestId, optionsJsonPtr) {
        var context = 'Yes2SDK.Notifications.ScheduleAsync';
        var onError = __y2notif.handleCatch('OnNotificationScheduleError', requestId, 'Notification schedule failed', context);
        try {
            if (!__y2notif.ready('OnNotificationScheduleError', requestId, context)) return;

            var options;
            try {
                options = JSON.parse(UTF8ToString(optionsJsonPtr));
            } catch (parseError) {
                __y2notif.sendError('OnNotificationScheduleError', requestId, 'INVALID_PARAM',
                    'Notification options are not valid JSON', context);
                return;
            }

            window.Yes2SDK.notifications.scheduleAsync(options)
                .then(function(notification) {
                    __y2notif.send('OnNotificationScheduleSuccess', requestId, JSON.stringify(notification));
                })
                .catch(onError);
        } catch (e) {
            onError(e);
        }
    },

    Yes2SDK_Notifications_CancelAsyncJS__deps: ['$__y2notif'],
    Yes2SDK_Notifications_CancelAsyncJS: function(requestId, notificationIdPtr) {
        var context = 'Yes2SDK.Notifications.CancelAsync';
        var onError = __y2notif.handleCatch('OnNotificationCancelError', requestId, 'Notification cancel failed', context);
        try {
            if (!__y2notif.ready('OnNotificationCancelError', requestId, context)) return;

            var notificationId = UTF8ToString(notificationIdPtr);
            window.Yes2SDK.notifications.cancelAsync(notificationId)
                .then(function() {
                    __y2notif.send('OnNotificationCancelSuccess', requestId, '');
                })
                .catch(onError);
        } catch (e) {
            onError(e);
        }
    },

    Yes2SDK_Notifications_CancelAllAsyncJS__deps: ['$__y2notif'],
    Yes2SDK_Notifications_CancelAllAsyncJS: function(requestId) {
        var context = 'Yes2SDK.Notifications.CancelAllAsync';
        var onError = __y2notif.handleCatch('OnNotificationCancelAllError', requestId, 'Notification cancel all failed', context);
        try {
            if (!__y2notif.ready('OnNotificationCancelAllError', requestId, context)) return;

            window.Yes2SDK.notifications.cancelAllAsync()
                .then(function() {
                    __y2notif.send('OnNotificationCancelAllSuccess', requestId, '');
                })
                .catch(onError);
        } catch (e) {
            onError(e);
        }
    }

});
