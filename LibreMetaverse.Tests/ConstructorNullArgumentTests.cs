using System;
using NUnit.Framework;
using OpenMetaverse;
using LibreMetaverse;

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
            Assert.Throws<ArgumentNullException>(() => new OpenMetaverse.InventoryManager(null));
        }

        [Test]
        public void AppearanceManager_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new AppearanceManager(null));
        }

        [Test]
        public void ObjectManager_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new OpenMetaverse.ObjectManager(null));
        }

        [Test]
        public void AgentManager_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new OpenMetaverse.AgentManager(null));
        }

        [Test]
        public void AvatarManager_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new OpenMetaverse.AvatarManager(null));
        }

        [Test]
        public void AssetManager_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new OpenMetaverse.AssetManager(null));
        }
    }
}
