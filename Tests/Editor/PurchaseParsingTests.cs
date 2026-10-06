using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace Yes2SDK.Tests
{
    /// <summary>
    /// Covers typed Purchase parsing for the platform payload shape and the
    /// editor mock shape.
    /// </summary>
    public class PurchaseParsingTests
    {
        private const string Full =
            "{\"purchaseToken\":\"tok-1\",\"productId\":\"gems_100\",\"paymentId\":\"pay-1\"," +
            "\"purchaseTime\":\"2026-10-06T12:00:00.000Z\",\"developerPayload\":\"payload\"," +
            "\"signedRequest\":\"sig.abc\",\"isSandbox\":true}";

        private const string Minimal =
            "{\"purchaseToken\":\"tok-2\",\"productId\":\"gems_5\",\"paymentId\":\"pay-2\"," +
            "\"purchaseTime\":\"2026-10-06T12:00:00.000Z\"}";

        [Test]
        public void FromJson_FullPayload_ParsesEveryField()
        {
            var p = Purchase.FromJson(Full);
            Assert.IsNotNull(p);
            Assert.AreEqual("gems_100", p.ProductId);
            Assert.AreEqual("tok-1", p.PurchaseToken);
            Assert.AreEqual("pay-1", p.PaymentId);
            Assert.AreEqual("2026-10-06T12:00:00.000Z", p.PurchaseTime);
            Assert.AreEqual("payload", p.DeveloperPayload);
            Assert.AreEqual("sig.abc", p.SignedRequest);
            Assert.IsTrue(p.IsSandbox);
        }

        [Test]
        public void FromJson_WithoutNewFields_DefaultsSandboxFalseAndNoSignedRequest()
        {
            var p = Purchase.FromJson(Minimal);
            Assert.IsNotNull(p);
            Assert.AreEqual("gems_5", p.ProductId);
            Assert.IsFalse(p.IsSandbox);
            Assert.IsNull(p.SignedRequest);
            Assert.IsNull(p.DeveloperPayload);
        }

        [Test]
        public void ListFromJson_ArrayPayload_ParsesAll()
        {
            List<Purchase> list = Purchase.ListFromJson("[" + Full + "," + Minimal + "]");
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual("gems_100", list[0].ProductId);
            Assert.IsTrue(list[0].IsSandbox);
            Assert.AreEqual("gems_5", list[1].ProductId);
            Assert.IsFalse(list[1].IsSandbox);
        }

        [Test]
        public void ListFromJson_EmptyArray_ReturnsEmptyList()
        {
            Assert.AreEqual(0, Purchase.ListFromJson("[]").Count);
        }

        [Test]
        public void FromJson_MalformedJson_ReturnsNullAndWarns()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*Purchase.*"));
            Assert.IsNull(Purchase.FromJson("{not json"));
        }

        [Test]
        public void FromJson_NullOrEmpty_ReturnsNullAndWarns()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*Purchase.*"));
            Assert.IsNull(Purchase.FromJson(null));
        }

        [Test]
        public void ListFromJson_MalformedJson_ReturnsEmptyListAndWarns()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*Purchase.*"));
            var list = Purchase.ListFromJson("[{oops");
            Assert.IsNotNull(list);
            Assert.AreEqual(0, list.Count);
        }
    }
}
