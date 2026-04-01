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
    public class SetDebugRestrictionTests : RlvTestBase
    {

        #region @setdebug=<y/n>
        [Test]
        public async Task CanSetDebug()
        {
            await CheckSimpleCommand("setDebug", m => m.CanSetDebug());
        }
        #endregion
    }
}
