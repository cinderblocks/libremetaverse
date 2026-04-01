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
    public class ViewScriptRestrictionTests : RlvTestBase
    {
        #region @viewscript=<y/n>
        [Test]
        public async Task CanViewScript()
        {
            await CheckSimpleCommand("viewScript", m => m.CanViewScript());
        }
        #endregion
    }
}
