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
    public class SetEnvRestrictionTests : RlvTestBase
    {
        #region @setenv=<y/n>
        [Test]
        public async Task CanSetEnv()
        {
            await CheckSimpleCommand("setEnv", m => m.CanSetEnv());
        }
        #endregion
    }
}
