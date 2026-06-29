/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2024-2025, Sjofn LLC.
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the openmetaverse.co nor the names
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.Assets;
using LibreMetaverse.Imaging;

namespace LibreMetaverse
{
    public partial class AppearanceManager
    {
        #region Appearance Helpers

        /// <summary>
        /// Blocking method to populate the Textures array with cached bakes
        /// </summary>
        /// <returns>True on success, otherwise false</returns>
        private bool GetCachedBakes()
        {
            var cacheCheckEvent = new AutoResetEvent(false);
            EventHandler<AgentCachedBakesReplyEventArgs> CacheCallback = (sender, e) => cacheCheckEvent.Set();

            CachedBakesReply += CacheCallback;

            RequestCachedBakes();

            var success = cacheCheckEvent.WaitOne(WEARABLE_TIMEOUT, false);

            CachedBakesReply -= CacheCallback;

            return success;
        }

        /// <summary>
        /// Async method to populate the Textures array with cached bakes
        /// </summary>
        /// <returns>True on success, otherwise false</returns>
        private async Task<bool> GetCachedBakesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<AgentCachedBakesReplyEventArgs> CacheCallback = (sender, e) => tcs.TrySetResult(true);

            CachedBakesReply += CacheCallback;

            try
            {
                RequestCachedBakes();

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(WEARABLE_TIMEOUT, cancellationToken))
                    .ConfigureAwait(false);

                if (completed == tcs.Task)
                    return tcs.Task.Result;

