/*
 * Copyright (c) 2026, Sjofn LLC
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

using LibreMetaverse.Messages.Linden;
using LibreMetaverse.StructuredData;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for the ProductInfoRequest capability, which returns a bare LLSD array of
    /// region-type SKU-to-description entries. Wire format verified against the reference viewer's
    /// LLProductInfoRequestManager::getLandDescriptionsCoro/getDescriptionForSku
    /// (llproductinforequest.cpp): each entry has exactly "sku", "name", and "description" --
    /// an earlier version of this message invented a "description_sale"/"description_renewal"
    /// split that the server never sends.
    /// </summary>
    [TestFixture]
    [Category("ProductInfo")]
    public class ProductInfoRequestMessageTests
    {
        [Test]
        public void Deserialize_BareArray_ReadsSkuNameDescription()
        {
            var arr = new OSDArray
            {
                new OSDMap
                {
                    ["sku"] = OSD.FromString("023"),
                    ["name"] = OSD.FromString("Homestead"),
                    ["description"] = OSD.FromString("A Homestead region")
                }
            };

            var msg = new ProductInfoRequestMessage();
            // Mirrors AgentManager.GetProductInfoAsync's handling of a bare-array response.
            msg.Deserialize(new OSDMap { ["products"] = arr });

            Assert.That(msg.Products, Has.Count.EqualTo(1));
            Assert.That(msg.Products[0].Sku, Is.EqualTo("023"));
            Assert.That(msg.Products[0].Name, Is.EqualTo("Homestead"));
            Assert.That(msg.Products[0].Description, Is.EqualTo("A Homestead region"));
        }

        [Test]
        public void SerializeDeserialize_RoundTrips()
        {
            var msg = new ProductInfoRequestMessage();
            msg.Products.Add(new ProductInfoRequestMessage.ProductInfo
            {
                Sku = "001", Name = "Full Region", Description = "A full region"
            });

            var roundTripped = new ProductInfoRequestMessage();
            roundTripped.Deserialize(msg.Serialize());

            Assert.That(roundTripped.Products, Has.Count.EqualTo(1));
            Assert.That(roundTripped.Products[0].Sku, Is.EqualTo("001"));
            Assert.That(roundTripped.Products[0].Name, Is.EqualTo("Full Region"));
            Assert.That(roundTripped.Products[0].Description, Is.EqualTo("A full region"));
        }
    }
}
