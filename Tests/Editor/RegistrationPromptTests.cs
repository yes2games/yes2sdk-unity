using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Yes2SDK.Tests
{
    /// <summary>
    /// Covers the registration prompt handle: a close message reaches only its
    /// own prompt and fires onClose at most once, a failed show returns null
    /// and reports the error synchronously, Login/Close on a closed handle do
    /// nothing, and the Editor mock follows the platform message rules.
    /// </summary>
    public class RegistrationPromptTests
    {
        private static readonly Regex DroppedCloseWarning = new Regex("dropped registration prompt close");
        private static readonly Regex ClosedHandleWarning = new Regex("registration prompt .* is already closed");

        private List<string> _calls;
        private Bridge _bridge;
        private bool _savedPlayerRegistered;

        [SetUp]
        public void SetUp()
        {
            _calls = new List<string>();
            Yes2SDKAuth.ResetRegistrationPromptStateForTests();
            Yes2SDKEditorMock.SessionRegisteredOverride = false;
            _savedPlayerRegistered = Yes2SDKEditorMock.PlayerRegistered;
            Yes2SDKEditorMock.PlayerRegistered = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (_bridge != null) UnityEngine.Object.DestroyImmediate(_bridge.gameObject);
            Yes2SDKAuth.ResetRegistrationPromptStateForTests();
            Yes2SDKEditorMock.SessionRegisteredOverride = false;
            Yes2SDKEditorMock.PlayerRegistered = _savedPlayerRegistered;
        }

        private RegistrationPrompt Open(string label)
        {
            return Yes2SDKAuth.CompleteShowForTests("",
                () => _calls.Add($"{label}:close"),
                error => _calls.Add($"{label}:error:{error.Code}"));
        }

        private static string Id(RegistrationPrompt prompt)
        {
            return prompt.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        // ---- show result ----------------------------------------------------------------

        [Test]
        public void CompleteShow_EmptyResult_ReturnsAnOpenHandle()
        {
            var prompt = Open("a");

            Assert.IsNotNull(prompt);
            Assert.IsTrue(prompt.IsOpen);
            Assert.AreEqual(1, Yes2SDKAuth.OpenPromptCountForTests());
            CollectionAssert.IsEmpty(_calls);
        }

        [Test]
        public void CompleteShow_ErrorJson_ReturnsNullAndCallsOnErrorSynchronously()
        {
            Error received = default;
            var prompt = Yes2SDKAuth.CompleteShowForTests(
                "{\"code\":\"INVALID_PARAM\",\"message\":\"bad message\",\"context\":\"Yes2SDK.Auth.ShowRegistrationPrompt\"}",
                () => _calls.Add("close"),
                error => received = error);

            Assert.IsNull(prompt);
            Assert.AreEqual("INVALID_PARAM", received.Code);
            Assert.AreEqual(ErrorCode.InvalidParams, received.ErrorCode);
            Assert.AreEqual("bad message", received.Message);
            Assert.AreEqual(0, Yes2SDKAuth.OpenPromptCountForTests());
            CollectionAssert.IsEmpty(_calls);
        }

        [Test]
        public void CompleteShow_ErrorWithNoOnError_ReturnsNullWithoutThrowing()
        {
            RegistrationPrompt prompt = null;
            Assert.DoesNotThrow(() => prompt = Yes2SDKAuth.CompleteShowForTests(
                "{\"code\":\"FEATURE_NOT_SUPPORTED\",\"message\":\"no\",\"context\":\"x\"}", null, null));
            Assert.IsNull(prompt);
            Assert.AreEqual(0, Yes2SDKAuth.OpenPromptCountForTests());
        }

        // ---- close routing --------------------------------------------------------------

        [Test]
        public void CloseMessage_ReachesOnlyItsOwnPrompt()
        {
            var first = Open("first");
            var second = Open("second");

            Yes2SDKAuth.HandleRegistrationPromptClose(Id(second));

            CollectionAssert.AreEqual(new[] { "second:close" }, _calls);
            Assert.IsTrue(first.IsOpen);
            Assert.IsFalse(second.IsOpen);
            Assert.AreEqual(1, Yes2SDKAuth.OpenPromptCountForTests());
        }

        [Test]
        public void CloseMessage_Duplicate_IsDroppedWithAWarning()
        {
            var prompt = Open("a");
            Yes2SDKAuth.HandleRegistrationPromptClose(Id(prompt));

            LogAssert.Expect(LogType.Warning, DroppedCloseWarning);
            Yes2SDKAuth.HandleRegistrationPromptClose(Id(prompt));

            CollectionAssert.AreEqual(new[] { "a:close" }, _calls);
        }

        [TestCase("999")]
        [TestCase("")]
        [TestCase(null)]
        [TestCase("abc")]
        [TestCase("-1")]
        public void CloseMessage_UnknownOrMalformedId_IsDroppedWithAWarning(string idText)
        {
            Open("a");

            LogAssert.Expect(LogType.Warning, DroppedCloseWarning);
            Assert.DoesNotThrow(() => Yes2SDKAuth.HandleRegistrationPromptClose(idText));

            CollectionAssert.IsEmpty(_calls);
            Assert.AreEqual(1, Yes2SDKAuth.OpenPromptCountForTests());
        }

        [Test]
        public void CloseMessage_ThrowingOnClose_StillRemovesThePrompt()
        {
            var prompt = Yes2SDKAuth.CompleteShowForTests("",
                () => throw new InvalidOperationException("onClose failed"), null);

            Assert.Throws<InvalidOperationException>(() => Yes2SDKAuth.HandleRegistrationPromptClose(Id(prompt)));

            Assert.IsFalse(prompt.IsOpen);
            Assert.AreEqual(0, Yes2SDKAuth.OpenPromptCountForTests());
        }

        [Test]
        public void BridgeMessage_RoutesTheCloseToItsPrompt()
        {
            _bridge = new GameObject("BridgeTest").AddComponent<Bridge>();
            var prompt = Open("a");

            _bridge.OnRegistrationPromptClose(Id(prompt));

            CollectionAssert.AreEqual(new[] { "a:close" }, _calls);
            Assert.IsFalse(prompt.IsOpen);
        }

        [Test]
        public void BridgeMessage_ThrowingOnClose_IsLoggedAndDoesNotPropagate()
        {
            _bridge = new GameObject("BridgeTest").AddComponent<Bridge>();
            var prompt = Yes2SDKAuth.CompleteShowForTests("",
                () => throw new InvalidOperationException("onClose failed"), null);

            LogAssert.Expect(LogType.Error, new Regex("callback 'OnRegistrationPromptClose' threw"));
            Assert.DoesNotThrow(() => _bridge.OnRegistrationPromptClose(Id(prompt)));
            Assert.AreEqual(0, Yes2SDKAuth.OpenPromptCountForTests());
        }

        // ---- handle actions after close -------------------------------------------------

        [Test]
        public void LoginAndClose_AfterClose_AreNoOps()
        {
            var prompt = Open("a");
            Yes2SDKAuth.HandleRegistrationPromptClose(Id(prompt));

            LogAssert.Expect(LogType.Warning, ClosedHandleWarning);
            prompt.Login();
            LogAssert.Expect(LogType.Warning, ClosedHandleWarning);
            prompt.Close();

            CollectionAssert.AreEqual(new[] { "a:close" }, _calls);
            Assert.IsFalse(Yes2SDKEditorMock.SessionRegisteredOverride);
        }

        // ---- Editor mock ----------------------------------------------------------------

        [Test]
        public void EditorMock_Guest_ShowReturnsAnOpenHandle()
        {
            var prompt = new Yes2SDKAuth().ShowRegistrationPrompt(null,
                () => _calls.Add("close"), error => _calls.Add("error:" + error.Code));

            Assert.IsNotNull(prompt);
            Assert.IsTrue(prompt.IsOpen);
            CollectionAssert.IsEmpty(_calls);
        }

        [Test]
        public void EditorMock_Login_RegistersThePlayerAndFiresOnCloseOnce()
        {
            var auth = new Yes2SDKAuth();
            var prompt = auth.ShowRegistrationPrompt(null, () => _calls.Add("close"));
            Assert.IsFalse(auth.IsAuthenticated());

            prompt.Login();

            Assert.IsTrue(Yes2SDKEditorMock.SessionRegisteredOverride);
            Assert.IsTrue(auth.IsAuthenticated());
            Assert.IsFalse(prompt.IsOpen);
            CollectionAssert.AreEqual(new[] { "close" }, _calls);
            Assert.AreEqual(0, Yes2SDKAuth.OpenPromptCountForTests());
        }

        [Test]
        public void EditorMock_Close_FiresOnCloseOnceWithoutRegistering()
        {
            var prompt = new Yes2SDKAuth().ShowRegistrationPrompt(null, () => _calls.Add("close"));

            prompt.Close();

            Assert.IsFalse(Yes2SDKEditorMock.SessionRegisteredOverride);
            Assert.IsFalse(prompt.IsOpen);
            CollectionAssert.AreEqual(new[] { "close" }, _calls);
        }

        [Test]
        public void EditorMock_RegisteredPlayer_GetsInvalidOperationAndNull()
        {
            Yes2SDKEditorMock.SessionRegisteredOverride = true;
            Error received = default;

            var prompt = new Yes2SDKAuth().ShowRegistrationPrompt(null, null, error => received = error);

            Assert.IsNull(prompt);
            Assert.AreEqual("INVALID_OPERATION", received.Code);
            Assert.AreEqual(0, Yes2SDKAuth.OpenPromptCountForTests());
        }

        [Test]
        public void EditorMock_InvalidMessage_GetsInvalidParamAndNull()
        {
            Error received = default;
            var options = new RegistrationPromptOptions { Message = "no code here" };

            var prompt = new Yes2SDKAuth().ShowRegistrationPrompt(options, null, error => received = error);

            Assert.IsNull(prompt);
            Assert.AreEqual("INVALID_PARAM", received.Code);
            Assert.AreEqual(ErrorCode.InvalidParams, received.ErrorCode);
            StringAssert.Contains("{{registrationCode}}", received.Message);
        }

        [Test]
        public void EditorMock_UnserializableData_GetsInvalidParamBeforeRegisteredCheck()
        {
            Yes2SDKEditorMock.SessionRegisteredOverride = true;
            var cyclic = new Dictionary<string, object>();
            cyclic["self"] = cyclic;
            Error received = default;
            var options = new RegistrationPromptOptions { Data = cyclic };

            var prompt = new Yes2SDKAuth().ShowRegistrationPrompt(options, null, error => received = error);

            Assert.IsNull(prompt);
            Assert.AreEqual("INVALID_PARAM", received.Code);
            StringAssert.Contains("could not be serialized", received.Message);
            Assert.AreEqual(0, Yes2SDKAuth.OpenPromptCountForTests());
        }

        [Test]
        public void EditorMock_ThrowingOnClose_IsLoggedAndDoesNotEscapeClose()
        {
            var prompt = new Yes2SDKAuth().ShowRegistrationPrompt(null,
                () => throw new InvalidOperationException("onClose failed"));

            LogAssert.Expect(LogType.Error, new Regex("callback 'OnRegistrationPromptClose' threw"));
            Assert.DoesNotThrow(() => prompt.Close());
            Assert.AreEqual(0, Yes2SDKAuth.OpenPromptCountForTests());
        }

        // ---- message validator ----------------------------------------------------------

        [TestCase("Let me in! {{registrationCode}} is my code.")]
        [TestCase("{{registrationCode}}")]
        [TestCase("code:{{registrationCode}}!")]
        [TestCase("{{registrationCode}} is my code")]
        public void ValidateMessage_AcceptsValidMessages(string message)
        {
            Assert.IsTrue(Yes2SDKAuth.TryValidateRegistrationMessage(message, out var error), error);
            Assert.IsNull(error);
        }

        [TestCase("", "must not be empty")]
        [TestCase("   ", "must not be empty")]
        [TestCase("\uFEFF", "must not be empty")]
        [TestCase("\u00A0\u2003\u3000\uFEFF", "must not be empty")]
        [TestCase("no code here", "exactly once")]
        [TestCase("{{registrationCode}} and {{registrationCode}}", "exactly once")]
        [TestCase("Hi {{name}}, {{registrationCode}} is my code", "placeholders other than")]
        [TestCase("abc{{registrationCode}} is my code", "must not run into")]
        [TestCase("my code {{registrationCode}}7", "must not run into")]
        [TestCase("my code {{registrationCode}}_x", "must not run into")]
        public void ValidateMessage_RejectsInvalidMessages(string message, string expectedFragment)
        {
            Assert.IsFalse(Yes2SDKAuth.TryValidateRegistrationMessage(message, out var error));
            StringAssert.Contains(expectedFragment, error);
        }

        [Test]
        public void ValidateMessage_LengthLimitIs140Characters()
        {
            string code = "{{registrationCode}}";
            string atLimit = code + " " + new string('a', 140 - code.Length - 1);
            string overLimit = atLimit + "a";

            Assert.AreEqual(140, atLimit.Length);
            Assert.IsTrue(Yes2SDKAuth.TryValidateRegistrationMessage(atLimit, out _));
            Assert.IsFalse(Yes2SDKAuth.TryValidateRegistrationMessage(overLimit, out var error));
            StringAssert.Contains("at most 140 characters", error);
        }

        // ---- options JSON ---------------------------------------------------------------

        [Test]
        public void OptionsJson_OmitsUnsetFields()
        {
            Assert.AreEqual("{}", new RegistrationPromptOptions().ToJson());
        }

        [Test]
        public void OptionsJson_CarriesThemeDataAndMessage()
        {
            var options = new RegistrationPromptOptions
            {
                Theme = RegistrationPromptTheme.Light,
                Data = new Dictionary<string, object> { ["level"] = 3, ["tag"] = "a" },
                Message = ""
            };

            Assert.AreEqual("{\"theme\":\"light\",\"data\":{\"level\":3,\"tag\":\"a\"},\"message\":\"\"}", options.ToJson());
        }

        [Test]
        public void OptionsJson_DarkTheme()
        {
            Assert.AreEqual("{\"theme\":\"dark\"}",
                new RegistrationPromptOptions { Theme = RegistrationPromptTheme.Dark }.ToJson());
        }
    }
}
