mergeInto(LibraryManager.library, {

    Yes2SDK_Friends_ListFriendsAsyncJS: function(page, size) {
        if (typeof window.Yes2SDK === 'undefined' || typeof window.Yes2SDK.friends === 'undefined') {
            var errorJson = JSON.stringify({
                code: 'NotInitialized',
                message: 'Yes2SDK Friends module not loaded',
                context: 'Yes2SDK.Friends.ListFriendsAsync'
            });
            SendMessage('Bridge', 'OnListFriendsError', errorJson);
            return;
        }

        window.Yes2SDK.friends.listFriendsAsync(page, size)
            .then(function(result) {
                SendMessage('Bridge', 'OnListFriendsSuccess', JSON.stringify(result));
            })
            .catch(function(error) {
                var errorJson = JSON.stringify({
                    code: (error && error.code) || 'Unknown',
                    message: (error && error.message) || 'ListFriends failed',
                    context: 'Yes2SDK.Friends.ListFriendsAsync'
                });
                SendMessage('Bridge', 'OnListFriendsError', errorJson);
            });
    }

});
