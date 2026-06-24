mergeInto(LibraryManager.library, {

    Yes2SDK_Review_IsSupportedJS__deps: ['$__y2h'],
    Yes2SDK_Review_IsSupportedJS: function() {
        return __y2h.has('review') && window.Yes2SDK.review.isSupported() ? 1 : 0;
    },

    Yes2SDK_Review_CanReviewAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Review_CanReviewAsyncJS: function() {
        if (!__y2h.has('review')) {
            __y2h.sendError('OnCanReviewError', 'NotInitialized', 'Yes2SDK Review module not loaded', 'Yes2SDK.Review.CanReviewAsync');
            return;
        }

        window.Yes2SDK.review.canReviewAsync()
            .then(function(eligibility) {
                SendMessage('Bridge', 'OnCanReviewSuccess', JSON.stringify(eligibility || { canReview: false }));
            })
            .catch(__y2h.handleCatch('OnCanReviewError', 'CanReview failed', 'Yes2SDK.Review.CanReviewAsync'));
    },

    Yes2SDK_Review_RequestReviewAsyncJS__deps: ['$__y2h'],
    Yes2SDK_Review_RequestReviewAsyncJS: function() {
        if (!__y2h.has('review')) {
            __y2h.sendError('OnRequestReviewError', 'NotInitialized', 'Yes2SDK Review module not loaded', 'Yes2SDK.Review.RequestReviewAsync');
            return;
        }

        window.Yes2SDK.review.requestReviewAsync()
            .then(function(result) {
                SendMessage('Bridge', 'OnRequestReviewSuccess', JSON.stringify(result || { feedbackSent: false }));
            })
            .catch(__y2h.handleCatch('OnRequestReviewError', 'RequestReview failed', 'Yes2SDK.Review.RequestReviewAsync'));
    }

});
