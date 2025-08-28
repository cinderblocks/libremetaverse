using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LibreMetaverse.RLV
{
    public class RlvActionCallbacksDefault : IRlvActionCallbacks
    {
        public virtual Task SendReplyAsync(int channel, string message, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public virtual Task SendInstantMessageAsync(Guid targetUser, string message, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task AdjustHeightAsync(float distance, float factor, float delta, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task AttachAsync(IReadOnlyList<AttachmentRequest> itemsToAttach, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task DetachAsync(IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task RemOutfitAsync(IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task SetCamFOVAsync(float fovInRadians, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task SetDebugAsync(string settingName, string settingValue, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task SetEnvAsync(string settingName, string settingValue, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task SetGroupAsync(string groupName, string? roleName, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task SetGroupAsync(Guid groupId, string? roleName, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task SetRotAsync(float angleInRadians, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task SitAsync(Guid target, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task SitGroundAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task TpToAsync(float x, float y, float z, string? regionName, float? lookat, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task UnsitAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    public class RlvCallbacksDefault : IRlvQueryCallbacks
    {
        public virtual Task<(bool Success, string EnvironmentSettingValue)> TryGetEnvironmentSettingValueAsync(string settingName, CancellationToken cancellationToken)
        {
            if (!Enum.TryParse(settingName, true, out RlvGetEnvType settingType))
            {
                return Task.FromResult((false, string.Empty));
            }

            switch (settingType)
            {
                case RlvGetEnvType.Daytime:
                case RlvGetEnvType.AmbientR:
                case RlvGetEnvType.AmbientG:
                case RlvGetEnvType.AmbientB:
                case RlvGetEnvType.AmbientI:
                case RlvGetEnvType.BlueDensityR:
                case RlvGetEnvType.BlueDensityG:
                case RlvGetEnvType.BlueDensityB:
                case RlvGetEnvType.BlueDensityI:
                case RlvGetEnvType.BlueHorizonR:
                case RlvGetEnvType.BlueHorizonG:
                case RlvGetEnvType.BlueHorizonB:
                case RlvGetEnvType.BlueHorizonI:
                case RlvGetEnvType.CloudColorR:
                case RlvGetEnvType.CloudColorG:
                case RlvGetEnvType.CloudColorB:
                case RlvGetEnvType.CloudColorI:
                case RlvGetEnvType.CloudCoverage:
                case RlvGetEnvType.CloudX:
                case RlvGetEnvType.CloudY:
                case RlvGetEnvType.CloudD:
                case RlvGetEnvType.CloudDetailX:
                case RlvGetEnvType.CloudDetailY:
                case RlvGetEnvType.CloudDetailD:
                case RlvGetEnvType.CloudScale:
                case RlvGetEnvType.CloudScrollX:
                case RlvGetEnvType.CloudScrollY:
                case RlvGetEnvType.CloudVariance:
                case RlvGetEnvType.DensityMultiplier:
                case RlvGetEnvType.DistanceMultiplier:
                case RlvGetEnvType.DropletRadius:
                case RlvGetEnvType.EastAngle:
                case RlvGetEnvType.IceLevel:
                case RlvGetEnvType.HazeDensity:
                case RlvGetEnvType.HazeHorizon:
                case RlvGetEnvType.MaxAltitude:
                case RlvGetEnvType.MoistureLevel:
                case RlvGetEnvType.MoonAzim:
                case RlvGetEnvType.MoonNBrightness:
                case RlvGetEnvType.MoonElev:
                case RlvGetEnvType.MoonScale:
                case RlvGetEnvType.SceneGamma:
                case RlvGetEnvType.StarBrightness:
                case RlvGetEnvType.SunGlowFocus:
                case RlvGetEnvType.SunAzim:
                case RlvGetEnvType.SunElev:
                case RlvGetEnvType.SunScale:
                case RlvGetEnvType.SunMoonPosition:
                case RlvGetEnvType.SunMoonColorR:
                case RlvGetEnvType.SunMoonColorG:
                case RlvGetEnvType.SunMoonColorB:
                case RlvGetEnvType.SunMoonColorI:
                    return Task.FromResult((true, "0"));

                case RlvGetEnvType.Ambient:
                case RlvGetEnvType.BlueDensity:
                case RlvGetEnvType.BlueHorizon:
                case RlvGetEnvType.CloudColor:
                case RlvGetEnvType.Cloud:
                case RlvGetEnvType.CloudDetail:
                case RlvGetEnvType.SunMoonColor:
                    return Task.FromResult((true, "0;0;0"));

                case RlvGetEnvType.CloudScroll:
                    return Task.FromResult((true, "0;0"));

                case RlvGetEnvType.Preset:
                case RlvGetEnvType.Asset:
                    return Task.FromResult((true, ""));

                case RlvGetEnvType.MoonImage:
                case RlvGetEnvType.SunImage:
                case RlvGetEnvType.CloudImage:
                    return Task.FromResult((true, Guid.Empty.ToString()));

                case RlvGetEnvType.SunGlowSize:
                    return Task.FromResult((true, "1"));
            }

            return Task.FromResult((false, string.Empty));
        }

        public virtual Task<(bool Success, string DebugSettingValue)> TryGetDebugSettingValueAsync(string settingName, CancellationToken cancellationToken)
        {
            if (!Enum.TryParse(settingName, true, out RlvGetDebugType settingType))
            {
                return Task.FromResult((false, string.Empty));
            }

            switch (settingType)
            {
                case RlvGetDebugType.AvatarSex:
                case RlvGetDebugType.RestrainedLoveForbidGiveToRLV:
                case RlvGetDebugType.WindLightUseAtmosShaders:
                    return Task.FromResult((true, "0"));

                case RlvGetDebugType.RenderResolutionDivisor:
                case RlvGetDebugType.RestrainedLoveNoSetEnv:
                    return Task.FromResult((true, "1"));
            }

            return Task.FromResult((false, string.Empty));
        }

        public virtual Task<bool> ObjectExistsAsync(Guid objectId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public virtual Task<bool> IsSittingAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public virtual Task<(bool Success, Guid SitId)> TryGetSitIdAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult((false, default(Guid)));
        }

        public virtual Task<(bool Success, RlvSharedFolder? SharedFolder)> TryGetSharedFolderAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult((false, (RlvSharedFolder?)null));
        }

        public virtual Task<(bool Success, CameraSettings? CameraSettings)> TryGetCameraSettingsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult((false, (CameraSettings?)null));
        }

        public virtual Task<(bool Success, string ActiveGroupName)> TryGetActiveGroupNameAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult((false, "None"));
        }

        public virtual Task<(bool Success, InventoryMap? InventoryMap)> TryGetInventoryMapAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult((false, (InventoryMap?)null));
        }
    }
}
