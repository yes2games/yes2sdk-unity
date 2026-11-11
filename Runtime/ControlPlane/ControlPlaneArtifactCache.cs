using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Yes2SDK
{
    internal sealed class ControlPlaneArtifactCache
    {
        public const long BudgetBytes = 64L * 1024 * 1024;
        private const string TempSuffix = ".tmp";
        private const int NameLength = 64;

        private readonly string _directory;
        private readonly HashSet<string> _returned = new HashSet<string>(StringComparer.Ordinal);
        private bool _swept;

        public ControlPlaneArtifactCache(string directory)
        {
            _directory = directory;
        }

        public static bool Verify(string path, string digest, long size)
        {
            if (!File.Exists(path) || new FileInfo(path).Length != size)
            {
                return false;
            }
            using (var stream = File.OpenRead(path))
            {
                return ControlPlaneSha256.Digest(stream) == digest;
            }
        }

        public string TryGetVerified(string digest, long size)
        {
            var path = PathOf(digest);
            if (!Verify(path, digest, size))
            {
                return null;
            }
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            _returned.Add(digest);
            return path;
        }

        public string NewTempPath(string digest)
        {
            Directory.CreateDirectory(_directory);
            if (!_swept)
            {
                _swept = true;
                foreach (var stale in Directory.GetFiles(_directory, "*" + TempSuffix))
                {
                    File.Delete(stale);
                }
            }
            return PathOf(digest) + "." + Guid.NewGuid().ToString("N") + TempSuffix;
        }

        public string Promote(string temp, string digest, long size)
        {
            var path = PathOf(digest);
            if (!Verify(path, digest, size))
            {
                File.Delete(path);
                File.Move(temp, path);
            }
            _returned.Add(digest);
            Evict();
            ControlPlaneLkgJournal.Persist();
            return path;
        }

        public void Clear()
        {
            _returned.Clear();
            if (!Directory.Exists(_directory))
            {
                return;
            }
            foreach (var file in new DirectoryInfo(_directory).GetFiles().Where(file => IsEntry(file) || file.Name.EndsWith(TempSuffix, StringComparison.Ordinal)))
            {
                file.Delete();
            }
            ControlPlaneLkgJournal.Persist();
        }

        private void Evict()
        {
            var entries = new DirectoryInfo(_directory).GetFiles().Where(IsEntry).OrderBy(file => file.LastWriteTimeUtc).ToList();
            var total = entries.Sum(file => file.Length);
            foreach (var file in entries)
            {
                if (total <= BudgetBytes)
                {
                    return;
                }
                if (!_returned.Contains("sha256:" + file.Name))
                {
                    total -= file.Length;
                    file.Delete();
                }
            }
        }

        private static bool IsEntry(FileInfo file)
        {
            return file.Name.Length == NameLength && ControlPlaneSha256.IsDigest("sha256:" + file.Name);
        }

        private string PathOf(string digest)
        {
            return Path.Combine(_directory, digest.Substring("sha256:".Length));
        }
    }
}
