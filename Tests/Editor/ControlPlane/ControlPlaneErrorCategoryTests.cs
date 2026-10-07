using System;
using System.Linq;
using NUnit.Framework;

namespace Yes2SDK.Tests
{
    public class ControlPlaneErrorCategoryTests
    {
        [Test]
        public void Codes_AreExactlyTheErrorsV1Set()
        {
            var codes = Enum.GetValues(typeof(ControlPlaneErrorCategory))
                .Cast<ControlPlaneErrorCategory>()
                .Select(category => category.Code());

            CollectionAssert.AreEqual(new[]
            {
                "network_unavailable",
                "timeout",
                "protocol_unsupported",
                "bootstrap_http_failed",
                "bootstrap_invalid",
                "snapshot_http_failed",
                "snapshot_invalid",
                "resource_missing",
                "resource_unsupported",
                "artifact_missing",
                "artifact_http_failed",
                "artifact_integrity_failed",
                "cache_unavailable",
                "cancelled"
            }, codes);
        }

        [Test]
        public void Code_RejectsUndefinedCategory()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ((ControlPlaneErrorCategory)99).Code());
        }
    }
}
