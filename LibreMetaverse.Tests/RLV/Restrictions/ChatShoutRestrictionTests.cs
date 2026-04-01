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
    public class ChatShoutRestrictionTests : RlvTestBase
    {
        #region @chatshout=<y/n>
        [Test]
        public async Task CanChatShout()
        {
            await CheckSimpleCommand("chatShout", m => m.CanChatShout());
        }
        #endregion

    }
}
