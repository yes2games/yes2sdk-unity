using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Yes2SDK.Tests
{
    /// <summary>
    /// Covers the entry point data dictionary and the Editor payload.
    /// </summary>
    public class EntryPointDataTests
    {
        private string _savedJson;

        [SetUp]
        public void SetUp()
        {
            _savedJson = Yes2SDKEditorMock.EntryPointDataJson;
        }

        [TearDown]
        public void TearDown()
        {
            Yes2SDKEditorMock.EntryPointDataJson = _savedJson;
        }

        [Test]
        public void Parse_NestedJson_ProducesJObjectAndJArray()
        {
            Dictionary<string, object> d = Yes2SDKSession.ParseEntryPointData(
                "{\"a\":\"x\",\"n\":{\"k\":1},\"list\":[1,2]}");

            Assert.AreEqual(3, d.Count);
            Assert.AreEqual("x", d["a"]);
            Assert.IsInstanceOf<JObject>(d["n"]);
            Assert.AreEqual(1, (int)((JObject)d["n"])["k"]);
            Assert.IsInstanceOf<JArray>(d["list"]);
            Assert.AreEqual(2, ((JArray)d["list"]).Count);
        }

        [Test]
        public void Parse_EmptyString_ReturnsEmpty()
        {
            Assert.AreEqual(0, Yes2SDKSession.ParseEntryPointData("").Count);
            Assert.AreEqual(0, Yes2SDKSession.ParseEntryPointData(null).Count);
        }

        [Test]
        public void Parse_InvalidJson_ReturnsEmpty()
        {
            Assert.AreEqual(0, Yes2SDKSession.ParseEntryPointData("{not json").Count);
        }

        [Test]
        public void Parse_ArrayJson_ReturnsEmpty()
        {
            Assert.AreEqual(0, Yes2SDKSession.ParseEntryPointData("[1,2,3]").Count);
        }

        [Test]
        public void Editor_ReturnsConfiguredPayload()
        {
            Yes2SDKEditorMock.EntryPointDataJson = "{\"ref\":\"abc\"}";
            var session = new Yes2SDKSession();

            Assert.AreEqual("{\"ref\":\"abc\"}", session.GetEntryPointData());
            Assert.AreEqual("abc", session.GetEntryPointDataDictionary()["ref"]);
        }

        [Test]
        public void Editor_NonObjectPayload_FallsBackToEmptyObject()
        {
            Yes2SDKEditorMock.EntryPointDataJson = "[1]";
            var session = new Yes2SDKSession();
            Assert.AreEqual("{}", session.GetEntryPointData());

            Yes2SDKEditorMock.EntryPointDataJson = "garbage";
            Assert.AreEqual("{}", session.GetEntryPointData());
            Assert.AreEqual(0, session.GetEntryPointDataDictionary().Count);
        }
    }
}
