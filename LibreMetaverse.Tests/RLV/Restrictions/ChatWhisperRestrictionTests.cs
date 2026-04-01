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
    public class ChatWhisperRestrictionTests : RlvTestBase
    {
        #region @chatwhisper=<y/n>
        [Test]
        public async Task CanChatWhisper()
        {
            await CheckSimpleCommand("chatWhisper", m => m.CanChatWhisper());
        }
        #endregion

    }
}
