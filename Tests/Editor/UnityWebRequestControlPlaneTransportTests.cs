using NUnit.Framework;

namespace Yes2SDK.Tests
{
    public class UnityWebRequestControlPlaneTransportTests
    {
        [TestCase("https://runtime.yes2games.com/environments/nsr/production/bootstrap-v1.json", "https://runtime.yes2games.com/environments/nsr/production/bootstrap-v1.json?cb=n1")]
        [TestCase("https://runtime.yes2games.com/bootstrap-v1.json?a=1", "https://runtime.yes2games.com/bootstrap-v1.json?a=1&cb=n1")]
        public void CacheBypassUrl_AppendsNonce(string url, string expected)
        {
            Assert.AreEqual(expected, UnityWebRequestControlPlaneTransport.CacheBypassUrl(url, "n1"));
        }
    }
}
