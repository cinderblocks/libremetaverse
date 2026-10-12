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

namespace LibreMetaverse.ImportExport
{
    /// <summary>How much a <see cref="ModelDiagnostic"/> matters</summary>
    public enum ModelDiagnosticSeverity
    {
        /// <summary>Something was left out or approximated, and the rest is usable</summary>
        Warning,

        /// <summary>The file yielded nothing usable</summary>
        Error
    }

    /// <summary>
    /// What a <see cref="ModelDiagnostic"/> is about, for a program that wants to say something different or offer
    /// something different for each. The codes are stable; the messages are not.
    /// </summary>
    public enum ModelDiagnosticCode
    {
        /// <summary>The file could not be read: it is missing, or access to it was refused</summary>
        FileUnreadable,

        /// <summary>The file was read but is not a valid model, or is damaged</summary>
        FileMalformed,

        /// <summary>The file is valid but needs something the loader does not support, such as a required glTF extension</summary>
        FileUnsupported,

        /// <summary>
        /// Something is bigger than a limit allows: the loader's (file size, vertices, materials, joints, line length)
        /// or the upload's (vertices in a face, joints in a skin)
        /// </summary>
        LimitExceeded,

        /// <summary>The loader failed on something it did not expect. The file is refused, but this is a gap in the loader</summary>
        LoaderFailed,

        /// <summary>A mesh or prim has no triangles, so there is nothing to load, export or upload</summary>
        NoGeometry,

        /// <summary>A whole piece of geometry was left out: a primitive that is not a triangle list, or a prim that has no size</summary>
        PrimitiveSkipped,

        /// <summary>A face was left out because it is unusable (too many vertices, too few corners, a broken triangle or a position that is not a number)</summary>
        FaceSkipped,

        /// <summary>
        /// A mesh has more faces than Second Life allows, which is eight. A loader warns, since it can still give you
        /// the mesh; <see cref="ModelUploadChecks"/> makes it an error, because the viewer cannot upload one.
        /// </summary>
        TooManyFaces,

        /// <summary>A node or object was left out of the model</summary>
        NodeSkipped,

        /// <summary>A texture could not be loaded, so the faces that use it have none</summary>
        TextureNotLoaded,

        /// <summary>
        /// A texture lies outside the model's directory and was not loaded. See <c>RestrictTexturesToModelDirectory</c>
        /// on the loader.
        /// </summary>
        TextureOutsideModelDirectory,

        /// <summary>A material library could not be found or read, so its materials are plain white</summary>
        MaterialLibraryNotLoaded,

        /// <summary>The avatar skeleton could not be loaded, so joint names are used without being checked</summary>
        SkeletonUnavailable,

        /// <summary>Joints of a skin that are not on the avatar skeleton were left out</summary>
        JointsIgnored,

        /// <summary>A skinned mesh was loaded without its rig: none of its joints are the avatar's, or the skin is of a kind that is not supported</summary>
        RigDropped,

        /// <summary>Vertices with no weight to a joint of the avatar follow the first joint instead</summary>
        UnweightedVertices,

        /// <summary>A vertex has more influences than Second Life takes, which is four, and the rest were left out</summary>
        InfluencesIgnored,

        /// <summary>A rig was taken exactly as it is although the file is not Z-up in meters</summary>
        RigAxesNotApplied,

        /// <summary>A rig could not be worked out exactly (a matrix cannot be inverted), and an approximation was used</summary>
        RigApproximated,

        /// <summary>A prim whose parent was not added was exported on its own</summary>
        LinksetBroken,

        /// <summary>A rigged mesh was exported without its skin, in the pose its bind shape gives it</summary>
        RigFlattened,

        /// <summary>A texture could not be exported, so the faces that use it have none</summary>
        TextureNotExported,

        /// <summary>Geometry or rig data that cannot be uploaded: an index past the vertices, a number that is not a number, or weights that do not fit the skin</summary>
        InvalidModel,

        /// <summary>A texture's pixel size is not known, so the upload fee is quoted from the whole texture</summary>
        TextureSizeUnknown,

        /// <summary>
        /// A glTF material has something a Second Life material cannot hold (a second set of texture coordinates, a normal
        /// scale, an occlusion strength, or a <c>KHR_materials_*</c> extension), and it was left out
        /// </summary>
        PbrApproximated
    }

