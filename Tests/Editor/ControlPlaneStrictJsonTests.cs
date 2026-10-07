using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Yes2SDK.Tests
{
    public class ControlPlaneStrictJsonTests
    {
        [Test]
        public void Parse_KeepsJsonTypes()
        {
            var token = ControlPlaneStrictJson.Parse(
                "{\"i\":-9007199254740991,\"f\":1.5e300,\"s\":\"2026-10-07T00:00:00Z\",\"b\":true,\"n\":null,\"a\":[0,-0.0,1E-400],\"o\":{}}");

            Assert.AreEqual(JTokenType.Integer, token["i"].Type);
            Assert.AreEqual(-9007199254740991L, token["i"].Value<long>());
            Assert.AreEqual(JTokenType.Float, token["f"].Type);
            Assert.AreEqual(1.5e300, token["f"].Value<double>());
            Assert.AreEqual(JTokenType.String, token["s"].Type);
            Assert.AreEqual("2026-10-07T00:00:00Z", token["s"].Value<string>());
            Assert.AreEqual(JTokenType.Boolean, token["b"].Type);
            Assert.AreEqual(JTokenType.Null, token["n"].Type);
            Assert.AreEqual(3, ((JArray)token["a"]).Count);
            Assert.AreEqual(JTokenType.Object, token["o"].Type);
        }

        [TestCase("{\"a\":1,\"a\":1}")]
        [TestCase("{\"a\":1,\"b\":2,\"a\":3}")]
        [TestCase("{\"x\":{\"a\":1,\"a\":2}}")]
        [TestCase("[{\"a\":[{\"k\":true,\"k\":false}]}]")]
        [TestCase("{\"a\":1,\"\\u0061\":2}")]
        public void Parse_RejectsDuplicateKeys(string json)
        {
            Assert.Throws<FormatException>(() => ControlPlaneStrictJson.Parse(json));
        }

        [Test]
        public void Parse_AllowsSameKeyInSiblingObjects()
        {
            var token = ControlPlaneStrictJson.Parse("[{\"a\":1},{\"a\":2,\"A\":3}]");

            Assert.AreEqual(2, token[1]["a"].Value<long>());
        }

        [TestCase("9007199254740991", 9007199254740991L)]
        [TestCase("-9007199254740991", -9007199254740991L)]
        [TestCase("0", 0L)]
        [TestCase("-0", 0L)]
        public void Parse_AcceptsSafeIntegers(string json, long expected)
        {
            var token = ControlPlaneStrictJson.Parse(json);

            Assert.AreEqual(JTokenType.Integer, token.Type);
            Assert.AreEqual(expected, token.Value<long>());
        }

        [TestCase("9007199254740992")]
        [TestCase("-9007199254740992")]
        [TestCase("9223372036854775807")]
        [TestCase("99999999999999999999999")]
        [TestCase("{\"a\":[9007199254740992]}")]
        public void Parse_RejectsUnsafeIntegers(string json)
        {
            Assert.Throws<FormatException>(() => ControlPlaneStrictJson.Parse(json));
        }

        [TestCase("NaN")]
        [TestCase("[NaN]")]
        [TestCase("{\"a\":NaN}")]
        [TestCase("Infinity")]
        [TestCase("-Infinity")]
        [TestCase("[Infinity]")]
        [TestCase("1e400")]
        [TestCase("-1e400")]
        [TestCase("{\"a\":1.8e308}")]
        public void Parse_RejectsNonFiniteNumbers(string json)
        {
            Assert.Throws<FormatException>(() => ControlPlaneStrictJson.Parse(json));
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase("{} {}")]
        [TestCase("{\"a\":1}x")]
        [TestCase("{\"a\":1,}")]
        [TestCase("[1,]")]
        [TestCase("[1,,2]")]
        [TestCase("{'a':1}")]
        [TestCase("{a:1}")]
        [TestCase("['a']")]
        [TestCase("[1 /* c */]")]
        [TestCase("// c\n1")]
        [TestCase("[undefined]")]
        [TestCase("[new Date(1)]")]
        [TestCase("[0x10]")]
        [TestCase("[010]")]
        [TestCase("[+1]")]
        [TestCase("[.5]")]
        [TestCase("[1.]")]
        [TestCase("[1e]")]
        [TestCase("[True]")]
        [TestCase("[nul]")]
        [TestCase("[\"a\nb\"]")]
        [TestCase("[\"\\x41\"]")]
        [TestCase("[\"\\u00G1\"]")]
        [TestCase("[\"abc]")]
        [TestCase("\u00a0[]")]
        public void Parse_RejectsNonStandardJson(string json)
        {
            Assert.Throws<FormatException>(() => ControlPlaneStrictJson.Parse(json));
        }

        [Test]
        public void Parse_BoundsNesting()
        {
            var depth = ControlPlaneStrictJson.MaxDepth;

            Assert.DoesNotThrow(() => ControlPlaneStrictJson.Parse(new string('[', depth) + new string(']', depth)));
            Assert.Throws<FormatException>(() => ControlPlaneStrictJson.Parse(new string('[', depth + 1) + new string(']', depth + 1)));
            Assert.Throws<FormatException>(() => ControlPlaneStrictJson.Parse(string.Concat(Enumerable.Repeat("{\"a\":", depth + 1)) + "1" + new string('}', depth + 1)));
        }

        [Test]
        public void Parse_ReadsUtf8Bytes()
        {
            var token = ControlPlaneStrictJson.Parse(Encoding.UTF8.GetBytes("{\"s\":\"h\u00e9llo \ud83d\ude00\",\"e\":\"\\u00e9\"}"));

            Assert.AreEqual("h\u00e9llo \ud83d\ude00", token["s"].Value<string>());
            Assert.AreEqual("\u00e9", token["e"].Value<string>());
        }

        [Test]
        public void Parse_RejectsInvalidUtf8()
        {
            var bytes = new byte[] { (byte)'[', (byte)'"', 0xff, (byte)'"', (byte)']' };

            Assert.Throws<FormatException>(() => ControlPlaneStrictJson.Parse(bytes));
        }
    }
}
