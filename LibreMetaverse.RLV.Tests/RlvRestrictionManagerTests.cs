using Moq;

namespace LibreMetaverse.RLV.Tests
{
    public class RlvRestrictionManagerTests : RestrictionsBase
    {
        #region GetTrackedPrimIds Tests

        [Fact]
        public void GetTrackedPrimIds_NoRestrictions_ReturnsEmptyCollection()
        {
            // Act
            var trackedIds = _rlv.Restrictions.GetTrackedPrimIds();

            // Assert
            Assert.Empty(trackedIds);
        }

        [Fact]
        public async Task GetTrackedPrimIds_SingleRestriction_ReturnsSingleId()
        {
            // Arrange
            var primId = new Guid("11111111-1111-1111-1111-111111111111");

            // Act
            await _rlv.ProcessMessage("@fly=n", primId, "TestObject");
            var trackedIds = _rlv.Restrictions.GetTrackedPrimIds();

            // Assert
            Assert.Single(trackedIds);
            Assert.Contains(primId, trackedIds);
        }

        [Fact]
        public async Task GetTrackedPrimIds_MultipleRestrictionsFromSameObject_ReturnsSingleId()
        {
            // Arrange
            var primId = new Guid("11111111-1111-1111-1111-111111111111");

            // Act
            await _rlv.ProcessMessage("@fly=n", primId, "TestObject");
            await _rlv.ProcessMessage("@jump=n", primId, "TestObject");
            await _rlv.ProcessMessage("@sit=n", primId, "TestObject");
            var trackedIds = _rlv.Restrictions.GetTrackedPrimIds();

            // Assert
            Assert.Single(trackedIds);
            Assert.Contains(primId, trackedIds);
        }

        [Fact]
        public async Task GetTrackedPrimIds_MultipleObjects_ReturnsAllUniqueIds()
        {
            // Arrange
            var primId1 = new Guid("11111111-1111-1111-1111-111111111111");
            var primId2 = new Guid("22222222-2222-2222-2222-222222222222");
            var primId3 = new Guid("33333333-3333-3333-3333-333333333333");

            // Act
            await _rlv.ProcessMessage("@fly=n", primId1, "TestObject1");
            await _rlv.ProcessMessage("@jump=n", primId2, "TestObject2");
            await _rlv.ProcessMessage("@sit=n", primId3, "TestObject3");
            var trackedIds = _rlv.Restrictions.GetTrackedPrimIds();

            // Assert
            Assert.Equal(3, trackedIds.Count());
            Assert.Contains(primId1, trackedIds);
            Assert.Contains(primId2, trackedIds);
            Assert.Contains(primId3, trackedIds);
        }

        [Fact]
        public async Task GetTrackedPrimIds_MixedRestrictionsAndExceptions_ReturnsAllUniqueIds()
        {
            // Arrange
            var primId1 = new Guid("11111111-1111-1111-1111-111111111111");
            var primId2 = new Guid("22222222-2222-2222-2222-222222222222");

            // Act
            await _rlv.ProcessMessage("@detachthis=n", primId1, "TestObject1");
            await _rlv.ProcessMessage("@detachthis_except:folder=n", primId2, "TestObject2");
            var trackedIds = _rlv.Restrictions.GetTrackedPrimIds();

            // Assert
            Assert.Equal(2, trackedIds.Count());
            Assert.Contains(primId1, trackedIds);
            Assert.Contains(primId2, trackedIds);
        }

        [Fact]
        public async Task GetTrackedPrimIds_AfterRemovingRestriction_DoesNotReturnRemovedObjectId()
        {
            // Arrange
            var primId1 = new Guid("11111111-1111-1111-1111-111111111111");
            var primId2 = new Guid("22222222-2222-2222-2222-222222222222");

            // Act
            await _rlv.ProcessMessage("@fly=n", primId1, "TestObject1");
            await _rlv.ProcessMessage("@jump=n", primId2, "TestObject2");
            await _rlv.ProcessMessage("@fly=y", primId1, "TestObject1");
            var trackedIds = _rlv.Restrictions.GetTrackedPrimIds();

            // Assert
            Assert.Single(trackedIds);
            Assert.Contains(primId2, trackedIds);
            Assert.DoesNotContain(primId1, trackedIds);
        }

