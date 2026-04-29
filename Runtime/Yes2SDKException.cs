using System;

namespace Yes2SDK
{
    /// <summary>
    /// Exception wrapping a Yes2SDK <see cref="Error"/> for use with the
    /// Task-returning overloads of the SDK's async APIs. The original
    /// <see cref="Error"/> struct is available via <see cref="SdkError"/>
    /// so callers can branch on <see cref="ErrorCode"/> in a catch block.
    /// </summary>
    public class Yes2SDKException : Exception
    {
        public Error SdkError { get; }
        public ErrorCode ErrorCode => SdkError.ErrorCode;

        public Yes2SDKException(Error sdkError)
            : base(string.IsNullOrEmpty(sdkError.Message)
                ? $"Yes2SDK error ({sdkError.ErrorCode}): {sdkError.Code}"
                : sdkError.Message)
        {
            SdkError = sdkError;
        }
    }
}
