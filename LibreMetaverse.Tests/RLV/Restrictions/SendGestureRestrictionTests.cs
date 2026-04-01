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
    public class SendGestureRestrictionTests : RlvTestBase
    {

        #region @sendgesture=<y/n>

        [Test]
        public async Task CanSendGesture()
        {
            await CheckSimpleCommand("sendGesture", m => m.CanSendGesture());
        }

        #endregion
    }
}
