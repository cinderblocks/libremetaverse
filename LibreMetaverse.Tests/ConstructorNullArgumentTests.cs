using System;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    public class ConstructorNullArgumentTests
    {
        [Test]
        public void InventoryAISClient_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new InventoryAISClient(null));
        }

        [Test]
        public void GridManager_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new GridManager(null));
        }

        [Test]
        public void InventoryManager_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new LibreMetaverse.InventoryManager(null));
        }

        [Test]
        public void AppearanceManager_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new AppearanceManager(null));
        }

        [Test]
        public void ObjectManager_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new LibreMetaverse.ObjectManager(null));
        }

        [Test]
        public void AgentManager_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new LibreMetaverse.AgentManager(null));
        }

        [Test]
        public void AvatarManager_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new LibreMetaverse.AvatarManager(null));
        }

        [Test]
        public void AssetManager_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new LibreMetaverse.AssetManager(null));
        }
    }
}
