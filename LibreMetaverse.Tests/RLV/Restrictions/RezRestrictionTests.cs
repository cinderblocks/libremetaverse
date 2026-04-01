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
    public class RezRestrictionTests : RlvTestBase
    {
        #region @rez=<y/n>
        [Test]
        public async Task CanRez()
        {
            await CheckSimpleCommand("rez", m => m.CanRez());
        }

        #endregion
    }
}
