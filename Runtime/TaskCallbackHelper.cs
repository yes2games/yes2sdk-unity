using System;
using System.Threading;
using System.Threading.Tasks;

namespace Yes2SDK
{
    /// <summary>
    /// Internal helper that converts callback-based SDK methods (the original
    /// signature shape) into Task-returning overloads. Cancellation marks the
    /// returned Task as cancelled — the underlying platform call may still
    /// run, but its result is dropped.
    /// </summary>
    internal static class TaskCallbackHelper
    {
        public static Task<T> ToTask<T>(
            Action<Action<T>, Action<Error>> invoke,
            CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<T>();
            CancellationTokenRegistration registration = default;
            if (cancellationToken.CanBeCanceled)
            {
                registration = cancellationToken.Register(
                    () => tcs.TrySetCanceled(cancellationToken));
            }

            invoke(
                result =>
                {
                    registration.Dispose();
                    tcs.TrySetResult(result);
                },
                error =>
                {
                    registration.Dispose();
                    tcs.TrySetException(new Yes2SDKException(error));
                });

            return tcs.Task;
        }

        public static Task ToTask(
            Action<Action, Action<Error>> invoke,
            CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<object>();
            CancellationTokenRegistration registration = default;
            if (cancellationToken.CanBeCanceled)
            {
                registration = cancellationToken.Register(
                    () => tcs.TrySetCanceled(cancellationToken));
            }

            invoke(
                () =>
                {
                    registration.Dispose();
                    tcs.TrySetResult(null);
                },
                error =>
                {
                    registration.Dispose();
                    tcs.TrySetException(new Yes2SDKException(error));
                });

            return tcs.Task;
        }
    }
}
