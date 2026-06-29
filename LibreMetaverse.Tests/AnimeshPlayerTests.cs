using System.Collections.Generic;
using NUnit.Framework;
using LibreMetaverse.Animesh;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    [Category("Animesh")]
    public class AnimeshPlayerTests
    {
        // ── Track management ──────────────────────────────────────────────────

        [Test]
        public void GetOrAddTrack_CreatesNewTrack()
        {
            var player = new AnimeshPlayer(UUID.Random());
            var id = UUID.Random();
            var track = player.GetOrAddTrack(id);
            Assert.That(track, Is.Not.Null);
            Assert.That(track.AnimationID, Is.EqualTo(id));
        }

        [Test]
        public void GetOrAddTrack_SameId_ReturnsSameTrack()
        {
            var player = new AnimeshPlayer(UUID.Random());
            var id = UUID.Random();
            var t1 = player.GetOrAddTrack(id);
            var t2 = player.GetOrAddTrack(id);
            Assert.That(t2, Is.SameAs(t1), "Second call must return the existing track");
        }

        [Test]
        public void GetOrAddTrack_DifferentIds_ReturnDifferentTracks()
        {
            var player = new AnimeshPlayer(UUID.Random());
            var a = player.GetOrAddTrack(UUID.Random());
            var b = player.GetOrAddTrack(UUID.Random());
            Assert.That(b, Is.Not.SameAs(a));
        }

        [Test]
        public void TrackCount_IncreasesWithNewTracks()
        {
            var player = new AnimeshPlayer(UUID.Random());
            player.GetOrAddTrack(UUID.Random());
            player.GetOrAddTrack(UUID.Random());
            Assert.That(player.TrackCount, Is.EqualTo(2));
        }

        [Test]
        public void TrackCount_StableForDuplicateId()
        {
            var player = new AnimeshPlayer(UUID.Random());
            var id = UUID.Random();
            player.GetOrAddTrack(id);
            player.GetOrAddTrack(id);
            Assert.That(player.TrackCount, Is.EqualTo(1));
        }

        [Test]
        public void RetainOnly_RemovesAbsentIds()
        {
            var player = new AnimeshPlayer(UUID.Random());
            var keep = UUID.Random();
            var drop = UUID.Random();
            player.GetOrAddTrack(keep);
            player.GetOrAddTrack(drop);

            player.RetainOnly(new HashSet<UUID> { keep });

            Assert.That(player.TrackCount, Is.EqualTo(1));
        }

        [Test]
        public void RetainOnly_KeepsPresentIds()
        {
            var player = new AnimeshPlayer(UUID.Random());
            var id = UUID.Random();
            var original = player.GetOrAddTrack(id);

            player.RetainOnly(new HashSet<UUID> { id });

            var still = player.GetOrAddTrack(id); // should return the same object
            Assert.That(still, Is.SameAs(original));
        }

        [Test]
        public void RetainOnly_EmptySet_RemovesAllTracks()
        {
            var player = new AnimeshPlayer(UUID.Random());
            player.GetOrAddTrack(UUID.Random());
            player.GetOrAddTrack(UUID.Random());

            player.RetainOnly(new HashSet<UUID>());

            Assert.That(player.TrackCount, Is.EqualTo(0));
        }

        // ── Playback ──────────────────────────────────────────────────────────

        [Test]
        public void Update_WithNoTracks_DoesNotThrow()
        {
            var player = new AnimeshPlayer(UUID.Random());
            Assert.DoesNotThrow(() => player.Update(0.016f));
        }

        [Test]
        public void Update_WithTracksButNoData_DoesNotThrow()
        {
            var player = new AnimeshPlayer(UUID.Random());
            player.GetOrAddTrack(UUID.Random());
            player.GetOrAddTrack(UUID.Random());
            Assert.DoesNotThrow(() => player.Update(0.016f));
        }

        [Test]
        public void EvaluatePose_NoLoadedData_ReturnsEmptyDictionary()
        {
            var player = new AnimeshPlayer(UUID.Random());
            player.GetOrAddTrack(UUID.Random()); // track exists but Data == null
            var pose = player.EvaluatePose();
            Assert.That(pose, Is.Empty);
        }

        // ── ObjectID ─────────────────────────────────────────────────────────

        [Test]
        public void ObjectID_StoresConstructorValue()
        {
            var id = UUID.Random();
            var player = new AnimeshPlayer(id);
            Assert.That(player.ObjectID, Is.EqualTo(id));
        }
    }
}
