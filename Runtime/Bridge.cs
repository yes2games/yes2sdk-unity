using UnityEngine;
using Newtonsoft.Json;

namespace Yes2SDK
{
    /// <summary>
    /// Bridge component that receives callbacks from JavaScript via SendMessage.
    /// This GameObject is automatically created when Yes2SDK initializes.
    /// </summary>
    public class Bridge : MonoBehaviour
    {
        private static Bridge _instance;

        /// <summary>
        /// Gets or creates the singleton bridge instance.
        /// </summary>
        public static Bridge Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("Bridge");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<Bridge>();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Ensures the bridge is created. Call this before any SDK operations.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            // Access Instance to ensure it's created
            var _ = Instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        #region Initialization Callbacks

        /// <summary>
        /// Called from JavaScript when initialization succeeds.
        /// </summary>
        public void OnInitializeSuccess(string message)
        {
            Callbacks.InvokeInitializeSuccess();
        }

        /// <summary>
        /// Called from JavaScript when initialization fails.
        /// </summary>
        public void OnInitializeError(string errorJson)
        {
            var error = ParseError(errorJson);
            Callbacks.InvokeInitializeError(error);
        }

        #endregion

        #region Game Start Callbacks

        /// <summary>
        /// Called from JavaScript when game start succeeds.
        /// </summary>
        public void OnStartGameSuccess(string message)
        {
            Callbacks.InvokeStartGameSuccess();
        }

        /// <summary>
        /// Called from JavaScript when game start fails.
        /// </summary>
        public void OnStartGameError(string errorJson)
        {
            var error = ParseError(errorJson);
            Callbacks.InvokeStartGameError(error);
        }

        #endregion

        #region Lifecycle Callbacks

        /// <summary>
        /// Called from JavaScript when game should pause.
        /// </summary>
        public void OnPause(string message)
        {
            Callbacks.InvokePause();
        }

        /// <summary>
        /// Called from JavaScript when game can resume.
        /// </summary>
        public void OnResume(string message)
        {
            Callbacks.InvokeResume();
        }

        #endregion

        #region Utility

        private Error ParseError(string errorJson)
        {
            if (string.IsNullOrEmpty(errorJson))
            {
                return new Error
                {
                    Code = "Unknown",
                    Message = "Unknown error",
                    Context = "Unknown"
                };
            }

            try
            {
                return JsonConvert.DeserializeObject<Error>(errorJson);
            }
            catch
            {
                return new Error
                {
                    Code = "Unknown",
                    Message = errorJson,
                    Context = "Unknown"
                };
            }
        }

        #endregion
    }

    /// <summary>
    /// Internal callback manager for Yes2SDK.
    /// </summary>
    internal static class Callbacks
    {
        internal static System.Action InitializeSuccessCallback;
        internal static System.Action<Error> InitializeErrorCallback;
        internal static System.Action StartGameSuccessCallback;
        internal static System.Action<Error> StartGameErrorCallback;

        internal static void InvokeInitializeSuccess()
        {
            Yes2SDK.SetInitialized(true);
            InitializeSuccessCallback?.Invoke();
            InitializeSuccessCallback = null;
        }

        internal static void InvokeInitializeError(Error error)
        {
            InitializeErrorCallback?.Invoke(error);
            InitializeErrorCallback = null;
        }

        internal static void InvokeStartGameSuccess()
        {
            StartGameSuccessCallback?.Invoke();
            StartGameSuccessCallback = null;
        }

        internal static void InvokeStartGameError(Error error)
        {
            StartGameErrorCallback?.Invoke(error);
            StartGameErrorCallback = null;
        }

        internal static void InvokePause()
        {
            Yes2SDK.InvokePause();
        }

        internal static void InvokeResume()
        {
            Yes2SDK.InvokeResume();
        }
    }
}
