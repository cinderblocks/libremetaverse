using Moq;

namespace LibreMetaverse.RLV.Tests
{
    public class RestrictionsBase
    {
        public record RlvObject(string Name, Guid Id);

        protected readonly RlvObject _sender;
        protected readonly Mock<IRlvQueryCallbacks> _queryCallbacks;
        protected readonly Mock<IRlvActionCallbacks> _actionCallbacks;
        protected readonly RlvService _rlv;

        public const float FloatTolerance = 0.00001f;

        public RestrictionsBase()
        {
            _sender = new RlvObject("Sender 1", new Guid("ffffffff-ffff-4fff-8fff-ffffffffffff"));
            _queryCallbacks = new Mock<IRlvQueryCallbacks>();
            _actionCallbacks = new Mock<IRlvActionCallbacks>();
            _rlv = new RlvService(_queryCallbacks.Object, _actionCallbacks.Object, true);
        }

        protected async Task CheckSimpleCommand(string cmd, Func<RlvPermissionsService, bool> canFunc)
        {
            await _rlv.ProcessMessage($"@{cmd}=n", _sender.Id, _sender.Name);
            Assert.False(canFunc(_rlv.Permissions));

            await _rlv.ProcessMessage($"@{cmd}=y", _sender.Id, _sender.Name);
            Assert.True(canFunc(_rlv.Permissions));
        }

        protected void SeedBlacklist(string seed)
        {
            var blacklistEntries = seed.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in blacklistEntries)
            {
                _rlv.Blacklist.BlacklistBehavior(item.Trim());
            }
        }

        //
        // RLVA stuff to implement
        //

        // @getattachnames[:<grp>]=<channel>
        // @getaddattachnames[:<grp>]=<channel>
        // @getremattachnames[:<grp>]=<channel>
        // @getoutfitnames=<channel>
        // @getaddoutfitnames=<channel>
        // @getremoutfitnames=<channel>

        // @fly:[true|false]=force

        // @setcam_eyeoffset[:<vector3>]=force,
        // @setcam_eyeoffsetscale[:<float>]=force
        // @setcam_focusoffset[:<vector3>]=force
        // @setcam_focus:<uuid>[;<dist>[;<direction>]]=force
        // @setcam_mode[:<option>]=force

        // @setcam_focusoffset:<vector3>=n|y
        // @setcam_eyeoffset:<vector3>=n|y
        // @setcam_eyeoffsetscale:<float>=n|y
        // @setcam_mouselook=n|y
        // @setcam=n|y

        // @getcam_avdist=<channel>
        // @getcam_textures=<channel>


        // @setoverlay_tween:[<alpha>];[<tint>];<duration>=force
        // @setoverlay=n|y
        // @setoverlay_touch=n
        // @setsphere=n|y

        // @getcommand[:<behaviour>[;<type>[;<separator>]]]=<channel>
        // @getheightoffset=<channel>

        // @buy=n|y
        // @pay=n|y

        // @showself=n|y
        // @showselfhead=n|y 
        // @viewtransparent=n|y
        // @viewwireframe=n|y


        // Probably don't care about/not going to touch:
        //  @bhvr=n|y
        //  @bhvr:<uuid>=n|y
        //  @bhvr[:<uuid>]=n|y
        //  @bhvr:<modifier>=n|y
        //  @bhvr:<global modifier>=n|y
        //  @bhvr:<local modifier>=force
        //  @bhvr:<modifier>=force

    }
}
