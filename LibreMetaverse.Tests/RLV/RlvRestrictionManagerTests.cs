using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using NUnit.Framework;
using Moq;

namespace LibreMetaverse.Tests.RLV
{
    [TestFixture]
    public class RlvRestrictionManagerTests : RlvTestBase
    {
        #region GetTrackedPrimIds Tests

        [Test]
        public void GetTrackedPrimIds_NoRestrictions_ReturnsEmptyCollection()
        {
            // Act
            var trackedIds = _rlv.Restrictions.GetTrackedPrimIds();

            // Assert
            Assert.That(trackedIds, Is.Empty);
        }

        [Test]
        public async Task GetTrackedPrimIds_SingleRestriction_ReturnsSingleId()
        {
            // Arrange
            var primId = new Guid("11111111-1111-1111-1111-111111111111");

            // Act
            await _rlv.ProcessMessage("@fly=n", primId, "TestObject");
            var trackedIds = _rlv.Restrictions.GetTrackedPrimIds();

            // Assert
            Assert.That(trackedIds, Has.Exactly(1).Items);
            Assert.That(trackedIds, Does.Contain(primId));
        }

        [Test]
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
            Assert.That(trackedIds, Has.Exactly(1).Items);
            Assert.That(trackedIds, Does.Contain(primId));
        }

        [Test]
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
            Assert.That(trackedIds.Count(), Is.EqualTo(3));
            Assert.That(trackedIds, Does.Contain(primId1));
            Assert.That(trackedIds, Does.Contain(primId2));
            Assert.That(trackedIds, Does.Contain(primId3));
        }

        [Test]
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
            Assert.That(trackedIds.Count(), Is.EqualTo(2));
            Assert.That(trackedIds, Does.Contain(primId1));
            Assert.That(trackedIds, Does.Contain(primId2));
        }

        [Test]
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
            Assert.That(trackedIds, Has.Exactly(1).Items);
            Assert.That(trackedIds, Does.Contain(primId2));
            Assert.That(trackedIds, Does.Not.Contain(primId1));
        }

        [Test]
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
            Assert.That(trackedIds, Is.Empty);
        }

        #endregion

        #region RemoveRestrictionsForObjects Tests

        [Test]
        public async Task RemoveRestrictionsForObjects_EmptyInput_NoEffect()
        {
            // Arrange
            var primId = new Guid("11111111-1111-1111-1111-111111111111");
            await _rlv.ProcessMessage("@fly=n", primId, "TestObject");

            // Act
            await _rlv.Restrictions.RemoveRestrictionsForObjects(Array.Empty<Guid>());

            // Assert
            Assert.That(_rlv.Permissions.CanFly(), Is.False);
            var trackedIds = _rlv.Restrictions.GetTrackedPrimIds();
            Assert.That(trackedIds, Has.Exactly(1).Items);
        }

        [Test]
        public async Task RemoveRestrictionsForObjects_NonExistentId_NoEffect()
        {
            // Arrange
            var primId = new Guid("11111111-1111-1111-1111-111111111111");
            var nonExistentId = new Guid("99999999-9999-9999-9999-999999999999");
            await _rlv.ProcessMessage("@fly=n", primId, "TestObject");

            // Act
            await _rlv.Restrictions.RemoveRestrictionsForObjects(new[] { nonExistentId });

            // Assert
            Assert.That(_rlv.Permissions.CanFly(), Is.False);
            var trackedIds = _rlv.Restrictions.GetTrackedPrimIds();
            Assert.That(trackedIds, Has.Exactly(1).Items);
            Assert.That(trackedIds, Does.Contain(primId));
        }

        [Test]
        public async Task RemoveRestrictionsForObjects_SingleObject_RemovesAllRestrictionsFromObject()
        {
            // Arrange
            var primId = new Guid("11111111-1111-1111-1111-111111111111");
            await _rlv.ProcessMessage("@fly=n", primId, "TestObject");
            await _rlv.ProcessMessage("@jump=n", primId, "TestObject");
            await _rlv.ProcessMessage("@sit=n", primId, "TestObject");

            // Verify restrictions are in place
            Assert.That(_rlv.Permissions.CanFly(), Is.False);
            Assert.That(_rlv.Permissions.CanJump(), Is.False);

            // Act
            await _rlv.Restrictions.RemoveRestrictionsForObjects(new[] { primId });

            // Assert
            Assert.That(_rlv.Permissions.CanFly(), Is.True);
            Assert.That(_rlv.Permissions.CanJump(), Is.True);
            var trackedIds = _rlv.Restrictions.GetTrackedPrimIds();
            Assert.That(trackedIds, Is.Empty);
        }

        [Test]
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
            Assert.That(_rlv.Permissions.CanFly(), Is.False);
            Assert.That(_rlv.Permissions.CanJump(), Is.False);

            // Act - Remove restrictions from primId1 and primId3
            await _rlv.Restrictions.RemoveRestrictionsForObjects(new[] { primId1, primId3 });

            // Assert
            Assert.That(_rlv.Permissions.CanFly(), Is.True); // fly restriction removed
            Assert.That(_rlv.Permissions.CanJump(), Is.False); // jump restriction remains
            var trackedIds = _rlv.Restrictions.GetTrackedPrimIds();
            Assert.That(trackedIds, Has.Exactly(1).Items);
            Assert.That(trackedIds, Does.Contain(primId2));
        }

        [Test]
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
            Assert.That(_rlv.Permissions.CanFly(), Is.True); // fly restriction removed
            Assert.That(_rlv.Permissions.CanJump(), Is.False); // jump restriction remains
            var trackedIds = _rlv.Restrictions.GetTrackedPrimIds();
            Assert.That(trackedIds, Has.Exactly(1).Items);
            Assert.That(trackedIds, Does.Contain(primId2));
        }

        [Test]
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

        [Test]
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
            Assert.That(restrictionsFromPrimId1.Count, Is.EqualTo(3));
            Assert.That(restrictionsFromPrimId2, Has.Exactly(1).Items);

            // Act
            await _rlv.Restrictions.RemoveRestrictionsForObjects(new[] { primId1 });

            // Assert
            restrictionsFromPrimId1 = _rlv.Restrictions.FindRestrictions(senderFilter: primId1);
            restrictionsFromPrimId2 = _rlv.Restrictions.FindRestrictions(senderFilter: primId2);
            Assert.That(restrictionsFromPrimId1, Is.Empty);
            Assert.That(restrictionsFromPrimId2, Has.Exactly(1).Items);
        }

        [Test]
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
            Assert.That(eventFired, Is.True);
            Assert.That(removedRestriction, Is.Not.Null);
            Assert.That(removedRestriction.Behavior, Is.EqualTo(RlvRestrictionType.Fly));
            Assert.That(removedRestriction.Sender, Is.EqualTo(primId));
        }

        #endregion
    }
}
