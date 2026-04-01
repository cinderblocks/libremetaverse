using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using Moq;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class JumpRestrictionTests : RlvTestBase
    {
        #region @jump=<y/n> (RLVa)
        [Test]
        public async Task CanJump()
        {
            await CheckSimpleCommand("jump", m => m.CanJump());
        }
        #endregion
    }
}
