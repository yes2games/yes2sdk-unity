using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Yes2SDK
{
    internal sealed class UnityWebRequestControlPlaneTransport : IControlPlaneTransport
    {
        private readonly Func<TimeSpan> _clock;

        public UnityWebRequestControlPlaneTransport(Func<TimeSpan> clock)
        {
            _clock = clock;
        }

        public static Func<TimeSpan> MonotonicClock()
        {
            var elapsed = Stopwatch.StartNew();
            return () => elapsed.Elapsed;
        }

        public async Task<ControlPlaneTransportResponse> GetAsync(ControlPlaneTransportRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (var web = UnityWebRequest.Get(request.Url))
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                if (request.BypassCache)
                {
                    web.SetRequestHeader("Cache-Control", "no-cache");
                }
#endif
                var deadline = _clock() + request.Timeout;
                var operation = web.SendWebRequest();
                while (!operation.isDone)
                {
                    if (token.IsCancellationRequested)
                    {
                        web.Abort();
                        token.ThrowIfCancellationRequested();
                    }
                    if (_clock() >= deadline)
                    {
                        web.Abort();
                        return ControlPlaneTransportResponse.TimedOut();
                    }
                    if (web.downloadedBytes > (ulong)request.MaxBytes)
                    {
                        var status = web.responseCode;
                        web.Abort();
                        return ControlPlaneTransportResponse.TooLarge(status);
                    }
                    await Task.Yield();
                }
                token.ThrowIfCancellationRequested();

                switch (web.result)
                {
                    case UnityWebRequest.Result.Success:
                        var body = web.downloadHandler.data;
                        return body != null && body.LongLength > request.MaxBytes
                            ? ControlPlaneTransportResponse.TooLarge(web.responseCode)
                            : ControlPlaneTransportResponse.Completed(web.responseCode, body);
                    case UnityWebRequest.Result.ProtocolError:
                        return ControlPlaneTransportResponse.Completed(web.responseCode, null);
                    default:
                        return _clock() >= deadline
                            ? ControlPlaneTransportResponse.TimedOut()
                            : ControlPlaneTransportResponse.NetworkUnavailable(web.error);
                }
            }
        }

        public static async Task DelayAsync(TimeSpan delay, CancellationToken token)
        {
            var elapsed = Stopwatch.StartNew();
            while (elapsed.Elapsed < delay)
            {
                token.ThrowIfCancellationRequested();
                await Task.Yield();
            }
            token.ThrowIfCancellationRequested();
        }
    }
}
