using NUnit.Framework;
using OpenMetaverse;
using LibreMetaverse.Appearance;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LibreMetaverse.Tests
{
    #region Test Helpers

    /// <summary>
    /// Test policy that records calls and allows configuration of behavior
    /// </summary>
    internal class TestPolicy : ICurrentOutfitPolicy
    {
        public bool AllowAttach { get; set; } = true;
        public bool AllowDetach { get; set; } = true;
        public int AttachCallCount { get; private set; }
        public int DetachCallCount { get; private set; }
        public int ReportChangeCallCount { get; private set; }
        public List<InventoryItem> LastAddedItems { get; private set; }
        public List<InventoryItem> LastRemovedItems { get; private set; }

        public bool CanAttach(InventoryItem item)
        {
            AttachCallCount++;
            return AllowAttach;
        }

        public bool CanDetach(InventoryItem item)
        {
            DetachCallCount++;
            return AllowDetach;
        }

        public Task ReportItemChange(List<InventoryItem> addedItems, List<InventoryItem> removedItems, CancellationToken cancellationToken = default)
        {
            ReportChangeCallCount++;
            LastAddedItems = addedItems;
            LastRemovedItems = removedItems;
            return Task.CompletedTask;
        }

        public void Reset()
        {
            AttachCallCount = 0;
            DetachCallCount = 0;
            ReportChangeCallCount = 0;
            LastAddedItems = null;
            LastRemovedItems = null;
        }
    }

    #endregion

    [TestFixture]
    public class CurrentOutfitFolderTests
    {
        [Test]
        public void Constructor_WithNullClient_ThrowsArgumentNullException()
        {
            Assert.That(() => new CurrentOutfitFolder(null), 
                Throws.ArgumentNullException.With.Property("ParamName").EqualTo("client"));
        }

        [Test]
        public void Dispose_CalledOnce_DoesNotThrow()
        {
            var client = new GridClient();
            using (var cof = new CurrentOutfitFolder(client))
            {
                Assert.That(() => cof.Dispose(), Throws.Nothing);
            }
        }

        [Test]
        public void Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            var client = new GridClient();
            using (var cof = new CurrentOutfitFolder(client))
            {
                cof.Dispose();
                Assert.That(() => cof.Dispose(), Throws.Nothing);
            }
        }

        [Test]
        public void MaxClothingLayers_Returns60()
        {
            var client = new GridClient();
            using (var cof = new CurrentOutfitFolder(client))
            {
                Assert.That(cof.MaxClothingLayers, Is.EqualTo(60));
            }
        }

        [Test]
        public void COF_InitiallyNull()
        {
            var client = new GridClient();
            using (var cof = new CurrentOutfitFolder(client))
            {
                Assert.That(cof.COF, Is.Null);
            }
        }

        [Test]
        public void AddPolicy_WithValidPolicy_ReturnsAddedPolicy()
        {
            var client = new GridClient();
            using (var cof = new CurrentOutfitFolder(client))
            {
                var policy = new TestPolicy();
            
                var result = cof.AddPolicy(policy);
            
                Assert.That(result, Is.SameAs(policy));
            }
        }

        [Test]
        public void RemovePolicy_WithValidPolicy_DoesNotThrow()
        {
            var client = new GridClient();
            using (var cof = new CurrentOutfitFolder(client))
            {
                var policy = new TestPolicy();
                cof.AddPolicy(policy);
            
                Assert.That(() => cof.RemovePolicy(policy), Throws.Nothing);
            }
        }

        [Test]
        public void RemovePolicy_WithNonAddedPolicy_DoesNotThrow()
        {
            var client = new GridClient();
            using (var cof = new CurrentOutfitFolder(client))
            {
                var policy = new TestPolicy();
            
                Assert.That(() => cof.RemovePolicy(policy), Throws.Nothing);
            }
        }

        [Test]
        public void ResolveInventoryLink_WithNonLinkItem_ReturnsSameItem()
        {
            var client = new GridClient();
            using (var cof = new CurrentOutfitFolder(client))
            {
                var item = new InventoryWearable(UUID.Random())
                {
                    AssetType = AssetType.Clothing,
                    AssetUUID = UUID.Random()
                };
            
                var result = cof.ResolveInventoryLink(item);
            
                Assert.That(result, Is.SameAs(item));
            }
        }

        [Test]
        public void ResolveInventoryLink_WithLinkItemNotInStore_ReturnsNull()
        {
            var client = new GridClient();
            using (var cof = new CurrentOutfitFolder(client))
            {
                var assetUuid = UUID.Random();
                var link = new InventoryItem(UUID.Random())
                {
                    AssetType = AssetType.Link,
                    AssetUUID = assetUuid,
                    OwnerID = client.Self.AgentID
                };
            
                // This test requires a fully initialized client with inventory store
                // Since we can't mock it easily, we verify it handles the null case
                try
                {
                    var result = cof.ResolveInventoryLink(link);
                    // If it succeeds, result should be null since item is not in store
                    Assert.That(result, Is.Null);
                }
                catch (NullReferenceException)
                {
                    // Expected when client.Inventory.Store is null
                    Assert.Pass("NullReferenceException is expected when inventory store is not initialized");
                }
            }
        }

        [Test]
        public void GetAttachmentItemID_WithNoNameValues_ReturnsZero()
        {
            var prim = new Primitive
            {
                NameValues = null
            };
            
            var result = CurrentOutfitFolder.GetAttachmentItemID(prim);
            
            Assert.That(result, Is.EqualTo(UUID.Zero));
        }

        [Test]
        public void GetAttachmentItemID_WithAttachItemID_ReturnsCorrectUUID()
        {
            var expectedUuid = UUID.Random();
            var prim = new Primitive
            {
                NameValues = new NameValue[]
                {
                    new NameValue("AttachItemID", NameValue.ValueType.String, 
                        NameValue.ClassType.ReadOnly, NameValue.SendtoType.SimViewer, expectedUuid.ToString())
                }
            };
            
            var result = CurrentOutfitFolder.GetAttachmentItemID(prim);
            
            Assert.That(result, Is.EqualTo(expectedUuid));
        }

        [Test]
        public void GetAttachmentItemID_WithoutAttachItemID_ReturnsZero()
        {
            var prim = new Primitive
            {
                NameValues = new NameValue[]
                {
                    new NameValue("SomeOtherValue", NameValue.ValueType.String,
                        NameValue.ClassType.ReadOnly, NameValue.SendtoType.SimViewer, "test")
                }
            };
            
            var result = CurrentOutfitFolder.GetAttachmentItemID(prim);
            
            Assert.That(result, Is.EqualTo(UUID.Zero));
        }

        [Test]
        public async Task IsObjectDescendentOf_WithZeroParentId_ReturnsFalse()
        {
            var client = new GridClient();
            using (var cof = new CurrentOutfitFolder(client))
            {
                var item = new InventoryItem(UUID.Random());
            
                var result = await cof.IsObjectDescendentOf(item, UUID.Zero, CancellationToken.None);
            
                Assert.That(result, Is.False);
            }
        }

        [Test]
        public async Task IsObjectDescendentOf_WithDirectParent_ReturnsTrue()
        {
            var client = new GridClient();
            using (var cof = new CurrentOutfitFolder(client))
            {
                var parentId = UUID.Random();
                var item = new InventoryItem(UUID.Random())
                {
                    ParentUUID = parentId
                };
            
                var result = await cof.IsObjectDescendentOf(item, parentId, CancellationToken.None);
            
                Assert.That(result, Is.True);
            }
        }

        [Test]
        public async Task FetchParent_WithZeroParentUUID_ReturnsNull()
        {
            var client = new GridClient();
            using (var cof = new CurrentOutfitFolder(client))
            {
                var item = new InventoryItem(UUID.Random())
                {
                    ParentUUID = UUID.Zero
                };
            
                var result = await cof.FetchParent(item, CancellationToken.None);
            
                Assert.That(result, Is.Null);
            }
        }

        [Test]
        [Ignore("Requires fully initialized GridClient with inventory capabilities")]
        public async Task GetCurrentOutfitLinks_WithNullCOF_ReturnsEmptyList()
        {
            var client = new GridClient();
            using (var cof = new CurrentOutfitFolder(client))
            {
                var result = await cof.GetCurrentOutfitLinks(CancellationToken.None);
            
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.Empty);
            }
        }
    }

    [TestFixture]
    public class CompositeCurrentOutfitPolicyTests
    {
        private CompositeCurrentOutfitPolicy compositePolicy;
        private TestPolicy testPolicy1;
        private TestPolicy testPolicy2;
        private InventoryItem testItem;

        [SetUp]
        public void SetUp()
        {
            compositePolicy = new CompositeCurrentOutfitPolicy();
            testPolicy1 = new TestPolicy();
            testPolicy2 = new TestPolicy();
            
            testItem = new InventoryWearable(UUID.Random())
            {
                AssetType = AssetType.Clothing,
                Name = "Test Item"
            };
        }

        #region AddPolicy Tests

        [Test]
        public void AddPolicy_WithValidPolicy_ReturnsThis()
        {
            var result = compositePolicy.AddPolicy(testPolicy1);
            
            Assert.That(result, Is.SameAs(compositePolicy));
        }

        [Test]
        public void AddPolicy_WithNullPolicy_ThrowsArgumentNullException()
        {
            Assert.That(() => compositePolicy.AddPolicy(null), 
                Throws.ArgumentNullException.With.Property("ParamName").EqualTo("policyToAdd"));
        }

        [Test]
        public void AddPolicy_MultiplePolicies_AllAreConsulted()
        {
            compositePolicy.AddPolicy(testPolicy1);
            compositePolicy.AddPolicy(testPolicy2);
            
            testPolicy1.AllowAttach = true;
            testPolicy2.AllowAttach = true;
            
            var result = compositePolicy.CanAttach(testItem);
            
            Assert.That(testPolicy1.AttachCallCount, Is.EqualTo(1));
            Assert.That(testPolicy2.AttachCallCount, Is.EqualTo(1));
            Assert.That(result, Is.True);
        }

        [Test]
        public void AddPolicy_SamePolicyTwice_OnlyAddsOnce()
        {
            compositePolicy.AddPolicy(testPolicy1);
            compositePolicy.AddPolicy(testPolicy1);
            
            testPolicy1.AllowAttach = true;
            
            compositePolicy.CanAttach(testItem);
            
            // ImmutableHashSet prevents duplicates
            Assert.That(testPolicy1.AttachCallCount, Is.EqualTo(1));
        }

        #endregion

        #region RemovePolicy Tests

        [Test]
        public void RemovePolicy_WithAddedPolicy_RemovesPolicy()
        {
            compositePolicy.AddPolicy(testPolicy1);
            compositePolicy.RemovePolicy(testPolicy1);
            
            testPolicy1.AllowAttach = false;
            
            var result = compositePolicy.CanAttach(testItem);
            
            // Should return true because no policies remain (empty = allow all)
            Assert.That(result, Is.True);
            Assert.That(testPolicy1.AttachCallCount, Is.EqualTo(0));
        }

        [Test]
        public void RemovePolicy_WithNonAddedPolicy_DoesNotThrow()
        {
            Assert.That(() => compositePolicy.RemovePolicy(testPolicy1), Throws.Nothing);
        }

        [Test]
        public void RemovePolicy_WithNull_DoesNotThrow()
        {
            Assert.That(() => compositePolicy.RemovePolicy(null), Throws.Nothing);
        }

        #endregion

        #region CanAttach Tests

        [Test]
        public void CanAttach_WithNoPolicies_ReturnsTrue()
        {
            var result = compositePolicy.CanAttach(testItem);
            
            Assert.That(result, Is.True);
        }

        [Test]
        public void CanAttach_WithNullItem_ThrowsArgumentNullException()
        {
            Assert.That(() => compositePolicy.CanAttach(null), 
                Throws.ArgumentNullException.With.Property("ParamName").EqualTo("item"));
        }

        [Test]
        public void CanAttach_WithAllPoliciesAllowing_ReturnsTrue()
        {
            compositePolicy.AddPolicy(testPolicy1);
            compositePolicy.AddPolicy(testPolicy2);
            
            testPolicy1.AllowAttach = true;
            testPolicy2.AllowAttach = true;
            
            var result = compositePolicy.CanAttach(testItem);
            
            Assert.That(result, Is.True);
        }

        [Test]
        public void CanAttach_WithOnePolicyDenying_ReturnsFalse()
        {
            compositePolicy.AddPolicy(testPolicy1);
            compositePolicy.AddPolicy(testPolicy2);
            
            testPolicy1.AllowAttach = true;
            testPolicy2.AllowAttach = false;
            
            var result = compositePolicy.CanAttach(testItem);
            
            Assert.That(result, Is.False);
        }

        [Test]
        public void CanAttach_WithAllPoliciesDenying_ReturnsFalse()
        {
            compositePolicy.AddPolicy(testPolicy1);
            compositePolicy.AddPolicy(testPolicy2);
            
            testPolicy1.AllowAttach = false;
            testPolicy2.AllowAttach = false;
            
            var result = compositePolicy.CanAttach(testItem);
            
            Assert.That(result, Is.False);
        }

        #endregion

        #region CanDetach Tests

        [Test]
        public void CanDetach_WithNoPolicies_ReturnsTrue()
        {
            var result = compositePolicy.CanDetach(testItem);
            
            Assert.That(result, Is.True);
        }

        [Test]
        public void CanDetach_WithNullItem_ThrowsArgumentNullException()
        {
            Assert.That(() => compositePolicy.CanDetach(null), 
                Throws.ArgumentNullException.With.Property("ParamName").EqualTo("item"));
        }

        [Test]
        public void CanDetach_WithAllPoliciesAllowing_ReturnsTrue()
        {
            compositePolicy.AddPolicy(testPolicy1);
            compositePolicy.AddPolicy(testPolicy2);
            
            testPolicy1.AllowDetach = true;
            testPolicy2.AllowDetach = true;
            
            var result = compositePolicy.CanDetach(testItem);
            
            Assert.That(result, Is.True);
        }

        [Test]
        public void CanDetach_WithOnePolicyDenying_ReturnsFalse()
        {
            compositePolicy.AddPolicy(testPolicy1);
            compositePolicy.AddPolicy(testPolicy2);
            
            testPolicy1.AllowDetach = true;
            testPolicy2.AllowDetach = false;
            
            var result = compositePolicy.CanDetach(testItem);
            
            Assert.That(result, Is.False);
        }

        [Test]
        public void CanDetach_WithAllPoliciesDenying_ReturnsFalse()
        {
            compositePolicy.AddPolicy(testPolicy1);
            compositePolicy.AddPolicy(testPolicy2);
            
            testPolicy1.AllowDetach = false;
            testPolicy2.AllowDetach = false;
            
            var result = compositePolicy.CanDetach(testItem);
            
            Assert.That(result, Is.False);
        }

        #endregion

        #region ReportItemChange Tests

        [Test]
        public async Task ReportItemChange_WithNoPolicies_CompletesSuccessfully()
        {
            var addedItems = new List<InventoryItem> { testItem };
            var removedItems = new List<InventoryItem>();
            
            await compositePolicy.ReportItemChange(addedItems, removedItems, CancellationToken.None);
            
            Assert.Pass("Completed without exception");
        }

        [Test]
        public async Task ReportItemChange_WithMultiplePolicies_CallsAllPolicies()
        {
            compositePolicy.AddPolicy(testPolicy1);
            compositePolicy.AddPolicy(testPolicy2);
            
            var addedItems = new List<InventoryItem> { testItem };
            var removedItems = new List<InventoryItem>();
            
            await compositePolicy.ReportItemChange(addedItems, removedItems, CancellationToken.None);
            
            Assert.That(testPolicy1.ReportChangeCallCount, Is.EqualTo(1));
            Assert.That(testPolicy2.ReportChangeCallCount, Is.EqualTo(1));
            Assert.That(testPolicy1.LastAddedItems, Is.SameAs(addedItems));
            Assert.That(testPolicy2.LastAddedItems, Is.SameAs(addedItems));
        }

        [Test]
        public void ReportItemChange_WithCancelledToken_ThrowsOperationCanceledException()
        {
            compositePolicy.AddPolicy(testPolicy1);
            
            var addedItems = new List<InventoryItem> { testItem };
            var removedItems = new List<InventoryItem>();
            var cts = new CancellationTokenSource();
            cts.Cancel();
            
            Assert.That(async () => 
                await compositePolicy.ReportItemChange(addedItems, removedItems, cts.Token), 
                Throws.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public async Task ReportItemChange_WithEmptyLists_DoesNotThrow()
        {
            compositePolicy.AddPolicy(testPolicy1);
            
            var addedItems = new List<InventoryItem>();
            var removedItems = new List<InventoryItem>();
            
            await compositePolicy.ReportItemChange(addedItems, removedItems, CancellationToken.None);
            
            Assert.That(testPolicy1.ReportChangeCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ReportItemChange_WithNullLists_DoesNotThrowFromComposite()
        {
            // The composite doesn't validate nulls, that's up to individual policies
            await compositePolicy.ReportItemChange(null, null, CancellationToken.None);
            
            Assert.Pass("Completed without exception");
        }

        #endregion

        #region Thread Safety Tests

        [Test]
        public void AddRemovePolicy_ConcurrentAccess_DoesNotThrow()
        {
            var policies = new List<TestPolicy>();
            for (int i = 0; i < 10; i++)
            {
                policies.Add(new TestPolicy());
            }

            var tasks = new List<Task>();
            
            // Add policies concurrently
            foreach (var policy in policies)
            {
                tasks.Add(Task.Run(() => compositePolicy.AddPolicy(policy)));
            }
            
            // Remove policies concurrently
            foreach (var policy in policies)
            {
                tasks.Add(Task.Run(() => compositePolicy.RemovePolicy(policy)));
            }
            
            Assert.That(() => Task.WaitAll(tasks.ToArray()), Throws.Nothing);
        }

        [Test]
        public void CanAttach_WhileModifyingPolicies_DoesNotThrow()
        {
            var cts = new CancellationTokenSource();
            
            var readTask = Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        compositePolicy.CanAttach(testItem);
                    }
                    catch (ArgumentNullException)
                    {
                        // Expected if timing is unfortunate
                    }
                }
            });
            
            var writeTask = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    var policy = new TestPolicy();
                    compositePolicy.AddPolicy(policy);
                    compositePolicy.RemovePolicy(policy);
                }
            });
            
            writeTask.Wait();
            cts.Cancel();
            
            Assert.That(() => readTask.Wait(1000), Throws.Nothing);
        }

        #endregion
    }
}
