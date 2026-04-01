using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using Moq;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV
{
    [TestFixture]
    public class RlvBlacklistTests : RlvTestBase
    {
        [Test]
        public void GetBlacklist_EmptyByDefault()
        {
            // Arrange & Act
            var blacklist = _rlv.Blacklist.GetBlacklist();

            // Assert
            Assert.That(blacklist, Is.Empty);
        }

        [Test]
        public void BlacklistBehavior_ValidBehavior_AddsToBlacklist()
        {
            // Arrange
            const string behavior = "testbehavior";

            // Act
            _rlv.Blacklist.BlacklistBehavior(behavior);

            // Assert
            var blacklist = _rlv.Blacklist.GetBlacklist();
            Assert.That(blacklist, Has.Count.EqualTo(1));
            Assert.That(blacklist, Does.Contain(behavior.ToLowerInvariant()));
        }

        [Test]
        public void BlacklistBehavior_CaseInsensitive_NormalizesToLowercase()
        {
            // Arrange
            const string behavior = "TestBehavior";

            // Act
            _rlv.Blacklist.BlacklistBehavior(behavior);

            // Assert
            var blacklist = _rlv.Blacklist.GetBlacklist();
            Assert.That(blacklist, Has.Count.EqualTo(1));
            Assert.That(blacklist, Does.Contain("testbehavior"));
            Assert.That(blacklist, Does.Not.Contain("TestBehavior"));
        }

        [Test]
        public void BlacklistBehavior_DuplicateBehavior_OnlyAddsOnce()
        {
            // Arrange
            const string behavior = "testbehavior";

            // Act
            _rlv.Blacklist.BlacklistBehavior(behavior);
            _rlv.Blacklist.BlacklistBehavior(behavior);
            _rlv.Blacklist.BlacklistBehavior(behavior.ToUpperInvariant());

            // Assert
            var blacklist = _rlv.Blacklist.GetBlacklist();
            Assert.That(blacklist, Has.Count.EqualTo(1));
            Assert.That(blacklist, Does.Contain(behavior.ToLowerInvariant()));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("\t")]
        [TestCase("\n")]
        public void BlacklistBehavior_NullOrWhitespaceBehavior_ThrowsArgumentException(string behavior)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _rlv.Blacklist.BlacklistBehavior(behavior));
            Assert.That(exception.ParamName, Is.EqualTo("behavior"));
        }

        [Test]
        public void UnBlacklistBehavior_ExistingBehavior_RemovesFromBlacklist()
        {
            // Arrange
            const string behavior = "testbehavior";
            _rlv.Blacklist.BlacklistBehavior(behavior);

            // Act
            _rlv.Blacklist.UnBlacklistBehavior(behavior);

            // Assert
            var blacklist = _rlv.Blacklist.GetBlacklist();
            Assert.That(blacklist, Is.Empty);
        }

        [Test]
        public void UnBlacklistBehavior_CaseInsensitive_RemovesCorrectBehavior()
        {
            // Arrange
            const string behavior = "TestBehavior";
            _rlv.Blacklist.BlacklistBehavior(behavior);

            // Act
            _rlv.Blacklist.UnBlacklistBehavior("testbehavior");

            // Assert
            var blacklist = _rlv.Blacklist.GetBlacklist();
            Assert.That(blacklist, Is.Empty);
        }

        [Test]
        public void UnBlacklistBehavior_NonExistentBehavior_DoesNothing()
        {
            // Arrange
            _rlv.Blacklist.BlacklistBehavior("existingbehavior");

            // Act
            _rlv.Blacklist.UnBlacklistBehavior("nonexistentbehavior");

            // Assert
            var blacklist = _rlv.Blacklist.GetBlacklist();
            Assert.That(blacklist, Has.Count.EqualTo(1));
            Assert.That(blacklist, Does.Contain("existingbehavior"));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("\t")]
        [TestCase("\n")]
        public void UnBlacklistBehavior_NullOrWhitespaceBehavior_ThrowsArgumentException(string behavior)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _rlv.Blacklist.UnBlacklistBehavior(behavior));
            Assert.That(exception.ParamName, Is.EqualTo("behavior"));
        }

        [Test]
        public void IsBlacklisted_BlacklistedBehavior_ReturnsTrue()
        {
            // Arrange
            const string behavior = "testbehavior";
            _rlv.Blacklist.BlacklistBehavior(behavior);

            // Act & Assert
            Assert.That(_rlv.Blacklist.IsBlacklisted(behavior), Is.True);
        }

        [Test]
        public void IsBlacklisted_CaseInsensitive_ReturnsTrue()
        {
            // Arrange
            const string behavior = "TestBehavior";
            _rlv.Blacklist.BlacklistBehavior(behavior);

            // Act & Assert
            Assert.That(_rlv.Blacklist.IsBlacklisted("testbehavior"), Is.True);
            Assert.That(_rlv.Blacklist.IsBlacklisted("TESTBEHAVIOR"), Is.True);
            Assert.That(_rlv.Blacklist.IsBlacklisted("TestBehavior"), Is.True);
        }

        [Test]
        public void IsBlacklisted_NonBlacklistedBehavior_ReturnsFalse()
        {
            // Arrange
            _rlv.Blacklist.BlacklistBehavior("existingbehavior");

            // Act & Assert
            Assert.That(_rlv.Blacklist.IsBlacklisted("nonexistentbehavior"), Is.False);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("\t")]
        [TestCase("\n")]
        public void IsBlacklisted_NullOrWhitespaceBehavior_ReturnsFalse(string behavior)
        {
            // Act & Assert
            Assert.That(_rlv.Blacklist.IsBlacklisted(behavior), Is.False);
        }

        [Test]
        public void GetBlacklist_MultipleItems_ReturnsOrderedCollection()
        {
            // Arrange
            var behaviors = new[] { "zebra", "alpha", "beta", "charlie" };
            foreach (var behavior in behaviors)
            {
                _rlv.Blacklist.BlacklistBehavior(behavior);
            }

            // Act
            var blacklist = _rlv.Blacklist.GetBlacklist();

            // Assert
            Assert.That(blacklist.Count, Is.EqualTo(4));
            var orderedBehaviors = behaviors.OrderBy(x => x).ToArray();
            Assert.That(blacklist.ToArray(), Is.EqualTo(orderedBehaviors));
        }

        [Test]
        public void GetBlacklist_ReturnsImmutableCopy()
        {
            // Arrange
            _rlv.Blacklist.BlacklistBehavior("testbehavior");
            var blacklist1 = _rlv.Blacklist.GetBlacklist();

            // Act
            _rlv.Blacklist.BlacklistBehavior("anotherbehavior");
            var blacklist2 = _rlv.Blacklist.GetBlacklist();

            // Assert
            Assert.That(blacklist1, Has.Count.EqualTo(1));
            Assert.That(blacklist2.Count, Is.EqualTo(2));
            Assert.That(blacklist1, Does.Contain("testbehavior"));
            Assert.That(blacklist1, Does.Not.Contain("anotherbehavior"));
        }

        [Test]
        public void BlacklistWorkflow_AddMultipleBehaviors_WorksCorrectly()
        {
            // Arrange
            var behaviors = new[] { "sendim", "recvim", "tpto", "fly" };

            // Act - Add behaviors
            foreach (var behavior in behaviors)
            {
                _rlv.Blacklist.BlacklistBehavior(behavior);
            }

            // Assert - All behaviors are blacklisted
            foreach (var behavior in behaviors)
            {
                Assert.That(_rlv.Blacklist.IsBlacklisted(behavior), Is.True);
            }

            var blacklist = _rlv.Blacklist.GetBlacklist();
            Assert.That(blacklist.Count, Is.EqualTo(4));

            // Act - Remove some behaviors
            _rlv.Blacklist.UnBlacklistBehavior("sendim");
            _rlv.Blacklist.UnBlacklistBehavior("fly");

            // Assert - Only remaining behaviors are blacklisted
            Assert.That(_rlv.Blacklist.IsBlacklisted("sendim"), Is.False);
            Assert.That(_rlv.Blacklist.IsBlacklisted("recvim"), Is.True);
            Assert.That(_rlv.Blacklist.IsBlacklisted("tpto"), Is.True);
            Assert.That(_rlv.Blacklist.IsBlacklisted("fly"), Is.False);

            var updatedBlacklist = _rlv.Blacklist.GetBlacklist();
            Assert.That(updatedBlacklist.Count, Is.EqualTo(2));
            Assert.That(updatedBlacklist, Does.Contain("recvim"));
            Assert.That(updatedBlacklist, Does.Contain("tpto"));
        }

        [Test]
        public void SeedBlacklist_IntegrationWithRestrictionsBase_WorksCorrectly()
        {
            // Arrange & Act
            SeedBlacklist("sendim,recvim,tpto");

            // Assert
            Assert.That(_rlv.Blacklist.IsBlacklisted("sendim"), Is.True);
            Assert.That(_rlv.Blacklist.IsBlacklisted("recvim"), Is.True);
            Assert.That(_rlv.Blacklist.IsBlacklisted("tpto"), Is.True);
            Assert.That(_rlv.Blacklist.IsBlacklisted("fly"), Is.False);

            var blacklist = _rlv.Blacklist.GetBlacklist();
            Assert.That(blacklist.Count, Is.EqualTo(3));
        }

        [Test]
        public void SeedBlacklist_EmptyString_ResultsInEmptyBlacklist()
        {
            // Arrange & Act
            SeedBlacklist("");

            // Assert
            var blacklist = _rlv.Blacklist.GetBlacklist();
            Assert.That(blacklist, Is.Empty);
        }

        [Test]
        public void SeedBlacklist_WithSpaces_HandlesCorrectly()
        {
            // Arrange & Act
            SeedBlacklist("sendim, recvim , tpto");

            // Assert
            Assert.That(_rlv.Blacklist.IsBlacklisted("sendim"), Is.True);
            Assert.That(_rlv.Blacklist.IsBlacklisted("recvim"), Is.True);
            Assert.That(_rlv.Blacklist.IsBlacklisted("tpto"), Is.True);

            var blacklist = _rlv.Blacklist.GetBlacklist();
            Assert.That(blacklist.Count, Is.EqualTo(3));
        }
    }
}