        [Fact]
        public async Task GetTrackedPrimIds_AfterClearingAllRestrictions_ReturnsEmptyCollection()
        {
            // Arrange
            var primId = new Guid("11111111-1111-1111-1111-111111111111");

            // Act
            await _rlv.ProcessMessage("@fly=n", primId, "TestObject");
            await _rlv.ProcessMessage("@jump=n", primId, "TestObject");
            await _rlv.ProcessMessage("@clear", primId, "TestObject");
            var trackedIds = _rlv.Restrictions.GetTrackedPrimIds();

            // Assert
            Assert.Empty(trackedIds);
        }

        #endregion

        #region RemoveRestrictionsForObjects Tests

        [Fact]
        public async Task RemoveRestrictionsForObjects_EmptyInput_NoEffect()
        {
            // Arrange
            var primId = new Guid("11111111-1111-1111-1111-111111111111");
            await _rlv.ProcessMessage("@fly=n", primId, "TestObject");

            // Act
            await _rlv.Restrictions.RemoveRestrictionsForObjects(Array.Empty<Guid>());

            // Assert
            Assert.False(_rlv.Permissions.CanFly());
            var trackedIds = _rlv.Restrictions.GetTrackedPrimIds();
            Assert.Single(trackedIds);
        }

        [Fact]
        public async Task RemoveRestrictionsForObjects_NonExistentId_NoEffect()
        {
            // Arrange
            var primId = new Guid("11111111-1111-1111-1111-111111111111");
            var nonExistentId = new Guid("99999999-9999-9999-9999-999999999999");
            await _rlv.ProcessMessage("@fly=n", primId, "TestObject");

            // Act
            await _rlv.Restrictions.RemoveRestrictionsForObjects(new[] { nonExistentId });

            // Assert
            Assert.False(_rlv.Permissions.CanFly());
            var trackedIds = _rlv.Restrictions.GetTrackedPrimIds();
            Assert.Single(trackedIds);
            Assert.Contains(primId, trackedIds);
        }

        [Fact]
        public async Task RemoveRestrictionsForObjects_SingleObject_RemovesAllRestrictionsFromObject()
        {
            // Arrange
            var primId = new Guid("11111111-1111-1111-1111-111111111111");
            await _rlv.ProcessMessage("@fly=n", primId, "TestObject");
            await _rlv.ProcessMessage("@jump=n", primId, "TestObject");
            await _rlv.ProcessMessage("@sit=n", primId, "TestObject");

            // Verify restrictions are in place
            Assert.False(_rlv.Permissions.CanFly());
            Assert.False(_rlv.Permissions.CanJump());

            // Act
            await _rlv.Restrictions.RemoveRestrictionsForObjects(new[] { primId });

            // Assert
            Assert.True(_rlv.Permissions.CanFly());
            Assert.True(_rlv.Permissions.CanJump());
            var trackedIds = _rlv.Restrictions.GetTrackedPrimIds();
            Assert.Empty(trackedIds);
        }

        [Fact]
        public async Task RemoveRestrictionsForObjects_MultipleObjects_RemovesOnlySpecifiedObjects()
        {
            // Arrange
            var primId1 = new Guid("11111111-1111-1111-1111-111111111111");
            var primId2 = new Guid("22222222-2222-2222-2222-222222222222");
            var primId3 = new Guid("33333333-3333-3333-3333-333333333333");

            await _rlv.ProcessMessage("@fly=n", primId1, "TestObject1");
            await _rlv.ProcessMessage("@jump=n", primId2, "TestObject2");
            await _rlv.ProcessMessage("@sit=n", primId3, "TestObject3");

            // Verify all restrictions are in place
            Assert.False(_rlv.Permissions.CanFly());
            Assert.False(_rlv.Permissions.CanJump());

            // Act - Remove restrictions from primId1 and primId3
            await _rlv.Restrictions.RemoveRestrictionsForObjects(new[] { primId1, primId3 });

            // Assert
            Assert.True(_rlv.Permissions.CanFly()); // fly restriction removed
            Assert.False(_rlv.Permissions.CanJump()); // jump restriction remains
            var trackedIds = _rlv.Restrictions.GetTrackedPrimIds();
            Assert.Single(trackedIds);
            Assert.Contains(primId2, trackedIds);
        }