                tcs.TrySetCanceled();
                return false;
            }
            finally
            {
                CachedBakesReply -= CacheCallback;
            }
        }

        /// <summary>
        /// Async method to download and parse currently worn wearable assets
        /// </summary>
        /// <returns>True on success, otherwise false</returns>
        private async Task<bool> DownloadWearablesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var success = true;
            var wearables = new List<WearableData>(GetWearables());

            // We will refresh the textures (zero out all non bake textures)
            for (var i = 0; i < Textures.Length; i++)
            {
                var isBake = BakeIndexToTextureIndex.Any(t => t == i);
                if (!isBake)
                    Textures[i] = new TextureData();
            }

            var pendingWearables = wearables.Count;

            foreach (var wearable in wearables.Where(wearable => wearable.Asset != null))
            {
                DecodeWearableParams(wearable, ref Textures);
                --pendingWearables;
            }

            if (pendingWearables == 0)
                return true;

            Logger.DebugLog($"Downloading {pendingWearables} wearable assets");

            using (var semaphore = new SemaphoreSlim(MAX_CONCURRENT_DOWNLOADS))
            {
                var tasks = wearables.Select(async wearable =>
                {
                    if (wearable.Asset != null) return;

                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        using var wearableCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        wearableCts.CancelAfter(WEARABLE_TIMEOUT);
                        Asset? asset = null;
                        try
                        {
                            asset = await Client.Assets.RequestAssetAsync(wearable.AssetID, wearable.AssetType, true, wearableCts.Token)
                                .ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                        {
                            Logger.Error($"Timed out downloading wearable asset {wearable.AssetID} ({wearable.WearableType})", Client);
                            success = false;
                        }

                        if (asset is AssetWearable assetWearable)
                        {
                            wearable.Asset = assetWearable;

                            if (wearable.Asset.Decode())
                            {
                                DecodeWearableParams(wearable, ref Textures);
                                Logger.DebugLog("Downloaded wearable asset " + wearable.WearableType + " with " +
                                                wearable.Asset.Params.Count +
                                                " visual params and " + wearable.Asset.Textures.Count + " textures",
                                    Client);
                            }
                            else
                            {
                                wearable.Asset = null;
                                Logger.Error("Failed to decode asset:" + Environment.NewLine +
                                             Utils.BytesToString(assetWearable.AssetData), Client);
                                success = false;
                            }
                        }
                        else if (asset != null)
                        {
                            Logger.Warn($"Wearable {wearable.AssetID} ({wearable.WearableType}) failed to download or wrong asset type", Client);
                            success = false;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Downloading wearable {wearable.AssetID} failed: {ex}", Client);
                        success = false;
                    }
                    finally
                    {
                        semaphore.Release();
                        Interlocked.Decrement(ref pendingWearables);
                    }
                }).ToArray();

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            return success;
        }


        /// <summary>
        /// Get a list of all textures that need to be downloaded for a single bake layer
        /// </summary>
        /// <param name="bakeType">Bake layer to get texture AssetIDs for</param>
        /// <returns>A list of texture AssetIDs to download</returns>
        private IEnumerable<UUID> GetTextureDownloadList(BakeType bakeType)
        {
            var indices = BakeTypeToTextures(bakeType);
            var textures = new List<UUID>();

            foreach (var textureData in from index in indices
                     where index != AvatarTextureIndex.Skirt || Wearables.ContainsKey(WearableType.Skirt)
                     select Textures[(int)index]
                     into textureData
                     where textureData.TextureID != UUID.Zero && textureData.Texture == null
                                                              && !textures.Contains(textureData.TextureID)
                     select textureData)
            {
                textures.Add(textureData.TextureID);
            }

            return textures;
        }

        /// <summary>
        /// Async method to download all textures needed for baking the given bake layers
        /// </summary>
        /// <param name="bakeLayers">A list of layers that need baking</param>
        /// <param name="cancellationToken"></param>
        private async Task DownloadTexturesAsync(List<BakeType> bakeLayers, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var textureIDs = new List<UUID>();

            foreach (var uuid in from t in bakeLayers
                     select GetTextureDownloadList(t)
                     into layerTextureIDs
                     from uuid in layerTextureIDs
                     where !textureIDs.Contains(uuid)
                     select uuid)
            {
                textureIDs.Add(uuid);
            }

            Logger.DebugLog("Downloading " + textureIDs.Count + " textures for baking");

            using (var semaphore = new SemaphoreSlim(MAX_CONCURRENT_DOWNLOADS))
            {
                var tasks = textureIDs.Select(async textureId =>
                {
                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        using var texCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        texCts.CancelAfter(TEXTURE_TIMEOUT);
                        AssetTexture? assetTexture = null;
                        try
                        {
                            assetTexture = await TextureProvider.RequestTextureAsync(textureId, texCts.Token)
                                .ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                        {
                            Logger.Warn($"Texture {textureId} failed to download, one or more bakes will be incomplete");
                        }

                        if (assetTexture != null)
                        {
                            try { assetTexture.Decode(); }
                            catch (Exception decodeEx) { Logger.Debug($"Failed to decode texture {textureId}: {decodeEx}", Client); }

                            foreach (var tex in Textures)
                            {
                                if (tex.TextureID == textureId)
                                    tex.Texture = assetTexture;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        Logger.Warn($"Download of texture {textureId} failed with exception {e}", Client);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }).ToArray();

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Async method to create and upload baked textures for all missing bakes
        /// </summary>
        /// <returns>True on success, otherwise false</returns>
        private async Task<bool> CreateBakesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var success = true;
            var pendingBakes = new List<BakeType>();

            for (var bakedIndex = 0; bakedIndex < BAKED_TEXTURE_COUNT; bakedIndex++)
            {
                var textureIndex = BakeTypeToAgentTextureIndex((BakeType)bakedIndex);

                if (Textures[(int)textureIndex].TextureID == UUID.Zero)
                {
                    if (bakedIndex == (int)BakeType.Skirt && !Wearables.ContainsKey(WearableType.Skirt))
                    {
                        Logger.DebugLog($"texture: {textureIndex} skipping not attached");
                        continue;
                    }

                    Logger.DebugLog($"texture: {textureIndex} is needed adding to pending Bakes");
                    pendingBakes.Add((BakeType)bakedIndex);
                }
                else
                {
                    Logger.DebugLog($"texture: {textureIndex} is ready");
                }
            }

            if (pendingBakes.Any())
            {
                await DownloadTexturesAsync(pendingBakes, cancellationToken).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                using (var semaphore = new SemaphoreSlim(MAX_CONCURRENT_UPLOADS))
                {
                    var tasks = pendingBakes.Select(async bakeType =>
                    {
                        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                        try
                        {
                            if (!await CreateBakeAsync(bakeType, cancellationToken).ConfigureAwait(false))
                                success = false;
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }).ToArray();

                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            }

            // Free up all the textures we're holding on to
            foreach (var tex in Textures)
            {
                tex.Texture = null;
            }

            return success;
        }

        /// <summary>
        /// Async method to create and upload a baked texture for a single bake layer
        /// </summary>
        /// <param name="bakeType">Layer to bake</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True on success, otherwise false</returns>
        private async Task<bool> CreateBakeAsync(BakeType bakeType, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var textureIndices = BakeTypeToTextures(bakeType);
            var oven = new Baker(bakeType);

            foreach (var textureIndex in textureIndices)
            {
                var texture = Textures[(int)textureIndex];
                texture.TextureIndex = textureIndex;
                oven.AddTexture(texture);
            }

            var start = Environment.TickCount;
            await Task.Run(() => oven.Bake(), cancellationToken).ConfigureAwait(false);
            Logger.DebugLog($"Baking {bakeType} took {Environment.TickCount - start}ms");

            var newAssetID = UUID.Zero;
            var retries = UPLOAD_RETRIES;

            while (newAssetID == UUID.Zero && retries > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (oven.BakedTexture == null || oven.BakedTexture.AssetData == null)
                {
                    Logger.Warn($"Baker produced no texture data for {bakeType}", Client);
                    break;
                }

                newAssetID = await UploadBakeAsync(oven.BakedTexture.AssetData, cancellationToken).ConfigureAwait(false);
                --retries;
            }

            var bakeIndex = (int)BakeTypeToAgentTextureIndex(bakeType);
            Logger.DebugLog($"Saving back to {(AvatarTextureIndex)bakeIndex}");

            Textures[bakeIndex].TextureID = newAssetID;

            if (newAssetID == UUID.Zero)
            {
                Logger.Warn($"Failed uploading bake {bakeType}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Async method to upload a baked texture
        /// </summary>
        /// <param name="textureData">Five channel JPEG2000 texture data to upload</param>
        /// <param name="cancellationToken"></param>
        /// <returns>UUID of the newly created asset on success, otherwise UUID.Zero</returns>
        private async Task<UUID> UploadBakeAsync(byte[] textureData, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var uploadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            uploadCts.CancelAfter(UPLOAD_TIMEOUT);
            try
            {
                return await Client.Assets.RequestUploadBakedTextureAsync(textureData, uploadCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return UUID.Zero;
            }
        }






        #endregion Appearance Helpers

        #region Static Helpers

        /// <summary>
        /// Converts a BakeType to the corresponding baked texture slot in AvatarTextureIndex
        /// </summary>
        /// <param name="index">A BakeType</param>
        /// <returns>The AvatarTextureIndex slot that holds the given BakeType</returns>
        public static AvatarTextureIndex BakeTypeToAgentTextureIndex(BakeType index)
        {
            switch (index)
            {
                case BakeType.Head:
                    return AvatarTextureIndex.HeadBaked;
                case BakeType.UpperBody:
                    return AvatarTextureIndex.UpperBaked;
                case BakeType.LowerBody:
                    return AvatarTextureIndex.LowerBaked;
                case BakeType.Eyes:
                    return AvatarTextureIndex.EyesBaked;
                case BakeType.Skirt:
                    return AvatarTextureIndex.SkirtBaked;
                case BakeType.Hair:
                    return AvatarTextureIndex.HairBaked;
                case BakeType.BakedLeftArm:
                    return AvatarTextureIndex.LeftArmBaked;
                case BakeType.BakedLeftLeg:
                    return AvatarTextureIndex.LegLegBaked;
                case BakeType.BakedAux1:
                    return AvatarTextureIndex.Aux1Baked;
                case BakeType.BakedAux2:
                    return AvatarTextureIndex.Aux2Baked;
                case BakeType.BakedAux3:
                    return AvatarTextureIndex.Aux3Baked;
                default:
                    return AvatarTextureIndex.Unknown;
            }
        }

        /// <summary>
        /// Gives the layer number that is used for morph mask
        /// </summary>
        /// <param name="bakeType">>A BakeType</param>
        /// <returns>Which layer number as defined in BakeTypeToTextures is used for morph mask</returns>
        public static AvatarTextureIndex MorphLayerForBakeType(BakeType bakeType)
        {
            // Indexes return here correspond to those returned
            // in BakeTypeToTextures(), those two need to be in sync.
            // Which wearable layer is used for morph is defined in avatar_lad.xml
            // by looking for <layer> that has <morph_mask> defined in it, and
            // looking up which wearable is defined in that layer. Morph mask
            // is never combined, it's always a straight copy of one single clothing
            // item's alpha channel per bake.
            switch (bakeType)
            {
                case BakeType.Head:
                    return AvatarTextureIndex.Hair; // hair
                case BakeType.UpperBody:
                    return AvatarTextureIndex.UpperShirt; // shirt
                case BakeType.LowerBody:
                    return AvatarTextureIndex.LowerPants; // lower pants
                case BakeType.Skirt:
                    return AvatarTextureIndex.Skirt; // skirt
                case BakeType.Hair:
                    return AvatarTextureIndex.Hair; // hair
                case BakeType.BakedLeftArm:
                    return AvatarTextureIndex.LeftArmTattoo;
                case BakeType.BakedLeftLeg:
                    return AvatarTextureIndex.LeftLegTattoo;
                case BakeType.BakedAux1:
                    return AvatarTextureIndex.Aux1Tattoo;
                case BakeType.BakedAux2:
                    return AvatarTextureIndex.Aux2Tattoo;
                case BakeType.BakedAux3:
                    return AvatarTextureIndex.Aux3Tattoo;
                default:
                    return AvatarTextureIndex.Unknown;
            }
        }

        /// <summary>
        /// Converts a BakeType to a list of the texture slots that make up that bake
        /// </summary>
        /// <param name="bakeType">A BakeType</param>
        /// <returns>A list of texture slots that are inputs for the given bake</returns>
        public static List<AvatarTextureIndex> BakeTypeToTextures(BakeType bakeType)
        {
            var textures = new List<AvatarTextureIndex>();

            switch (bakeType)
            {
                case BakeType.Head:
                    textures.Add(AvatarTextureIndex.HeadBodypaint);
                    textures.Add(AvatarTextureIndex.HeadTattoo);
                    textures.Add(AvatarTextureIndex.HeadUniversalTattoo);
                    textures.Add(AvatarTextureIndex.Hair);
                    textures.Add(AvatarTextureIndex.HeadAlpha);
                    break;
                case BakeType.UpperBody:
                    textures.Add(AvatarTextureIndex.UpperBodypaint);
                    textures.Add(AvatarTextureIndex.UpperTattoo);
                    textures.Add(AvatarTextureIndex.UpperUniversalTattoo);
                    textures.Add(AvatarTextureIndex.UpperGloves);
                    textures.Add(AvatarTextureIndex.UpperUndershirt);
                    textures.Add(AvatarTextureIndex.UpperShirt);
                    textures.Add(AvatarTextureIndex.UpperJacket);
                    textures.Add(AvatarTextureIndex.UpperAlpha);
                    break;
                case BakeType.LowerBody:
                    textures.Add(AvatarTextureIndex.LowerBodypaint);
                    textures.Add(AvatarTextureIndex.LowerTattoo);
                    textures.Add(AvatarTextureIndex.LowerUniversalTattoo);
                    textures.Add(AvatarTextureIndex.LowerUnderpants);
                    textures.Add(AvatarTextureIndex.LowerSocks);
                    textures.Add(AvatarTextureIndex.LowerShoes);
                    textures.Add(AvatarTextureIndex.LowerPants);
                    textures.Add(AvatarTextureIndex.LowerJacket);
                    textures.Add(AvatarTextureIndex.LowerAlpha);
                    break;
                case BakeType.Eyes:
                    textures.Add(AvatarTextureIndex.EyesIris);
                    textures.Add(AvatarTextureIndex.EyesTattoo);
                    textures.Add(AvatarTextureIndex.EyesAlpha);
                    break;
                case BakeType.Skirt:
                    textures.Add(AvatarTextureIndex.Skirt);
                    textures.Add(AvatarTextureIndex.SkirtTattoo);
                    break;
                case BakeType.Hair:
                    textures.Add(AvatarTextureIndex.Hair);
                    textures.Add(AvatarTextureIndex.HairTattoo);
                    textures.Add(AvatarTextureIndex.HairAlpha);
                    break;
                case BakeType.BakedLeftArm:
                    textures.Add(AvatarTextureIndex.LeftArmTattoo);
                    break;
                case BakeType.BakedLeftLeg:
                    textures.Add(AvatarTextureIndex.LeftLegTattoo);
                    break;
                case BakeType.BakedAux1:
                    textures.Add(AvatarTextureIndex.Aux1Tattoo);
                    break;
                case BakeType.BakedAux2:
                    textures.Add(AvatarTextureIndex.Aux2Tattoo);
                    break;
                case BakeType.BakedAux3:
                    textures.Add(AvatarTextureIndex.Aux3Tattoo);
                    break;
            }

            return textures;
        }

        #endregion Static Helpers
    }
}
