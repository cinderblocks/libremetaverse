using System.Threading.Tasks;
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
