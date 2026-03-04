mergeInto(LibraryManager.library, {

    Yes2SDK_Friends_ListFriendsAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Friends_ListFriendsAsyncJS: function(page, size) {
        if (!__y2h.has('friends')) {
            __y2h.sendError('OnListFriendsError', 'NotInitialized', 'Yes2SDK Friends module not loaded', 'Yes2SDK.Friends.ListFriendsAsync');
            return;
        }

        window.Yes2SDK.friends.listFriendsAsync(page, size)
            .then(function(result) {
                SendMessage('Bridge', 'OnListFriendsSuccess', JSON.stringify(result));
            })
            .catch(__y2h.handleCatch('OnListFriendsError', 'ListFriends failed', 'Yes2SDK.Friends.ListFriendsAsync'));
    }

});
