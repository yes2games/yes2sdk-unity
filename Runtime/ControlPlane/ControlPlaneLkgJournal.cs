using System;
using System.IO;
using System.Text;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Yes2SDK
{
    internal sealed class ControlPlaneLkgJournal
    {
        public const string SlotA = "lkg-a.dat";
        public const string SlotB = "lkg-b.dat";
        public const string Pointer = "lkg-current";
        private const string Format = "yes2sdk-control-plane-lkg-v1";
        private const string TempSuffix = ".tmp";
        private const int ChecksumLength = 71;
        private const long MaxRecordBytes = ControlPlaneDocuments.MaxDocumentBytes + 4096;

        private readonly string _directory;
        private readonly string _gameKey;
        private readonly string _environmentKey;
        private readonly Action _beforeMutation;

        public ControlPlaneLkgJournal(string directory, string gameKey, string environmentKey, Action beforeMutation = null)
        {
            _directory = directory;
            _gameKey = gameKey;
            _environmentKey = environmentKey;
            _beforeMutation = beforeMutation ?? (() => { });
        }

        public ControlPlaneSnapshot Load()
        {
            return LoadEntry()?.Snapshot;
        }

        public void Write(ControlPlaneSnapshot snapshot)
        {
            var active = LoadEntry();
            var slot = active?.Slot == SlotA ? SlotB : SlotA;
            var generation = (active?.Generation ?? 0) + 1;

            Mutate(() => Directory.CreateDirectory(_directory));
            var temp = PathOf(slot + TempSuffix);
            WriteDurably(temp, Encode(generation, snapshot));
            if (Decode(temp) == null)
            {
                throw new IOException(slot + " did not verify after it was written");
            }
            Replace(temp, PathOf(slot));

            var pointerTemp = PathOf(Pointer + TempSuffix);
            WriteDurably(pointerTemp, Encoding.ASCII.GetBytes(slot + " " + generation));
            Replace(pointerTemp, PathOf(Pointer));
            Persist();
        }

        public void Clear()
        {
            foreach (var name in new[] { Pointer, SlotA, SlotB, Pointer + TempSuffix, SlotA + TempSuffix, SlotB + TempSuffix })
            {
                Mutate(() => File.Delete(PathOf(name)));
            }
            Persist();
        }

        private Entry LoadEntry()
        {
            if (!Directory.Exists(_directory))
            {
                return null;
            }
            var pointed = ReadPointer();
            if (pointed != null)
            {
                var entry = Decode(PathOf(pointed.Slot));
                if (entry != null && entry.Generation == pointed.Generation)
                {
                    return entry;
                }
            }
            var a = Decode(PathOf(SlotA));
            var b = Decode(PathOf(SlotB));
            if (a == null || b == null)
            {
                return a ?? b;
            }
            return b.Generation > a.Generation ? b : a;
        }

        private Entry ReadPointer()
        {
            var path = PathOf(Pointer);
            if (!File.Exists(path) || new FileInfo(path).Length > 64)
            {
                return null;
            }
            var parts = Encoding.ASCII.GetString(File.ReadAllBytes(path)).Split(' ');
            if (parts.Length != 2 || (parts[0] != SlotA && parts[0] != SlotB) || !long.TryParse(parts[1], out var generation) || generation < 1)
            {
                return null;
            }
            return new Entry(parts[0], generation, null);
        }

        private byte[] Encode(long generation, ControlPlaneSnapshot snapshot)
        {
            var bootstrap = snapshot.Bootstrap;
            byte[] body;
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8))
                {
                    writer.Write(Format);
                    writer.Write(generation);
                    writer.Write(bootstrap.GameKey);
                    writer.Write(bootstrap.EnvironmentKey);
                    writer.Write(bootstrap.DeploymentId);
                    writer.Write(bootstrap.ReleaseId);
                    writer.Write(bootstrap.SnapshotUrl);
                    writer.Write(bootstrap.SnapshotDigest);
                    writer.Write(snapshot.Bytes.Length);
                    writer.Write(snapshot.Bytes);
                    writer.Flush();
                    body = stream.ToArray();
                }
            }
            var record = new byte[body.Length + ChecksumLength];
            Buffer.BlockCopy(body, 0, record, 0, body.Length);
            Encoding.ASCII.GetBytes(ControlPlaneSha256.Digest(body), 0, ChecksumLength, record, body.Length);
            return record;
        }

        private Entry Decode(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }
            var length = new FileInfo(path).Length;
            if (length <= ChecksumLength || length > MaxRecordBytes)
            {
                return null;
            }
            var record = File.ReadAllBytes(path);
            var body = new byte[record.Length - ChecksumLength];
            Buffer.BlockCopy(record, 0, body, 0, body.Length);
            if (Encoding.ASCII.GetString(record, body.Length, ChecksumLength) != ControlPlaneSha256.Digest(body))
            {
                return null;
            }

            long generation;
            ControlPlaneBootstrap bootstrap;
            byte[] bytes;
            try
            {
                using (var reader = new BinaryReader(new MemoryStream(body), Encoding.UTF8))
                {
                    if (reader.ReadString() != Format)
                    {
                        return null;
                    }
                    generation = reader.ReadInt64();
                    bootstrap = new ControlPlaneBootstrap(reader.ReadString(), reader.ReadString(), reader.ReadString(), reader.ReadString(), reader.ReadString(), reader.ReadString());
                    var count = reader.ReadInt32();
                    if (count < 0 || count > ControlPlaneDocuments.MaxDocumentBytes)
                    {
                        return null;
                    }
                    bytes = reader.ReadBytes(count);
                    if (bytes.Length != count || reader.BaseStream.Position != body.Length)
                    {
                        return null;
                    }
                }
            }
            catch (Exception e) when (e is EndOfStreamException || e is IOException || e is FormatException || e is ArgumentException)
            {
                return null;
            }

            if (generation < 1 || bootstrap.GameKey != _gameKey || bootstrap.EnvironmentKey != _environmentKey)
            {
                return null;
            }
            var snapshot = ControlPlaneDocuments.ReadSnapshot(bytes, bootstrap);
            return snapshot.Succeeded ? new Entry(Path.GetFileName(path) == SlotB ? SlotB : SlotA, generation, snapshot.Value) : null;
        }

        private void WriteDurably(string path, byte[] bytes)
        {
            Mutate(() =>
            {
                using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
            });
        }

        private void Replace(string source, string destination)
        {
            Mutate(() => File.Delete(destination));
            Mutate(() => File.Move(source, destination));
        }

        private void Mutate(Action operation)
        {
            _beforeMutation();
            operation();
        }

        private string PathOf(string name)
        {
            return Path.Combine(_directory, name);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void JS_FileSystem_Sync();

        private static void Persist()
        {
            JS_FileSystem_Sync();
        }
#else
        private static void Persist()
        {
        }
#endif

        private sealed class Entry
        {
            public Entry(string slot, long generation, ControlPlaneSnapshot snapshot)
            {
                Slot = slot;
                Generation = generation;
                Snapshot = snapshot;
            }

            public string Slot { get; }
            public long Generation { get; }
            public ControlPlaneSnapshot Snapshot { get; }
        }
    }
}
