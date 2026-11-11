using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Yes2SDK
{
    internal static class ControlPlaneSha256
    {
        private const string Prefix = "sha256:";

        public static string Digest(byte[] bytes)
        {
            return Digest(sha => sha.ComputeHash(bytes));
        }

        public static string Digest(Stream stream)
        {
            return Digest(sha => sha.ComputeHash(stream));
        }

        private static string Digest(Func<SHA256, byte[]> compute)
        {
            byte[] hash;
            using (var sha = new SHA256Managed())
            {
                hash = compute(sha);
            }

            var digest = new StringBuilder(Prefix, Prefix.Length + hash.Length * 2);
            foreach (var b in hash)
            {
                digest.Append(b.ToString("x2"));
            }
            return digest.ToString();
        }

        public static bool IsDigest(string value)
        {
            if (value == null || value.Length != Prefix.Length + 64 || !value.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return false;
            }
            for (var i = Prefix.Length; i < value.Length; i++)
            {
                var c = value[i];
                if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f'))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
