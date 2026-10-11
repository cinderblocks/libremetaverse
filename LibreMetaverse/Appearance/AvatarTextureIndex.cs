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

namespace LibreMetaverse
{
    /// <summary>
    /// Index of TextureEntry slots for avatar appearances
    /// </summary>
    public enum AvatarTextureIndex
    {
        Unknown = -1,
        HeadBodypaint = 0,
        UpperShirt,
        LowerPants,
        EyesIris,
        Hair,
        UpperBodypaint,
        LowerBodypaint,
        LowerShoes,
        HeadBaked,  // pre-composited
        UpperBaked, // pre-composited
        LowerBaked, // pre-composited
        EyesBaked,  // pre-composited
        LowerSocks,
        UpperJacket,
        LowerJacket,
        UpperGloves,
        UpperUndershirt,
        LowerUnderpants,
        Skirt,
        SkirtBaked, // pre-composited
        HairBaked,  // pre-composited
        LowerAlpha,
        UpperAlpha,
        HeadAlpha,
        EyesAlpha,
        HairAlpha,
        HeadTattoo,
        UpperTattoo,
        LowerTattoo,
        HeadUniversalTattoo,
        UpperUniversalTattoo,
        LowerUniversalTattoo,
        SkirtTattoo,
        HairTattoo,
        EyesTattoo,
        LeftArmTattoo,
        LeftLegTattoo,
        Aux1Tattoo,
        Aux2Tattoo,
        Aux3Tattoo,
        LeftArmBaked,   // pre-composited
        LegLegBaked,    // pre-composited
        Aux1Baked,  // pre-composited
        Aux2Baked,  // pre-composited
        Aux3Baked,  // pre-composited
        NumberOfEntries
    }
}
