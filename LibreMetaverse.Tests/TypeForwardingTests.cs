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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Types moved out of LibreMetaverse.dll into the packages it references. An application compiled against 3.1.8
    /// names them by the assembly "LibreMetaverse", and the type forwarders in that assembly are what lets it load
    /// them from where they are now. PublicTypes-3.1.8.tsv is every public top-level type 3.1.8 had in LibreMetaverse.dll.
    /// A type that moves, or goes, and is not forwarded fails here.
    /// </summary>
    [TestFixture]
    public class TypeForwardingTests
    {
        private static readonly Assembly Core = typeof(GridClient).Assembly;

        /// <summary>Type name, and for the ones that cannot be forwarded the package they are in now.</summary>
        private static List<(string Name, string MovedTo)> PublicTypes318()
        {
            using (var stream = typeof(TypeForwardingTests).Assembly.GetManifestResourceStream("PublicTypes-3.1.8.tsv"))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.TrimEnd('\r').Split('\t'))
                    .Select(p => (p[0], p.Length > 1 ? p[1] : string.Empty))
                    .ToList();
            }
        }

        [Test]
        public void EveryPublicTypeOf318_StillLoadsFromLibreMetaverse_ExceptTheOnesInPackagesTheCoreCannotReach()
        {
            var missing = new List<string>();
            int resolved = 0;

            foreach (var (name, movedTo) in PublicTypes318().Where(t => t.MovedTo.Length == 0))
            {
                var type = Type.GetType(name + ", " + Core.GetName().Name, throwOnError: false);
                if (type == null || type.FullName != name || !type.IsPublic)
                    missing.Add(name);
                else
                    resolved++;
            }

            Assert.That(missing, Is.Empty, "types of 3.1.8 that an application compiled against it can no longer load");
            Assert.That(resolved, Is.GreaterThan(1100));
        }

        [Test]
        public void TheTypesThatAreNotForwarded_AreTheModelFormatsAndArchiveOnes_AndNothingElse()
        {
            var notForwarded = PublicTypes318().Where(t => t.MovedTo.Length > 0).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(notForwarded.Select(t => t.MovedTo).Distinct().OrderBy(x => x),
                    Is.EqualTo(new[] { "LibreMetaverse.Archiving", "LibreMetaverse.ModelFormats" }));
                Assert.That(notForwarded.Count(t => t.MovedTo == "LibreMetaverse.Archiving"), Is.EqualTo(7));
                Assert.That(notForwarded.Select(t => t.Name), Does.Contain("LibreMetaverse.Assets.OarFile"));
                Assert.That(notForwarded.Select(t => t.Name), Does.Contain("LibreMetaverse.ImportExport.ColladaLoader"));

                // They really are not in this assembly; if one were, the list is out of date
                foreach (var (name, movedTo) in notForwarded)
                    Assert.That(Core.GetType(name, throwOnError: false), Is.Null, name);
            });
        }

#if NET5_0_OR_GREATER
        [Test]
        public void EveryForwarderPointsAtAPublicTypeInAnotherAssemblyThatTheCoreReferences()
        {
            // Forwarders are not custom attributes at run time; the assembly lists them as exported types, nested ones too
            var forwarded = Core.GetForwardedTypes();
            var referenced = new HashSet<string>(Core.GetReferencedAssemblies().Select(a => a.Name));
            var bad = new List<string>();

            foreach (var destination in forwarded)
            {
                if (!(destination.IsPublic || destination.IsNestedPublic) || destination.Assembly == Core || !referenced.Contains(destination.Assembly.GetName().Name))
                    bad.Add(destination.FullName + " in " + destination.Assembly.GetName().Name);
            }

            Assert.That(bad, Is.Empty);
            Assert.That(forwarded, Has.Length.GreaterThan(800));
        }
#endif

        [Test]
        public void AForwardedTypeIsTheSameTypeWhichEverAssemblyNameFindsIt()
        {
            // Packets, from LibreMetaverse.Protocol; asset types, from LibreMetaverse.Assets; the locks, from LibreMetaverse.Types
            foreach (var name in new[] { "LibreMetaverse.Packets.ChatFromViewerPacket", "LibreMetaverse.Assets.AssetTexture", "LibreMetaverse.Threading.SpinReaderWriterLock" })
            {
                var viaCore = Type.GetType(name + ", LibreMetaverse", throwOnError: true);
                Assert.That(viaCore.Assembly, Is.Not.SameAs(Core), name);
                Assert.That(Type.GetType(name + ", " + viaCore.Assembly.GetName().Name), Is.SameAs(viaCore), name);
            }
        }
    }
}
