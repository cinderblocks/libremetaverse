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
    public class ViewNoteRestrictionTests : RlvTestBase
    {
        #region @viewnote=<y/n>
        [Test]
        public async Task CanViewNote()
        {
            await CheckSimpleCommand("viewNote", m => m.CanViewNote());
        }
        #endregion
    }
}
