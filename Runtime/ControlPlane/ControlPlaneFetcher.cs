using System;
using System.Threading;
using System.Threading.Tasks;

namespace Yes2SDK
{
    internal sealed class ControlPlaneFetcher
    {
        public const int Attempts = 2;
        public const int MinRetryDelayMilliseconds = 250;
        public const int MaxRetryDelayMilliseconds = 500;
        public static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(5);

        private readonly IControlPlaneTransport _transport;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly Random _random;

        public ControlPlaneFetcher(IControlPlaneTransport transport, Func<TimeSpan, CancellationToken, Task> delay, Random random)
        {
            _transport = transport;
            _delay = delay;
            _random = random;
        }

        public Task<ControlPlaneResult<ControlPlaneBootstrap>> FetchBootstrapAsync(string url, string gameKey, string environmentKey, CancellationToken token)
        {
            return FetchAsync(
                new ControlPlaneTransportRequest(url, AttemptTimeout, ControlPlaneDocuments.MaxDocumentBytes, true),
                ControlPlaneErrorCategory.BootstrapHttpFailed,
                ControlPlaneErrorCategory.BootstrapInvalid,
                body => ControlPlaneDocuments.ReadBootstrap(body, gameKey, environmentKey),
                token);
        }

        public Task<ControlPlaneResult<ControlPlaneSnapshot>> FetchSnapshotAsync(ControlPlaneBootstrap bootstrap, CancellationToken token)
        {
            return FetchAsync(
                new ControlPlaneTransportRequest(bootstrap.SnapshotUrl, AttemptTimeout, ControlPlaneDocuments.MaxDocumentBytes, false),
                ControlPlaneErrorCategory.SnapshotHttpFailed,
                ControlPlaneErrorCategory.SnapshotInvalid,
                body => ControlPlaneDocuments.ReadSnapshot(body, bootstrap),
                token);
        }

        private async Task<ControlPlaneResult<T>> FetchAsync<T>(
            ControlPlaneTransportRequest request,
            ControlPlaneErrorCategory httpFailed,
            ControlPlaneErrorCategory invalid,
            Func<byte[], ControlPlaneResult<T>> read,
            CancellationToken token) where T : class
        {
            try
            {
                for (var attempt = 1; ; attempt++)
                {
                    if (attempt > 1)
                    {
                        await _delay(TimeSpan.FromMilliseconds(_random.Next(MinRetryDelayMilliseconds, MaxRetryDelayMilliseconds + 1)), token);
                    }
                    token.ThrowIfCancellationRequested();

                    ControlPlaneTransportResponse response;
                    try
                    {
                        response = await _transport.GetAsync(request, token);
                    }
                    catch (Exception e) when (!(e is OperationCanceledException && token.IsCancellationRequested))
                    {
                        response = ControlPlaneTransportResponse.NetworkUnavailable(e.GetType().Name + ": " + e.Message);
                    }
                    token.ThrowIfCancellationRequested();

                    ControlPlaneErrorCategory error;
                    string detail;
                    bool retryable;
                    switch (response.Status)
                    {
                        case ControlPlaneTransportStatus.NetworkUnavailable:
                            error = ControlPlaneErrorCategory.NetworkUnavailable;
                            detail = response.Detail;
                            retryable = true;
                            break;
                        case ControlPlaneTransportStatus.TimedOut:
                            error = ControlPlaneErrorCategory.Timeout;
                            detail = "no response within " + request.Timeout.TotalSeconds + " s";
                            retryable = true;
                            break;
                        default:
                            if (response.HttpStatus < 200 || response.HttpStatus > 299)
                            {
                                error = httpFailed;
                                detail = "HTTP " + response.HttpStatus;
                                retryable = response.HttpStatus == 408 || response.HttpStatus == 429 || (response.HttpStatus >= 500 && response.HttpStatus <= 599);
                                break;
                            }
                            if (response.Status == ControlPlaneTransportStatus.TooLarge)
                            {
                                return ControlPlaneResult<T>.Failure(invalid, "body exceeds " + request.MaxBytes + " bytes");
                            }
                            return read(response.Body);
                    }

                    if (!retryable || attempt >= Attempts)
                    {
                        return ControlPlaneResult<T>.Failure(error, detail);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return ControlPlaneResult<T>.Failure(ControlPlaneErrorCategory.Cancelled, null);
            }
        }
    }
}
