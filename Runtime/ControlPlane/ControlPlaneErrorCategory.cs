using System;

namespace Yes2SDK
{
    internal enum ControlPlaneErrorCategory
    {
        NetworkUnavailable,
        Timeout,
        ProtocolUnsupported,
        BootstrapHttpFailed,
        BootstrapInvalid,
        SnapshotHttpFailed,
        SnapshotInvalid,
        ResourceMissing,
        ResourceUnsupported,
        ArtifactMissing,
        ArtifactHttpFailed,
        ArtifactIntegrityFailed,
        CacheUnavailable,
        Cancelled
    }

    internal static class ControlPlaneErrorCategories
    {
        public static string Code(this ControlPlaneErrorCategory category)
        {
            return category switch
            {
                ControlPlaneErrorCategory.NetworkUnavailable => "network_unavailable",
                ControlPlaneErrorCategory.Timeout => "timeout",
                ControlPlaneErrorCategory.ProtocolUnsupported => "protocol_unsupported",
                ControlPlaneErrorCategory.BootstrapHttpFailed => "bootstrap_http_failed",
                ControlPlaneErrorCategory.BootstrapInvalid => "bootstrap_invalid",
                ControlPlaneErrorCategory.SnapshotHttpFailed => "snapshot_http_failed",
                ControlPlaneErrorCategory.SnapshotInvalid => "snapshot_invalid",
                ControlPlaneErrorCategory.ResourceMissing => "resource_missing",
                ControlPlaneErrorCategory.ResourceUnsupported => "resource_unsupported",
                ControlPlaneErrorCategory.ArtifactMissing => "artifact_missing",
                ControlPlaneErrorCategory.ArtifactHttpFailed => "artifact_http_failed",
                ControlPlaneErrorCategory.ArtifactIntegrityFailed => "artifact_integrity_failed",
                ControlPlaneErrorCategory.CacheUnavailable => "cache_unavailable",
                ControlPlaneErrorCategory.Cancelled => "cancelled",
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }
    }
}
