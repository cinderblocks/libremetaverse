namespace LibreMetaverse.RLV.Tests
{
    public class RlvBlacklistTests : RestrictionsBase
    {
        [Fact]
        public void GetBlacklist_EmptyByDefault()
        {
            // Arrange & Act
            var blacklist = _rlv.Blacklist.GetBlacklist();

            // Assert
            Assert.Empty(blacklist);
        }

        [Fact]
        public void BlacklistBehavior_ValidBehavior_AddsToBlacklist()
        {
            // Arrange
            const string behavior = "testbehavior";

            // Act
            _rlv.Blacklist.BlacklistBehavior(behavior);

            // Assert
            var blacklist = _rlv.Blacklist.GetBlacklist();
            Assert.Single(blacklist);
            Assert.Contains(behavior.ToLowerInvariant(), blacklist);
        }

        [Fact]
        public void BlacklistBehavior_CaseInsensitive_NormalizesToLowercase()
        {
            // Arrange
            const string behavior = "TestBehavior";

            // Act
            _rlv.Blacklist.BlacklistBehavior(behavior);

            // Assert
            var blacklist = _rlv.Blacklist.GetBlacklist();
            Assert.Single(blacklist);
            Assert.Contains("testbehavior", blacklist);
            Assert.DoesNotContain("TestBehavior", blacklist);
        }

        [Fact]
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
            Assert.Single(blacklist);
            Assert.Contains(behavior.ToLowerInvariant(), blacklist);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        [InlineData("\n")]
        public void BlacklistBehavior_NullOrWhitespaceBehavior_ThrowsArgumentException(string behavior)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _rlv.Blacklist.BlacklistBehavior(behavior));
            Assert.Equal("behavior", exception.ParamName);
        }

        [Fact]
        public void UnBlacklistBehavior_ExistingBehavior_RemovesFromBlacklist()
        {
            // Arrange
            const string behavior = "testbehavior";
            _rlv.Blacklist.BlacklistBehavior(behavior);

            // Act
            _rlv.Blacklist.UnBlacklistBehavior(behavior);

            // Assert
            var blacklist = _rlv.Blacklist.GetBlacklist();
            Assert.Empty(blacklist);
        }

        [Fact]
        public void UnBlacklistBehavior_CaseInsensitive_RemovesCorrectBehavior()
        {
            // Arrange
            const string behavior = "TestBehavior";
            _rlv.Blacklist.BlacklistBehavior(behavior);

            // Act
            _rlv.Blacklist.UnBlacklistBehavior("testbehavior");

            // Assert
            var blacklist = _rlv.Blacklist.GetBlacklist();
            Assert.Empty(blacklist);
        }

        [Fact]
        public void UnBlacklistBehavior_NonExistentBehavior_DoesNothing()
        {
            // Arrange
            _rlv.Blacklist.BlacklistBehavior("existingbehavior");

            // Act
            _rlv.Blacklist.UnBlacklistBehavior("nonexistentbehavior");

            // Assert
            var blacklist = _rlv.Blacklist.GetBlacklist();
            Assert.Single(blacklist);
            Assert.Contains("existingbehavior", blacklist);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        [InlineData("\n")]
        public void UnBlacklistBehavior_NullOrWhitespaceBehavior_ThrowsArgumentException(string behavior)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _rlv.Blacklist.UnBlacklistBehavior(behavior));
            Assert.Equal("behavior", exception.ParamName);
        }

        [Fact]
        public void IsBlacklisted_BlacklistedBehavior_ReturnsTrue()
        {
            // Arrange
            const string behavior = "testbehavior";
            _rlv.Blacklist.BlacklistBehavior(behavior);

            // Act & Assert
            Assert.True(_rlv.Blacklist.IsBlacklisted(behavior));
        }

        [Fact]
        public void IsBlacklisted_CaseInsensitive_ReturnsTrue()
        {
            // Arrange
            const string behavior = "TestBehavior";
            _rlv.Blacklist.BlacklistBehavior(behavior);

            // Act & Assert
            Assert.True(_rlv.Blacklist.IsBlacklisted("testbehavior"));
            Assert.True(_rlv.Blacklist.IsBlacklisted("TESTBEHAVIOR"));
            Assert.True(_rlv.Blacklist.IsBlacklisted("TestBehavior"));
        }

        [Fact]
        public void IsBlacklisted_NonBlacklistedBehavior_ReturnsFalse()
        {
            // Arrange
            _rlv.Blacklist.BlacklistBehavior("existingbehavior");

            // Act & Assert
            Assert.False(_rlv.Blacklist.IsBlacklisted("nonexistentbehavior"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        [InlineData("\n")]
        public void IsBlacklisted_NullOrWhitespaceBehavior_ReturnsFalse(string behavior)
        {
            // Act & Assert
            Assert.False(_rlv.Blacklist.IsBlacklisted(behavior));
        }

        [Fact]
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
            Assert.Equal(4, blacklist.Count);
            var orderedBehaviors = behaviors.OrderBy(x => x).ToArray();
            Assert.Equal(orderedBehaviors, blacklist.ToArray());
        }

        [Fact]
        public void GetBlacklist_ReturnsImmutableCopy()
        {
            // Arrange
            _rlv.Blacklist.BlacklistBehavior("testbehavior");
            var blacklist1 = _rlv.Blacklist.GetBlacklist();

            // Act
            _rlv.Blacklist.BlacklistBehavior("anotherbehavior");
            var blacklist2 = _rlv.Blacklist.GetBlacklist();

            // Assert
            Assert.Single(blacklist1);
            Assert.Equal(2, blacklist2.Count);
            Assert.Contains("testbehavior", blacklist1);
            Assert.DoesNotContain("anotherbehavior", blacklist1);
        }

        [Fact]
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
                Assert.True(_rlv.Blacklist.IsBlacklisted(behavior));
            }

            var blacklist = _rlv.Blacklist.GetBlacklist();
            Assert.Equal(4, blacklist.Count);

            // Act - Remove some behaviors
            _rlv.Blacklist.UnBlacklistBehavior("sendim");
            _rlv.Blacklist.UnBlacklistBehavior("fly");

            // Assert - Only remaining behaviors are blacklisted
            Assert.False(_rlv.Blacklist.IsBlacklisted("sendim"));
            Assert.True(_rlv.Blacklist.IsBlacklisted("recvim"));
            Assert.True(_rlv.Blacklist.IsBlacklisted("tpto"));
            Assert.False(_rlv.Blacklist.IsBlacklisted("fly"));

            var updatedBlacklist = _rlv.Blacklist.GetBlacklist();
            Assert.Equal(2, updatedBlacklist.Count);
            Assert.Contains("recvim", updatedBlacklist);
            Assert.Contains("tpto", updatedBlacklist);
        }

        [Fact]
        public void SeedBlacklist_IntegrationWithRestrictionsBase_WorksCorrectly()
        {
            // Arrange & Act
            SeedBlacklist("sendim,recvim,tpto");

            // Assert
            Assert.True(_rlv.Blacklist.IsBlacklisted("sendim"));
            Assert.True(_rlv.Blacklist.IsBlacklisted("recvim"));
            Assert.True(_rlv.Blacklist.IsBlacklisted("tpto"));
            Assert.False(_rlv.Blacklist.IsBlacklisted("fly"));

            var blacklist = _rlv.Blacklist.GetBlacklist();
            Assert.Equal(3, blacklist.Count);
        }

        [Fact]
        public void SeedBlacklist_EmptyString_ResultsInEmptyBlacklist()
        {
            // Arrange & Act
            SeedBlacklist("");

            // Assert
            var blacklist = _rlv.Blacklist.GetBlacklist();
            Assert.Empty(blacklist);
        }

        [Fact]
        public void SeedBlacklist_WithSpaces_HandlesCorrectly()
        {
            // Arrange & Act
            SeedBlacklist("sendim, recvim , tpto");

            // Assert
            Assert.True(_rlv.Blacklist.IsBlacklisted("sendim"));
            Assert.True(_rlv.Blacklist.IsBlacklisted("recvim"));
            Assert.True(_rlv.Blacklist.IsBlacklisted("tpto"));

            var blacklist = _rlv.Blacklist.GetBlacklist();
            Assert.Equal(3, blacklist.Count);
        }
    }
}