    /// <summary>One thing a loader or exporter wants you to know about</summary>
    public sealed class ModelDiagnostic
    {
        public ModelDiagnosticSeverity Severity { get; }

        public ModelDiagnosticCode Code { get; }

        /// <summary>A description for a person. It is not stable: use <see cref="Code"/> to decide what to do</summary>
        public string Message { get; }

        /// <summary>
        /// What it is about, such as the name of an object, a joint or a texture file, or null. It comes from the
        /// file, so it is whatever the file's author wrote.
        /// </summary>
        public string? Subject { get; }

        /// <summary>How many times it happened, for things that are counted rather than listed (such as faces)</summary>
        public int Count { get; }

        public ModelDiagnostic(ModelDiagnosticSeverity severity, ModelDiagnosticCode code, string message,
            string? subject = null, int count = 1)
        {
            Severity = severity;
            Code = code;
            Message = message ?? string.Empty;
            Subject = subject;
            Count = count;
        }

        public override string ToString() => $"{Severity} {Code}: {Message}";
    }

    /// <summary>
    /// Gathers the <see cref="ModelDiagnostic"/> entries of one load or export. Each is also written to the log
    /// as it is reported, at the level it always was.
    /// </summary>
    public sealed class ModelDiagnosticCollector
    {
        private readonly List<ModelDiagnostic> _items = new List<ModelDiagnostic>();

        /// <summary>A snapshot of what has been reported so far, in order</summary>
        public IReadOnlyList<ModelDiagnostic> Diagnostics
        {
            get
            {
                lock (_items) return _items.ToArray();
            }
        }

        public bool HasErrors
        {
            get
            {
                lock (_items) return _items.Exists(d => d.Severity == ModelDiagnosticSeverity.Error);
            }
        }

        /// <summary>Reports something that was left out or approximated</summary>
        public void Warn(ModelDiagnosticCode code, string message, string? subject = null, int count = 1)
        {
            Add(new ModelDiagnostic(ModelDiagnosticSeverity.Warning, code, message, subject, count));
            Logger.Warn(message);
        }

        /// <summary>Reports that the file yielded nothing usable</summary>
        public void Error(ModelDiagnosticCode code, string message, string? subject = null, int count = 1, Exception? exception = null)
        {
            Add(new ModelDiagnostic(ModelDiagnosticSeverity.Error, code, message, subject, count));
            if (exception != null)
                Logger.Error(message, exception);
            else
                Logger.Error(message);
        }

        private void Add(ModelDiagnostic diagnostic)
        {
            lock (_items) _items.Add(diagnostic);
        }
    }

    /// <summary>The outcome of loading a model file</summary>
    public sealed class ModelLoadResult
    {
        /// <summary>What was loaded. Empty if the file could not be loaded at all.</summary>
        public IReadOnlyList<ModelPrim> Prims { get; }

        /// <summary>What was left out, approximated or refused, in the order it happened</summary>
        public IReadOnlyList<ModelDiagnostic> Diagnostics { get; }

        /// <summary>True if the file yielded nothing usable, and <see cref="Diagnostics"/> says why</summary>
        public bool HasErrors { get; }

        public ModelLoadResult(IReadOnlyList<ModelPrim> prims, IReadOnlyList<ModelDiagnostic> diagnostics)
        {
            Prims = prims ?? throw new ArgumentNullException(nameof(prims));
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            HasErrors = false;
            foreach (var d in diagnostics)
            {
                if (d.Severity == ModelDiagnosticSeverity.Error) HasErrors = true;
            }
        }
    }

    /// <summary>
    /// A model loader that says what it left out. <see cref="IModelLoader.Load"/> gives the same prims and
    /// writes the same things to the log, but throws the rest away.
    /// </summary>
    public interface IDiagnosticModelLoader : IModelLoader
    {
        /// <summary>Parses a model file, and reports what it could not use</summary>
        /// <param name="filename">Load the model from this file</param>
        /// <param name="loadImages">Load and decode images for uploading with the model</param>
        ModelLoadResult LoadWithDiagnostics(string filename, bool loadImages);
    }
}
