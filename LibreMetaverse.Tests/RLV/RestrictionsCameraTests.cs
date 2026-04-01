using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using NUnit.Framework;
using Moq;

namespace LibreMetaverse.Tests.RLV
{
    [TestFixture]
    public class RestrictionsCameraTests : RlvTestBase
    {

        #region CamMinFunctionsThrough

        [Test]
        public async Task CamZoomMin_Single()
        {
            await _rlv.ProcessMessage("@CamZoomMin:1.5=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.ZoomMin, Is.Not.Null);
            Assert.That(cameraRestrictions.ZoomMin.Value, Is.EqualTo(1.5f).Within(FloatTolerance));
        }

        [Test]
        public async Task CamZoomMin_Multiple_SingleSender()
        {
            await _rlv.ProcessMessage("@CamZoomMin:3.5=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@CamZoomMin:4.5=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@CamZoomMin:1.5=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.ZoomMin, Is.Not.Null);
            Assert.That(cameraRestrictions.ZoomMin.Value, Is.EqualTo(4.5f).Within(FloatTolerance));
        }

        [Test]
        public async Task CamZoomMin_Multiple_SingleSender_WithRemoval()
        {
            await _rlv.ProcessMessage("@CamZoomMin:3.5=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@CamZoomMin:4.5=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@CamZoomMin:1.5=n", _sender.Id, _sender.Name);

            await _rlv.ProcessMessage("@CamZoomMin:8.5=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@CamZoomMin:8.5=y", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.ZoomMin, Is.Not.Null);
            Assert.That(cameraRestrictions.ZoomMin.Value, Is.EqualTo(4.5f).Within(FloatTolerance));
        }

        [Test]
        public async Task CamZoomMin_Multiple_MultipleSenders()
        {
            var sender2 = new RlvObject("Sender 2", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
            var sender3 = new RlvObject("Sender 3", new Guid("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"));

            await _rlv.ProcessMessage("@CamZoomMin:3.5=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@CamZoomMin:4.5=n", sender2.Id, sender2.Name);
            await _rlv.ProcessMessage("@CamZoomMin:1.5=n", sender3.Id, sender3.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.ZoomMin, Is.Not.Null);
            Assert.That(cameraRestrictions.ZoomMin.Value, Is.EqualTo(4.5f).Within(FloatTolerance));
        }

        [Test]
        public async Task CamZoomMin_Off()
        {
            await _rlv.ProcessMessage("@CamZoomMin:1.5=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@CamZoomMin:1.5=y", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.ZoomMin, Is.Null);
        }
        #endregion

        #region CamMaxFunctionsThrough
        [Test]
        public void CamZoomMax_Default()
        {
            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.ZoomMax, Is.Null);
        }

        [Test]
        public async Task CamZoomMax_Single()
        {
            await _rlv.ProcessMessage("@CamZoomMax:1.5=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.ZoomMax, Is.Not.Null);
            Assert.That(cameraRestrictions.ZoomMax, Is.EqualTo(1.5f));
        }

        [Test]
        public async Task CamZoomMax_Multiple_SingleSender()
        {
            await _rlv.ProcessMessage("@CamZoomMax:3.5=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@CamZoomMax:4.5=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@CamZoomMax:1.5=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.ZoomMax, Is.Not.Null);
            Assert.That(cameraRestrictions.ZoomMax, Is.EqualTo(1.5f));
        }

        [Test]
        public async Task CamZoomMax_Multiple_SingleSender_WithRemoval()
        {
            await _rlv.ProcessMessage("@CamZoomMax:3.5=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@CamZoomMax:4.5=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@CamZoomMax:1.5=n", _sender.Id, _sender.Name);

            await _rlv.ProcessMessage("@CamZoomMax:0.5=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@CamZoomMax:0.5=y", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.ZoomMax, Is.Not.Null);
            Assert.That(cameraRestrictions.ZoomMax, Is.EqualTo(1.5f));
        }

        [Test]
        public async Task CamZoomMax_Multiple_MultipleSenders()
        {
            var sender2 = new RlvObject("Sender 2", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
            var sender3 = new RlvObject("Sender 3", new Guid("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"));

            await _rlv.ProcessMessage("@CamZoomMax:3.5=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@CamZoomMax:4.5=n", sender2.Id, sender2.Name);
            await _rlv.ProcessMessage("@CamZoomMax:1.5=n", sender3.Id, sender3.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.ZoomMax, Is.Not.Null);
            Assert.That(cameraRestrictions.ZoomMax, Is.EqualTo(1.5f));
        }

        [Test]
        public async Task CamZoomMax_Off()
        {
            await _rlv.ProcessMessage("@CamZoomMax:1.5=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@CamZoomMax:1.5=y", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.ZoomMax, Is.Null);
        }

        #endregion

        #region @CamZoomMin
        [Test]
        public async Task CamZoomMin()
        {
            await _rlv.ProcessMessage("@CamZoomMin:0.5=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.ZoomMin, Is.Not.Null);
            Assert.That(cameraRestrictions.ZoomMin.Value, Is.EqualTo(0.5f).Within(FloatTolerance));
        }
        #endregion

        #region @CamZoomMax
        [Test]
        public async Task CamZoomMax()
        {
            await _rlv.ProcessMessage("@CamZoomMax:1.5=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.ZoomMax, Is.Not.Null);
            Assert.That(cameraRestrictions.ZoomMax.Value, Is.EqualTo(1.5f).Within(FloatTolerance));
        }
        #endregion

        #region @setcam_fovmin
        [Test]
        public async Task SetCamFovMin()
        {
            await _rlv.ProcessMessage("@setcam_fovmin:15=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.FovMin, Is.Not.Null);
            Assert.That(cameraRestrictions.FovMin.Value, Is.EqualTo(15f).Within(FloatTolerance));
        }
        #endregion

        #region @setcam_fovmax
        [Test]
        public async Task SetCamFovMax()
        {
            await _rlv.ProcessMessage("@setcam_fovmax:45=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.FovMax, Is.Not.Null);
            Assert.That(cameraRestrictions.FovMax.Value, Is.EqualTo(45f).Within(FloatTolerance));
        }
        #endregion

        #region @setcam_fov:<fov_in_radians>=force
        [Test]
        public async Task SetCamFov()
        {
            Assert.That(await _rlv.ProcessMessage("@setcam_fov:1.75=force", _sender.Id, _sender.Name), Is.True);

            _actionCallbacks.Verify(x => x.SetCamFOVAsync(
                1.75f,
                It.IsAny<CancellationToken>()
            ), Times.Once);

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Test]
        public async Task SetCamFov_Restricted()
        {
            await _rlv.ProcessMessage("@setcam_unlock=n", _sender.Id, _sender.Name);

            Assert.That(await _rlv.ProcessMessage("@setcam_fov:1.75=force", _sender.Id, _sender.Name), Is.False);

            _actionCallbacks.Verify(x => x.SetCamFOVAsync(
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()
            ), Times.Never);

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Test]
        public async Task SetCamFov_Restricted_Synonym()
        {
            await _rlv.ProcessMessage("@camunlock=n", _sender.Id, _sender.Name);

            Assert.That(await _rlv.ProcessMessage("@setcam_fov:1.75=force", _sender.Id, _sender.Name), Is.False);

            _actionCallbacks.Verify(x => x.SetCamFOVAsync(
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()
            ), Times.Never);

            _actionCallbacks.VerifyNoOtherCalls();
        }
        #endregion

        #region @setcam_avdistmax
        [Test]
        public async Task SetCamAvDistMax()
        {
            await _rlv.ProcessMessage("@setcam_avdistmax:30=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.AvDistMax, Is.Not.Null);
            Assert.That(cameraRestrictions.AvDistMax.Value, Is.EqualTo(30f).Within(FloatTolerance));
        }
        [Test]
        public async Task SetCamAvDistMax_Synonym()
        {
            await _rlv.ProcessMessage("@camdistmax:30=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.AvDistMax, Is.Not.Null);
            Assert.That(cameraRestrictions.AvDistMax.Value, Is.EqualTo(30f).Within(FloatTolerance));
        }
        #endregion

        #region @setcam_avdistmin
        [Test]
        public async Task SetCamAvDistMin()
        {
            await _rlv.ProcessMessage("@setcam_avdistmin:0.3=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.AvDistMin, Is.Not.Null);
            Assert.That(cameraRestrictions.AvDistMin.Value, Is.EqualTo(0.3f).Within(FloatTolerance));
        }

        [Test]
        public async Task SetCamAvDistMin_Synonym()
        {
            await _rlv.ProcessMessage("@camdistmin:0.3=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.AvDistMin, Is.Not.Null);
            Assert.That(cameraRestrictions.AvDistMin.Value, Is.EqualTo(0.3f).Within(FloatTolerance));
        }
        #endregion

        #region @CamDrawAlphaMax
        [Test]
        public async Task CamDrawAlphaMax()
        {
            await _rlv.ProcessMessage("@CamDrawAlphaMax:0.9=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.DrawAlphaMax, Is.Not.Null);
            Assert.That(cameraRestrictions.DrawAlphaMax.Value, Is.EqualTo(0.9f).Within(FloatTolerance));
        }
        #endregion

        #region @camdrawmin:<min_distance>=<y/n>

        [Test]
        public async Task CamDrawMin()
        {
            await _rlv.ProcessMessage("@camdrawmin:1.75=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.DrawMin, Is.Not.Null);
            Assert.That(cameraRestrictions.DrawMin.Value, Is.EqualTo(1.75f).Within(FloatTolerance));
        }

        [Test]
        public async Task CamDrawMin_Small()
        {
            Assert.That(await _rlv.ProcessMessage("@camdrawmin:0.15=n", _sender.Id, _sender.Name), Is.False);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.DrawMin, Is.Null);
        }

        #endregion

        #region @camdrawmax:<max_distance>=<y/n>

        [Test]
        public async Task CamDrawMax()
        {
            await _rlv.ProcessMessage("@camdrawmax:1.75=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.DrawMax, Is.Not.Null);
            Assert.That(cameraRestrictions.DrawMax.Value, Is.EqualTo(1.75f).Within(FloatTolerance));
        }

        [Test]
        public async Task CamDrawMax_Small()
        {
            Assert.That(await _rlv.ProcessMessage("@camdrawmax:0.15=n", _sender.Id, _sender.Name), Is.False);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.DrawMax, Is.Null);
        }

        #endregion

        #region @camdrawalphamin:<min_distance>=<y/n>

        [Test]
        public async Task CamDrawAlphaMin()
        {
            await _rlv.ProcessMessage("@camdrawalphamin:1.75=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.DrawAlphaMin, Is.Not.Null);
            Assert.That(cameraRestrictions.DrawAlphaMin.Value, Is.EqualTo(1.75f).Within(FloatTolerance));
        }

        #endregion

        #region @CamDrawColor

        [Test]
        public async Task CamDrawColor()
        {
            await _rlv.ProcessMessage("@CamDrawColor:0.1;0.2;0.3=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();

            Assert.That(cameraRestrictions.DrawColor, Is.Not.Null);
            Assert.That(cameraRestrictions.DrawColor.Value.X, Is.EqualTo(0.1f).Within(FloatTolerance));
            Assert.That(cameraRestrictions.DrawColor.Value.Y, Is.EqualTo(0.2f).Within(FloatTolerance));
            Assert.That(cameraRestrictions.DrawColor.Value.Z, Is.EqualTo(0.3f).Within(FloatTolerance));
        }

        [Test]
        public void CamDrawColor_Default()
        {
            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.DrawColor, Is.Null);
        }

        [Test]
        public async Task CamDrawColor_Large()
        {
            await _rlv.ProcessMessage("@CamDrawColor:5;6;7=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();

            Assert.That(cameraRestrictions.DrawColor, Is.Not.Null);
            Assert.That(cameraRestrictions.DrawColor.Value.X, Is.EqualTo(1.0f).Within(FloatTolerance));
            Assert.That(cameraRestrictions.DrawColor.Value.Y, Is.EqualTo(1.0f).Within(FloatTolerance));
            Assert.That(cameraRestrictions.DrawColor.Value.Z, Is.EqualTo(1.0f).Within(FloatTolerance));
        }

        [Test]
        public async Task CamDrawColor_Negative()
        {
            await _rlv.ProcessMessage("@CamDrawColor:-5;-6;-7=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();

            Assert.That(cameraRestrictions.DrawColor, Is.Not.Null);
            Assert.That(cameraRestrictions.DrawColor.Value.X, Is.EqualTo(0.0f).Within(FloatTolerance));
            Assert.That(cameraRestrictions.DrawColor.Value.Y, Is.EqualTo(0.0f).Within(FloatTolerance));
            Assert.That(cameraRestrictions.DrawColor.Value.Z, Is.EqualTo(0.0f).Within(FloatTolerance));
        }

        [Test]
        public async Task CamDrawColor_Removal()
        {
            await _rlv.ProcessMessage("@CamDrawColor:0.1;0.2;0.3=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@CamDrawColor:0.1;0.2;0.3=y", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.DrawColor, Is.Null);
        }

        [Test]
        public async Task CamDrawColor_Multi()
        {
            await _rlv.ProcessMessage("@CamDrawColor:0.1;0.2;0.3=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@CamDrawColor:0.2;0.3;0.6=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();

            Assert.That(cameraRestrictions.DrawColor, Is.Not.Null);
            Assert.That(cameraRestrictions.DrawColor.Value.X, Is.EqualTo(0.15f).Within(FloatTolerance));
            Assert.That(cameraRestrictions.DrawColor.Value.Y, Is.EqualTo(0.25f).Within(FloatTolerance));
            Assert.That(cameraRestrictions.DrawColor.Value.Z, Is.EqualTo(0.45f).Within(FloatTolerance));
        }
        #endregion

        #region @camunlock
        [Test]
        public async Task CanSetCamUnlock()
        {
            await _rlv.ProcessMessage("@setcam_unlock=n", _sender.Id, _sender.Name);
            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.IsLocked, Is.True);

            await _rlv.ProcessMessage("@setcam_unlock=y", _sender.Id, _sender.Name);
            cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.IsLocked, Is.False);
        }
        #endregion

        #region @setcam_unlock
        [Test]
        public async Task CanCamUnlock()
        {
            await _rlv.ProcessMessage("@camunlock=n", _sender.Id, _sender.Name);
            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.IsLocked, Is.True);

            await _rlv.ProcessMessage("@camunlock=y", _sender.Id, _sender.Name);
            cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.IsLocked, Is.False);
        }
        #endregion

        #region @camavdist
        [Test]
        public async Task CamAvDist()
        {
            await _rlv.ProcessMessage("@CamAvDist:5=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();

            Assert.That(cameraRestrictions.AvDist, Is.Not.Null);
            Assert.That(cameraRestrictions.AvDist.Value, Is.EqualTo(5f).Within(FloatTolerance));
        }
        #endregion

        #region @camtextures @setcam_textures[:texture_uuid]=<y/n>

        [TestCase("setcam_textures")]
        [TestCase("camtextures")]
        public async Task SetCamTextures(string command)
        {
            await _rlv.ProcessMessage($"@{command}=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.Texture, Is.EqualTo(Guid.Empty));
        }

        [TestCase("setcam_textures")]
        [TestCase("camtextures")]
        public async Task SetCamTextures_Single(string command)
        {
            var textureId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessage($"@{command}:{textureId1}=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.Texture, Is.EqualTo(textureId1));
        }

        [TestCase("setcam_textures", "setcam_textures")]
        [TestCase("setcam_textures", "camtextures")]
        [TestCase("camtextures", "camtextures")]
        [TestCase("camtextures", "setcam_textures")]
        public async Task SetCamTextures_Multiple_Synonyms(string command1, string command2)
        {
            var textureId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var textureId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@{command1}:{textureId1}=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@{command2}:{textureId2}=n", _sender.Id, _sender.Name);

            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.Texture == textureId2, Is.True);

            await _rlv.ProcessMessage($"@{command1}:{textureId2}=y", _sender.Id, _sender.Name);

            cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();
            Assert.That(cameraRestrictions.Texture == textureId1, Is.True);
        }

        #endregion

        private CameraSettings DefaultCameraSettings()
        {
            return new CameraSettings(
               avDistMin: 1.23f,
               avDistMax: 123.45f,
               fovMin: 45.60f,
               fovMax: 130.0f,
               zoomMin: 12.34f,
               currentFov: 91.34f
           );
        }

        #region @getcam_avdistmin=<channel_number>
        [Test]
        public async Task GetCamAvDistMin()
        {
            var actual = _actionCallbacks.RecordReplies();

            var cameraSettings = DefaultCameraSettings();
            _queryCallbacks.Setup(e =>
                e.TryGetCameraSettingsAsync(default)
            ).ReturnsAsync((true, cameraSettings));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, cameraSettings.AvDistMin.ToString()),
            };

            Assert.That(await _rlv.ProcessMessage("@getcam_avdistmin=1234", _sender.Id, _sender.Name), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }


        #endregion

        #region @getcam_avdistmax=<channel_number>
        [Test]
        public async Task GetCamAvDistMax()
        {
            var actual = _actionCallbacks.RecordReplies();

            var cameraSettings = DefaultCameraSettings();
            _queryCallbacks.Setup(e =>
                e.TryGetCameraSettingsAsync(default)
            ).ReturnsAsync((true, cameraSettings));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, cameraSettings.AvDistMax.ToString()),
            };

            Assert.That(await _rlv.ProcessMessage("@getcam_avdistmax=1234", _sender.Id, _sender.Name), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }
        #endregion

        #region @getcam_fovmin=<channel_number>
        [Test]
        public async Task GetCamFovMin()
        {
            var actual = _actionCallbacks.RecordReplies();

            var cameraSettings = DefaultCameraSettings();
            _queryCallbacks.Setup(e =>
                e.TryGetCameraSettingsAsync(default)
            ).ReturnsAsync((true, cameraSettings));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, cameraSettings.FovMin.ToString()),
            };

            Assert.That(await _rlv.ProcessMessage("@getcam_fovmin=1234", _sender.Id, _sender.Name), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }

        #endregion

        #region @getcam_fovmax=<channel_number>
        [Test]
        public async Task GetCamFovMax()
        {
            var actual = _actionCallbacks.RecordReplies();

            var cameraSettings = DefaultCameraSettings();
            _queryCallbacks.Setup(e =>
                e.TryGetCameraSettingsAsync(default)
            ).ReturnsAsync((true, cameraSettings));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, cameraSettings.FovMax.ToString()),
            };

            Assert.That(await _rlv.ProcessMessage("@getcam_fovmax=1234", _sender.Id, _sender.Name), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }

        #endregion

        #region @getcam_zoommin=<channel_number>
        [Test]
        public async Task GetCamZoomMin()
        {
            var actual = _actionCallbacks.RecordReplies();

            var cameraSettings = DefaultCameraSettings();
            _queryCallbacks.Setup(e =>
                e.TryGetCameraSettingsAsync(default)
            ).ReturnsAsync((true, cameraSettings));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, cameraSettings.ZoomMin.ToString()),
            };

            Assert.That(await _rlv.ProcessMessage("@getcam_zoommin=1234", _sender.Id, _sender.Name), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }

        #endregion

        #region @getcam_fov=<channel_number>
        [Test]
        public async Task GetCamFov()
        {
            var actual = _actionCallbacks.RecordReplies();

            var cameraSettings = DefaultCameraSettings();
            _queryCallbacks.Setup(e =>
                e.TryGetCameraSettingsAsync(default)
            ).ReturnsAsync((true, cameraSettings));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, cameraSettings.CurrentFov.ToString()),
            };

            Assert.That(await _rlv.ProcessMessage("@getcam_fov=1234", _sender.Id, _sender.Name), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }
        #endregion

        [TestCase("@getcam_zoommin=1234")]
        [TestCase("@getcam_fov=1234")]
        [TestCase("@getcam_fovmax=1234")]
        [TestCase("@getcam_avdistmin=1234")]
        [TestCase("@getcam_avdistmax=1234")]
        [TestCase("@getcam_fovmin=1234")]
        public async Task CameraSettings_Default(string command)
        {
            var actual = _actionCallbacks.RecordReplies();

            _queryCallbacks.Setup(e =>
                e.TryGetCameraSettingsAsync(default)
            ).ReturnsAsync((false, null));

            Assert.That(await _rlv.ProcessMessage(command, _sender.Id, _sender.Name), Is.False);
            Assert.That(actual, Is.Empty);
        }
    }
}
