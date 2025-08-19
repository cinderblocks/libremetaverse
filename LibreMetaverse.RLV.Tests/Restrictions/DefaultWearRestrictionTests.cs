using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class DefaultWearRestrictionTests : RestrictionsBase
    {
        #region @defaultwear=<y/n>

        [Fact]
        public async Task CanDefaultWear()
        {
            await CheckSimpleCommand("defaultWear", m => m.CanDefaultWear());
        }

        #endregion
    }
}
