/*
 * Copyright (c) 2026, Sjofn LLC.
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

using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.Assets;

namespace LibreMetaverse
{
    /// <summary>
    /// Fetches decoded texture images for the avatar baking pipeline.
    /// <para>
    /// Implement this interface to supply textures from a non-standard source —
    /// for example, an OpenSimulator server-side baking (SSB) service that reads
    /// directly from an OpenSim asset database rather than downloading over HTTPS.
    /// </para>
    /// <para>
    /// The default implementation, <see cref="GridClientBakingTextureProvider"/>,
    /// fetches textures via <see cref="AssetManager.RequestImageAsync"/>.
    /// Override <see cref="AppearanceManager.TextureProvider"/> to swap it out.
    /// </para>
    /// </summary>
    public interface IBakingTextureProvider
    {
        /// <summary>
        /// Asynchronously fetches and decodes the texture identified by
        /// <paramref name="textureID"/>.
        /// </summary>
        /// <param name="textureID">Asset UUID of the texture to fetch.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// The decoded <see cref="AssetTexture"/>, or <c>null</c> if the texture
        /// could not be fetched or decoded.
        /// </returns>
        Task<AssetTexture?> RequestTextureAsync(UUID textureID,
            CancellationToken cancellationToken = default);
    }
}
