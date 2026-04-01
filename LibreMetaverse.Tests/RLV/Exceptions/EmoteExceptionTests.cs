using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using Moq;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Exceptions
{
    [TestFixture]
    public class EmoteExceptionTests : RlvTestBase
    {
        #region @emote=<rem/add>
        [Test]
        public async Task CanEmote()
        {
            await CheckSimpleCommand("emote", m => m.CanEmote());
        }

        #endregion
    }
}
