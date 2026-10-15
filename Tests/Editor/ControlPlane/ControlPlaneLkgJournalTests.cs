using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace Yes2SDK.Tests
{
    public class ControlPlaneLkgJournalTests
    {
        private static readonly ControlPlaneSnapshot First = ControlPlaneFixture.Deployment("dep-1", "rel-1");
        private static readonly ControlPlaneSnapshot Second = ControlPlaneFixture.Deployment("dep-2", "rel-2");
        private static readonly ControlPlaneSnapshot Third = ControlPlaneFixture.Deployment("dep-3", "rel-3");

        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "yes2sdk-lkg-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        private ControlPlaneLkgJournal Journal(Action beforeMutation = null)
        {
            return new ControlPlaneLkgJournal(_directory, ControlPlaneFixture.Game, ControlPlaneFixture.Environment, beforeMutation);
        }

        private string FileOf(string name)
        {
            return Path.Combine(_directory, name);
        }

        private string Pointer()
        {
            return File.ReadAllText(FileOf(ControlPlaneLkgJournal.Pointer));
        }

        private static string DeploymentOf(ControlPlaneSnapshot snapshot)
        {
            return snapshot?.Bootstrap.DeploymentId;
        }

        private void Corrupt(string name, Func<byte[], byte[]> change)
        {
            File.WriteAllBytes(FileOf(name), change(File.ReadAllBytes(FileOf(name))));
        }

        private static byte[] FlipLastSnapshotByte(byte[] record)
        {
            record[record.Length - 72] ^= 0x01;
            return record;
        }

        [Test]
        public void Load_ReturnsNothingWithoutDirectory()
        {
            Assert.IsNull(Journal().Load());
        }

        [Test]
        public void Write_RoundTripsIdentityAndExactBytes()
        {
            Journal().Write(First);

            var loaded = Journal().Load();

            Assert.AreEqual("dep-1", loaded.Bootstrap.DeploymentId);
            Assert.AreEqual("rel-1", loaded.Bootstrap.ReleaseId);
            Assert.AreEqual(ControlPlaneFixture.SnapshotUrl, loaded.Bootstrap.SnapshotUrl);
            Assert.AreEqual(First.Bootstrap.SnapshotDigest, loaded.Bootstrap.SnapshotDigest);
            CollectionAssert.AreEqual(First.Bytes, loaded.Bytes);
        }

        [Test]
        public void Write_AlternatesSlotsAndWritesPointerLast()
        {
            var journal = Journal();

            journal.Write(First);
            Assert.AreEqual("lkg-a.dat 1", Pointer());
            journal.Write(Second);
            Assert.AreEqual("lkg-b.dat 2", Pointer());
            journal.Write(Third);
            Assert.AreEqual("lkg-a.dat 3", Pointer());

            Assert.AreEqual("dep-3", DeploymentOf(Journal().Load()));
            CollectionAssert.AreEquivalent(
                new[] { "lkg-a.dat", "lkg-b.dat", "lkg-current" },
                Array.ConvertAll(Directory.GetFiles(_directory), Path.GetFileName));
        }

        [Test]
        public void Load_RecoversFromCorruptPointedSlot()
        {
            Journal().Write(First);
            Journal().Write(Second);
            Corrupt(ControlPlaneLkgJournal.SlotB, FlipLastSnapshotByte);

            Assert.AreEqual("dep-1", DeploymentOf(Journal().Load()));
        }

        [Test]
        public void Load_RecoversFromTruncatedPointedSlot()
        {
            Journal().Write(First);
            Journal().Write(Second);
            Corrupt(ControlPlaneLkgJournal.SlotB, record =>
            {
                Array.Resize(ref record, record.Length / 2);
                return record;
            });

            Assert.AreEqual("dep-1", DeploymentOf(Journal().Load()));
        }

        [Test]
        public void Load_RecoversFromEmptyPointedSlot()
        {
            Journal().Write(First);
            Journal().Write(Second);
            File.WriteAllBytes(FileOf(ControlPlaneLkgJournal.SlotB), new byte[0]);

            Assert.AreEqual("dep-1", DeploymentOf(Journal().Load()));
        }

        [TestCase("")]
        [TestCase("garbage")]
        [TestCase("lkg-c.dat 2")]
        [TestCase("lkg-a.dat 0")]
        [TestCase("lkg-a.dat 2")]
        public void Load_ScansForHighestValidGenerationWhenPointerIsInvalid(string pointer)
        {
            Journal().Write(First);
            Journal().Write(Second);
            File.WriteAllText(FileOf(ControlPlaneLkgJournal.Pointer), pointer);

            Assert.AreEqual("dep-2", DeploymentOf(Journal().Load()));
        }

        [Test]
        public void Load_ScansWhenPointerIsMissing()
        {
            Journal().Write(First);
            Journal().Write(Second);
            File.Delete(FileOf(ControlPlaneLkgJournal.Pointer));

            Assert.AreEqual("dep-2", DeploymentOf(Journal().Load()));
        }

        [Test]
        public void Load_ReturnsNothingWhenBothSlotsAreCorrupt()
        {
            Journal().Write(First);
            Journal().Write(Second);
            Corrupt(ControlPlaneLkgJournal.SlotA, FlipLastSnapshotByte);
            Corrupt(ControlPlaneLkgJournal.SlotB, FlipLastSnapshotByte);

            Assert.IsNull(Journal().Load());
        }

        [Test]
        public void Load_RejectsRecordOfAnotherEnvironment()
        {
            Journal().Write(First);

            Assert.IsNull(new ControlPlaneLkgJournal(_directory, ControlPlaneFixture.Game, "staging").Load());
        }

        [Test]
        public void Load_IgnoresLeftoverTempFiles()
        {
            Journal().Write(First);
            File.WriteAllBytes(FileOf("lkg-b.dat.tmp"), new byte[] { 1, 2, 3 });
            File.WriteAllText(FileOf("lkg-current.tmp"), "lkg-b.dat 2");

            Assert.AreEqual("dep-1", DeploymentOf(Journal().Load()));
            Journal().Write(Second);
            Assert.AreEqual("dep-2", DeploymentOf(Journal().Load()));
        }

        [Test]
        public void Write_RefusesRecordThatDoesNotVerifyAndKeepsPrevious()
        {
            Journal().Write(First);
            var mismatched = new ControlPlaneSnapshot(Second.Bootstrap, First.Bytes, First.Root);

            Assert.Throws<IOException>(() => Journal().Write(mismatched));

            Assert.AreEqual("dep-1", DeploymentOf(Journal().Load()));
            Assert.AreEqual("lkg-a.dat 1", Pointer());
        }

        [Test]
        public void Write_FailsWhenDirectoryIsUnavailable()
        {
            File.WriteAllText(_directory, "not a directory");
            try
            {
                Assert.Throws<IOException>(() => Journal().Write(First));
            }
            finally
            {
                File.Delete(_directory);
            }
        }

        [Test]
        public void Clear_RemovesEveryJournalFile()
        {
            Journal().Write(First);
            Journal().Write(Second);
            File.WriteAllText(FileOf("lkg-a.dat.tmp"), "partial");

            Journal().Clear();

            Assert.IsEmpty(Directory.GetFiles(_directory));
            Assert.IsNull(Journal().Load());
        }

        private static int MutationsPerWrite(Action<ControlPlaneLkgJournal> arrange, string directory)
        {
            var count = 0;
            var journal = new ControlPlaneLkgJournal(directory, ControlPlaneFixture.Game, ControlPlaneFixture.Environment, () => count++);
            arrange(journal);
            count = 0;
            journal.Write(Second);
            Directory.Delete(directory, true);
            return count;
        }

        private void KillEveryMutation(Action<ControlPlaneLkgJournal> arrange, string before, int committedFrom)
        {
            var mutations = MutationsPerWrite(arrange, _directory);
            Assert.AreEqual(7, mutations);

            for (var kill = 0; kill < mutations; kill++)
            {
                if (Directory.Exists(_directory))
                {
                    Directory.Delete(_directory, true);
                }
                arrange(Journal());
                var count = 0;
                var killAt = kill;
                var crashing = Journal(() =>
                {
                    if (count++ == killAt)
                    {
                        throw new KilledException();
                    }
                });

                Assert.Throws<KilledException>(() => crashing.Write(Second), "kill before mutation " + kill);

                var recovered = DeploymentOf(Journal().Load());
                var committed = kill >= committedFrom;
                Assert.AreEqual(committed ? "dep-2" : before, recovered, "kill before mutation " + kill);

                Journal().Write(Third);
                Assert.AreEqual("dep-3", DeploymentOf(Journal().Load()), "write after kill before mutation " + kill);
            }
        }

        [Test]
        public void Write_KilledBeforeEveryMutationOverEmptyJournalLeavesNothingOrNew()
        {
            KillEveryMutation(_ => { }, null, 4);
        }

        [Test]
        public void Write_KilledBeforeEveryMutationOverOneSlotLeavesOldOrNew()
        {
            KillEveryMutation(journal => journal.Write(First), "dep-1", 6);
        }

        [Test]
        public void Write_KilledBeforeEveryMutationOverTwoSlotsLeavesOldOrNew()
        {
            KillEveryMutation(journal =>
            {
                journal.Write(Third);
                journal.Write(First);
            }, "dep-1", 6);
        }

        [Test]
        public void Write_TornTempSlotNeverReplacesTheActiveSlot()
        {
            Journal().Write(First);
            var bytesWritten = 0;
            var crashing = Journal(() =>
            {
                if (++bytesWritten == 2)
                {
                    File.WriteAllBytes(FileOf("lkg-b.dat.tmp"), Encoding.ASCII.GetBytes("torn"));
                    throw new KilledException();
                }
            });

            Assert.Throws<KilledException>(() => crashing.Write(Second));

            Assert.AreEqual("dep-1", DeploymentOf(Journal().Load()));
        }

        private sealed class KilledException : Exception
        {
        }
    }
}
