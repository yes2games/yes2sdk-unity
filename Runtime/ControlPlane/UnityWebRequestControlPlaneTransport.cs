using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Yes2SDK
{
    internal sealed class UnityWebRequestControlPlaneTransport : IControlPlaneTransport
    {
        public async Task<ControlPlaneTransportResponse> GetAsync(ControlPlaneTransportRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var url = request.BypassCache ? CacheBypassUrl(request.Url, Guid.NewGuid().ToString("N")) : request.Url;
            using (var web = UnityWebRequest.Get(url))
            {
                var elapsed = Stopwatch.StartNew();
                var operation = web.SendWebRequest();
                while (!operation.isDone)
                {
                    if (token.IsCancellationRequested)
                    {
                        web.Abort();
                        token.ThrowIfCancellationRequested();
                    }
                    if (elapsed.Elapsed >= request.Timeout)
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
                        return elapsed.Elapsed >= request.Timeout
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

        public static string CacheBypassUrl(string url, string nonce)
        {
            return url + (url.IndexOf('?') < 0 ? "?" : "&") + "cb=" + nonce;
        }
    }
}
