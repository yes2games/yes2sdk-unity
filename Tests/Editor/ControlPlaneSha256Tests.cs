using System.Text;
using NUnit.Framework;

namespace Yes2SDK.Tests
{
    public class ControlPlaneSha256Tests
    {
        [TestCase("", "sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
        [TestCase("abc", "sha256:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
        [TestCase("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq", "sha256:248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1")]
        [TestCase("abcdefghbcdefghicdefghijdefghijkefghijklfghijklmghijklmnhijklmnoijklmnopjklmnopqklmnopqrlmnopqrsmnopqrstnopqrstu", "sha256:cf5b16a778af8380036ce59e7b0492370b249b11e8f07a51afac45037afee9d1")]
        public void Digest_MatchesNistVectors(string message, string expected)
        {
            Assert.AreEqual(expected, ControlPlaneSha256.Digest(Encoding.ASCII.GetBytes(message)));
        }

        [Test]
        public void Digest_MatchesMillionAVector()
        {
            Assert.AreEqual(
                "sha256:cdc76e5c9914fb9281a1c7e284d73e67f1809a48a497200e046d39ccc7112cd0",
                ControlPlaneSha256.Digest(Encoding.ASCII.GetBytes(new string('a', 1000000))));
        }

        [Test]
        public void Digest_IsOverExactBytes()
        {
            Assert.AreNotEqual(
                ControlPlaneSha256.Digest(Encoding.UTF8.GetBytes("{\"a\":1}")),
                ControlPlaneSha256.Digest(Encoding.UTF8.GetBytes("{\"a\": 1}")));
        }

        [TestCase("sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", true)]
        [TestCase("sha256:E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855", false)]
        [TestCase("SHA256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", false)]
        [TestCase("sha512:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", false)]
        [TestCase("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", false)]
        [TestCase("sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b85", false)]
        [TestCase("sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b8550", false)]
        [TestCase("sha256:g3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsDigest_AcceptsOnlyLowercaseSha256(string value, bool expected)
        {
            Assert.AreEqual(expected, ControlPlaneSha256.IsDigest(value));
        }
    }
}
