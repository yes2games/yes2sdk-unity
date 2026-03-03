mergeInto(LibraryManager.library, {

    Yes2SDK_Score_AddScoreJS__deps: ['$__y2h'],
    Yes2SDK_Score_AddScoreJS: function(score) {
        if (__y2h.has('score')) window.Yes2SDK.score.addScore(score);
    },

    Yes2SDK_Score_SubmitScoreJS__deps: ['$__y2h'],
    Yes2SDK_Score_SubmitScoreJS: function(encryptedScorePtr) {
        var encryptedScore = UTF8ToString(encryptedScorePtr);
        if (__y2h.has('score')) window.Yes2SDK.score.submitScore(encryptedScore);
    }

});