        [Fact]
        public async Task RemoveRestrictionsForObjects_PartialMatch_RemovesOnlyMatchingObjects()
        {
            // Arrange
            var primId1 = new Guid("11111111-1111-1111-1111-111111111111");
            var primId2 = new Guid("22222222-2222-2222-2222-222222222222");
            var nonExistentId = new Guid("99999999-9999-9999-9999-999999999999");

            await _rlv.ProcessMessage("@fly=n", primId1, "TestObject1");
            await _rlv.ProcessMessage("@jump=n", primId2, "TestObject2");

            // Act - Try to remove restrictions from one existing and one non-existent object
            await _rlv.Restrictions.RemoveRestrictionsForObjects(new[] { primId1, nonExistentId });

            // Assert
            Assert.True(_rlv.Permissions.CanFly()); // fly restriction removed
            Assert.False(_rlv.Permissions.CanJump()); // jump restriction remains
            var trackedIds = _rlv.Restrictions.GetTrackedPrimIds();
            Assert.Single(trackedIds);
            Assert.Contains(primId2, trackedIds);
        }

        [Fact]
        public async Task RemoveRestrictionsForObjects_WithNotificationRestrictions_SendsNotifications()
        {
            // Arrange
            var primId1 = new Guid("11111111-1111-1111-1111-111111111111");
            var primId2 = new Guid("22222222-2222-2222-2222-222222222222");
            var notificationChannel = 123;

            // Set up notification
            await _rlv.ProcessMessage($"@notify:{notificationChannel}=n", primId2, "NotificationObject");
            await _rlv.ProcessMessage("@fly=n", primId1, "TestObject");

            // Act
            await _rlv.Restrictions.RemoveRestrictionsForObjects(new[] { primId1 });

            // Assert
            _actionCallbacks.Verify(x => x.SendReplyAsync(
                notificationChannel,
                "/fly=y",
                It.IsAny<CancellationToken>()
            ), Times.Once);
        }

        [Fact]
        public async Task RemoveRestrictionsForObjects_WithComplexRestrictions_RemovesCorrectly()
        {
            // Arrange
            var primId1 = new Guid("11111111-1111-1111-1111-111111111111");
            var primId2 = new Guid("22222222-2222-2222-2222-222222222222");

            // Add various types of restrictions
            await _rlv.ProcessMessage("@detachthis=n", primId1, "TestObject1");
            await _rlv.ProcessMessage("@detachthis_except:folder=n", primId1, "TestObject1");
            await _rlv.ProcessMessage("@touchfar:1.5=n", primId1, "TestObject1");
            await _rlv.ProcessMessage("@fly=n", primId2, "TestObject2");

            // Verify restrictions
            var restrictionsFromPrimId1 = _rlv.Restrictions.FindRestrictions(senderFilter: primId1);
            var restrictionsFromPrimId2 = _rlv.Restrictions.FindRestrictions(senderFilter: primId2);
            Assert.Equal(3, restrictionsFromPrimId1.Count);
            Assert.Single(restrictionsFromPrimId2);

            // Act
            await _rlv.Restrictions.RemoveRestrictionsForObjects(new[] { primId1 });

            // Assert
            restrictionsFromPrimId1 = _rlv.Restrictions.FindRestrictions(senderFilter: primId1);
            restrictionsFromPrimId2 = _rlv.Restrictions.FindRestrictions(senderFilter: primId2);
            Assert.Empty(restrictionsFromPrimId1);
            Assert.Single(restrictionsFromPrimId2);
        }

        [Fact]
        public async Task RemoveRestrictionsForObjects_TriggersRestrictionUpdatedEvent()
        {
            // Arrange
            var primId = new Guid("11111111-1111-1111-1111-111111111111");
            await _rlv.ProcessMessage("@fly=n", primId, "TestObject");

            var eventFired = false;
            RlvRestriction? removedRestriction = null;

            _rlv.Restrictions.RestrictionUpdated += (sender, args) =>
            {
                if (!args.IsNew && args.IsDeleted)
                {
                    eventFired = true;
                    removedRestriction = args.Restriction;
                }
            };

            // Act
            await _rlv.Restrictions.RemoveRestrictionsForObjects(new[] { primId });

            // Assert
            Assert.True(eventFired);
            Assert.NotNull(removedRestriction);
            Assert.Equal(RlvRestrictionType.Fly, removedRestriction.Behavior);
            Assert.Equal(primId, removedRestriction.Sender);
        }

        #endregion
    }
}
