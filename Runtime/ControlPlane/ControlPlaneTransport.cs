using System;
using System.Threading;
using System.Threading.Tasks;

namespace Yes2SDK
{
    internal interface IControlPlaneTransport
    {
        Task<ControlPlaneTransportResponse> GetAsync(ControlPlaneTransportRequest request, CancellationToken token);
    }

    internal sealed class ControlPlaneTransportRequest
    {
        public ControlPlaneTransportRequest(string url, TimeSpan timeout, long maxBytes, bool bypassCache)
        {
            Url = url;
            Timeout = timeout;
            MaxBytes = maxBytes;
            BypassCache = bypassCache;
        }

        public string Url { get; }
        public TimeSpan Timeout { get; }
        public long MaxBytes { get; }
        public bool BypassCache { get; }
    }

    internal enum ControlPlaneTransportStatus
    {
        Completed,
        NetworkUnavailable,
        TimedOut,
        TooLarge
    }

    internal sealed class ControlPlaneTransportResponse
    {
        private ControlPlaneTransportResponse(ControlPlaneTransportStatus status, long httpStatus, byte[] body, string detail)
        {
            Status = status;
            HttpStatus = httpStatus;
            Body = body;
            Detail = detail;
        }

        public ControlPlaneTransportStatus Status { get; }
        public long HttpStatus { get; }
        public byte[] Body { get; }
        public string Detail { get; }

        public static ControlPlaneTransportResponse Completed(long httpStatus, byte[] body)
        {
            return new ControlPlaneTransportResponse(ControlPlaneTransportStatus.Completed, httpStatus, body, null);
        }

        public static ControlPlaneTransportResponse NetworkUnavailable(string detail)
        {
            return new ControlPlaneTransportResponse(ControlPlaneTransportStatus.NetworkUnavailable, 0, null, detail);
        }

        public static ControlPlaneTransportResponse TimedOut()
        {
            return new ControlPlaneTransportResponse(ControlPlaneTransportStatus.TimedOut, 0, null, null);
        }

        public static ControlPlaneTransportResponse TooLarge(long httpStatus)
        {
            return new ControlPlaneTransportResponse(ControlPlaneTransportStatus.TooLarge, httpStatus, null, null);
        }
    }
}
