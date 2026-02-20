mergeInto(LibraryManager.library, {

    Yes2SDK_Score_AddScoreJS: function(score) {
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.score !== 'undefined') {
            window.Yes2SDK.score.addScore(score);
        }
    },

    Yes2SDK_Score_SubmitScoreJS: function(encryptedScorePtr) {
        var encryptedScore = UTF8ToString(encryptedScorePtr);
        if (typeof window.Yes2SDK !== 'undefined' && typeof window.Yes2SDK.score !== 'undefined') {
            window.Yes2SDK.score.submitScore(encryptedScore);
        }
    }

});
