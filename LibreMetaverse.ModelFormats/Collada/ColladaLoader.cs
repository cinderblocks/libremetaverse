/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2025, Sjofn LLC.
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
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml;
using System.Linq;
using System.Xml.Serialization;
using NumMatrix = System.Numerics.Matrix4x4;
using NumVector3 = System.Numerics.Vector3;
using LibreMetaverse.Imaging;
using LibreMetaverse.ImportExport.Collada14;
using LibreMetaverse.Rendering;

namespace LibreMetaverse.ImportExport
{
    /// <summary>
    /// Parsing Collada model files into data structures
    /// </summary>
    public class ColladaLoader : IDiagnosticModelLoader
    {
        private COLLADA? Model;
        private static XmlSerializer? Serializer = null;
        private List<Node> Nodes = new List<Node>();
        private List<ModelMaterial> Materials = new List<ModelMaterial>();
        private Dictionary<string, string> MatSymTarget = new Dictionary<string, string>();
        private string FileName = string.Empty;
        private readonly ITextureCodec? _textureCodec;
        private UpAxisType _upAxis = UpAxisType.Y_UP;
        private float _unitMeter = 1f;

        /// <summary>
        /// Only load textures from the directory of the model file and its subdirectories. A model
        /// file can name any path, so without this an untrusted model can make the loader read
        /// (and a caller upload) image files from anywhere the user can read. Set to false for
        /// models that keep their textures elsewhere, such as in a sibling directory.
        /// </summary>
        public bool RestrictTexturesToModelDirectory { get; set; } = true;

        /// <summary>
        /// For a rigged mesh, take the position and size of each joint from the file's inverse bind matrices, and
        /// use the rotation the avatar's joint has there. A rig built in a modelling program usually gives its bones
        /// axes of their own, which would turn the mesh around each joint on the avatar. A rig made for Second Life
        /// has the avatar's rotations already, whatever shape it was bound at, and comes out the same either way. Set
        /// to false to carry the matrices over exactly as authored, as the viewer's uploader does.
        /// </summary>
        public bool UseAvatarJointAxes { get; set; } = true;

        /// <summary>The most joints a rigged mesh may use; a mesh with more is refused</summary>
        public int MaxJoints { get; set; } = ModelSkin.MaxJoints;

        /// <summary>
        /// Creates a new Collada loader
        /// </summary>
        /// <param name="textureCodec">Decodes arbitrary (non-TGA, non-J2K) image formats referenced
        /// by the model. Reference LibreMetaverse.Imaging.Skia for a working implementation, or
        /// provide your own. Not required for models that only reference .tga/.jp2/.j2c textures.</param>
        public ColladaLoader(ITextureCodec? textureCodec = null)
        {
            _textureCodec = textureCodec;
        }

        private class Node
        {
            public Matrix4 Transform = Matrix4.Identity;
            public string Name = string.Empty;
            public string ID = string.Empty;
            public string MeshID = string.Empty;

            /// <summary>The controller (skin) this node instances, for a rigged mesh</summary>
            public string ControllerID = string.Empty;
        }

        /// <summary>
        /// Parses Collada document
        /// </summary>
        /// <param name="filename">Load .dae model from this file</param>
        /// <param name="loadImages">Load and decode images for uploading with model</param>
        /// <returns>A list of mesh prims that were parsed from the collada file</returns>
        [RequiresUnreferencedCode("Uses XmlSerializer with runtime-reflected COLLADA types. Not AOT-safe.")]
        public List<ModelPrim> Load(string filename, bool loadImages)
        {
            return new List<ModelPrim>(LoadWithDiagnostics(filename, loadImages).Prims);
        }

        /// <summary>
        /// Parses Collada document, and reports what it could not use
        /// </summary>
        /// <param name="filename">Load .dae model from this file</param>
        /// <param name="loadImages">Load and decode images for uploading with model</param>
        [RequiresUnreferencedCode("Uses XmlSerializer with runtime-reflected COLLADA types. Not AOT-safe.")]
        public ModelLoadResult LoadWithDiagnostics(string filename, bool loadImages)
        {
            _diagnostics = new ModelDiagnosticCollector();
            var prims = new List<ModelPrim>();
            try
            {
                // Create an instance of the XmlSerializer specifying type and namespace.
                if (Serializer == null)
                {
#pragma warning disable IL3050
                    Serializer = new XmlSerializer(typeof(COLLADA));
#pragma warning restore IL3050
                }

                this.FileName = filename;

                // A FileStream is needed to read the XML document.
                object? des;
                using (var fs = new FileStream(filename, FileMode.Open))
                using (var reader = XmlReader.Create(fs))
                {
#pragma warning disable IL3050
                    des = Serializer.Deserialize(reader);
#pragma warning restore IL3050
                }
                Model = des as COLLADA;
                if (Model == null)
                {
                    throw new InvalidOperationException("Failed to deserialize COLLADA document");
                }
                prims = Parse();
                if (loadImages)
                {
                    LoadImages(prims);
                }
            }
            catch (Exception ex)
            {
                _diagnostics.ReportFailure(ex, "collada");
                prims = new List<ModelPrim>();
            }
            return new ModelLoadResult(prims, _diagnostics.Diagnostics);
        }

        private ModelDiagnosticCollector _diagnostics = new ModelDiagnosticCollector();

        private void LoadImages(IEnumerable<ModelPrim> prims)
        {
            foreach (var prim in prims)
            {
                foreach (var face in prim.Faces)
                {
                    if (!string.IsNullOrEmpty(face.Material.Texture))
                    {
                        LoadImage(face.Material);
                    }
                }
            }
        }

        private void LoadImage(ModelMaterial material)
        {
            var fname = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(FileName) ?? string.Empty, material.Texture);

            if (RestrictTexturesToModelDirectory && !ModelTextureLoader.IsInModelDirectory(FileName, fname))
            {
                _diagnostics.Warn(ModelDiagnosticCode.TextureOutsideModelDirectory, $"Not loading texture {material.Texture}: it is outside the model's directory " +
                            "(see ColladaLoader.RestrictTexturesToModelDirectory)", material.Texture);
                return;
            }

            ModelTextureLoader.LoadFile(_diagnostics, fname, material.Texture, material, _textureCodec);
        }

        private ModelMaterial ExtractMaterial(object diffuse)
        {
            ModelMaterial ret = new ModelMaterial();
            if (diffuse is common_color_or_texture_typeColor color)
            {
                ret.DiffuseColor = new Color4(
                    Utils.Clamp((float)color.Values[0], 0f, 1f),
                    Utils.Clamp((float)color.Values[1], 0f, 1f),
                    Utils.Clamp((float)color.Values[2], 0f, 1f),
                    Utils.Clamp((float)color.Values[3], 0f, 1f));
            }
            else if (diffuse is common_color_or_texture_typeTexture tex)
            {
                ret.Texture = tex.texcoord;
            }
            return ret;

        }

        private void ParseMaterials()
        {

            if (Model == null) return;

            Materials = new List<ModelMaterial>();

            // Material -> effect mapping
            var matEffect = new Dictionary<string, string>();
            var tmpEffects = new List<ModelMaterial>();

            // Image ID -> filename mapping
            Dictionary<string, string> imgMap = new Dictionary<string, string>();

            foreach (var item in Model.Items)
            {
                if (item is library_images images)
                {
                    if (images.image != null)
                    {
                        foreach (var image in images.image)
                        {
                            var img = (image)image;
                            string ID = img.id;
                            if (img.Item is string imgItem)
                            {
                                imgMap[ID] = imgItem;
                            }
                        }
                    }
                }
            }

            foreach (var item in Model.Items)
            {
                if (item is library_materials materials)
                {
                    if (materials.material != null)
                    {
                        foreach (var material in materials.material)
                        {
                            var ID = material.id;
                            var effectUrl = material.instance_effect?.url;
                            if (!string.IsNullOrEmpty(effectUrl))
                            {
                                matEffect[effectUrl!.Substring(1)] = ID;
                            }
                        }
                    }
                }
            }

            foreach (var item in Model.Items)
            {
                if (item is library_effects effects)
                {
                    if (effects.effect != null)
                    {
                        foreach (var effect in effects.effect)
                        {
                            string ID = effect.id;
                            foreach (var effItem in effect.Items)
                            {
                                if (effItem is effectFx_profile_abstractProfile_COMMON common)
                                {
                                    var teq = common.technique;
                                    if (teq != null)
                                    {
                                        if (teq.Item is effectFx_profile_abstractProfile_COMMONTechniquePhong phong)
                                        {
                                            if (phong.diffuse != null)
                                            {
                                                var material = ExtractMaterial(phong.diffuse.Item);
                                                material.ID = ID;
                                                tmpEffects.Add(material);
                                            }
                                        }
                                        else if (teq.Item is effectFx_profile_abstractProfile_COMMONTechniqueLambert shader)
                                        {
                                            if (shader.diffuse != null)
                                            {
                                                var material = ExtractMaterial(shader.diffuse.Item);
                                                material.ID = ID;
                                                tmpEffects.Add(material);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            foreach (var effect in tmpEffects)
            {
                if (matEffect.TryGetValue(effect.ID, out var effectId))
                {
                    effect.ID = effectId;
                    if (!string.IsNullOrEmpty(effect.Texture))
                    {
                        if (imgMap.TryGetValue(effect.Texture, out var effectTexture))
                        {
                            effect.Texture = effectTexture;
                        }
                    }
                    Materials.Add(effect);
                }
            }
        }

        private void ProcessNode(node node)
        {
            Node n = new Node {Name = node.name, ID = node.id};

            if (node.Items != null)
            {
                // Try finding matrix
                foreach (var i in node.Items)
                {
                    if (i is matrix mtx)
                    {
                        n.Transform = new Matrix4(
                            (float)mtx.Values[0],  (float)mtx.Values[4],  (float)mtx.Values[8],  (float)mtx.Values[12],
                            (float)mtx.Values[1],  (float)mtx.Values[5],  (float)mtx.Values[9],  (float)mtx.Values[13],
                            (float)mtx.Values[2],  (float)mtx.Values[6],  (float)mtx.Values[10], (float)mtx.Values[14],
                            (float)mtx.Values[3],  (float)mtx.Values[7],  (float)mtx.Values[11], (float)mtx.Values[15]);
                    }
                }
            }

            // Find geometry and material
            if (node.instance_geometry != null && node.instance_geometry.Length > 0)
            {
                var instGeom = node.instance_geometry[0];
                if (!string.IsNullOrEmpty(instGeom.url))
                {
                    n.MeshID = instGeom.url.Substring(1);
                }
                BindMaterials(instGeom.bind_material);
            }

            // A skinned mesh is instanced through its controller. It is placed by its joints, so the node's own
            // transform only matters if the skin turns out not to be one we can use.
            if (node.instance_controller != null && node.instance_controller.Length > 0)
            {
                var instController = node.instance_controller[0];
                if (!string.IsNullOrEmpty(instController.url) && instController.url.Length > 1)
                {
                    Nodes.Add(new Node { Name = node.name, ID = node.id, Transform = n.Transform, ControllerID = instController.url.Substring(1) });
                    BindMaterials(instController.bind_material);
                }
            }

            if (node.Items != null && node.instance_geometry != null && node.instance_geometry.Length > 0)
                Nodes.Add(n);

            // Recurse if the scene is hierarchical
            if (node.node1 != null)
            {
                foreach (node nd in node.node1)
                    ProcessNode(nd);
            }
        }

        private void BindMaterials(bind_material? bind)
        {
            if (bind?.technique_common == null) return;

            foreach (var teq in bind.technique_common)
            {
                var target = teq.target;
                if (string.IsNullOrEmpty(target)) continue;

                target = target.Substring(1);
                MatSymTarget[teq.symbol] = target;
            }
        }

        private void ParseVisualScene()
        {
            Nodes = new List<Node>();
            if (Model == null) return;

            MatSymTarget = new Dictionary<string, string>();

            foreach (var item in Model.Items)
            {
                if (item is library_visual_scenes scenes)
                {
                    var scene = scenes.visual_scene[0];
                    foreach (var node in scene.node)
                    {
                        ProcessNode(node);
                    }
                }
            }
        }

        private List<ModelPrim> Parse()
        {
            var Prims = new List<ModelPrim>();

            float DEG_TO_RAD = 0.017453292519943295769236907684886f;

            if (Model == null) return Prims;

            Matrix4 transform = Matrix4.Identity;

            UpAxisType upAxis = UpAxisType.Y_UP;
            _unitMeter = 1f;

            var asset = Model.asset;
            if (asset != null)
            {
                upAxis = asset.up_axis;
                if (asset.unit != null)
                {
                    float meter = (float)asset.unit.meter;
                    _unitMeter = meter;
                    transform = new Matrix4(
                        meter, transform.M12, transform.M13, transform.M14,
                        transform.M21, meter, transform.M23, transform.M24,
                        transform.M31, transform.M32, meter, transform.M34,
                        transform.M41, transform.M42, transform.M43, transform.M44);
                }
            }

            Matrix4 rotation = Matrix4.Identity;

            if (upAxis == UpAxisType.X_UP)
            {
                rotation = Matrix4.CreateFromEulers(0.0f, 90.0f * DEG_TO_RAD, 0.0f);
            }
            else if (upAxis == UpAxisType.Y_UP)
            {
                // Y goes to +Z. (This used to be CreateFromEulers(+90 degrees), which sends Y to -Z and
                // put every Y-up model upside down.)
                rotation = ModelAxes.YUpToZUp;
            }

            rotation *= transform;
            transform = rotation;
            _upAxis = upAxis;

            ParseVisualScene();
            ParseMaterials();
            var rigs = ResolveRigs();

            foreach (var item in Model.Items) {
                if (item is library_geometries geometries) {
                    foreach (var geo in geometries.geometry) {
                        var mesh = geo.Item as mesh;
                        if (mesh == null) 
                            continue;

                        var nodes = Nodes.FindAll(n => n.MeshID == geo.id);     // Find all instances of this geometry
                        ModelPrim? firstPrim = null;         // The first prim is actually calculated, the others are just copied from it.

                        Vector3 asset_scale = new Vector3(1,1,1);
                        Vector3 asset_offset = new Vector3(0, 0, 0);            // Scale and offset between Collada and OS asset (Which is always in a unit cube)

                        foreach (var node in nodes) {
                            var prim = new ModelPrim
                            {
                                ID = node.ID
                            };
                            Prims.Add(prim);

                            // First node is used to create the asset. This is as the code to crate the byte array is somewhat
                            // erroneously placed in the ModelPrim class.
                            if (firstPrim == null) {
                                firstPrim = prim;
                                AddPositions(out asset_scale, out asset_offset, mesh, prim, transform);     // transform is used only for inch -> meter and up axis transform. 

                                foreach (var mitem in mesh.Items) {
                                    if (mitem is triangles triangles)
                                        AddFacesFromPolyList(Triangles2Polylist(triangles), mesh, prim, transform, asset_scale);  // Transform is used to turn normals according to up axis
                                    if (mitem is polylist polylist)
                                        AddFacesFromPolyList(polylist, mesh, prim, transform, asset_scale);
                                }

                                prim.CreateAsset(UUID.Zero);
                            }
                            else {
                                // Copy the values set by Addpositions and AddFacesFromPolyList as these are the same as long as the mesh is the same
                                prim.Asset = firstPrim!.Asset;
                                prim.BoundMin = firstPrim!.BoundMin;
                                prim.BoundMax = firstPrim!.BoundMax;
                                prim.Positions = firstPrim!.Positions;
                                prim.Faces = firstPrim!.Faces;
                            }

                            // Note: This ignores any shear or similar non-linear effects. This can cause some problems but it
                            // is unlikely that authoring software can generate such matrices.
                            node.Transform.Decompose(out prim.Scale, out prim.Rotation, out prim.Position);
                            float roll, pitch, yaw;
                            node.Transform.GetEulerAngles(out roll, out pitch, out yaw);

                            // The offset created when normalizing the mesh vertices into the OS unit cube must be rotated
                            // before being added to the position part of the Collada transform. 
                            Matrix4 rot = Matrix4.CreateFromQuaternion(prim.Rotation);              // Convert rotation to matrix for for Transform
                            Vector3 offset = Vector3.Transform(asset_offset * prim.Scale, rot);     // The offset must be rotated and mutiplied by the Collada file's scale as the offset is added during rendering with the unit cube mesh already multiplied by the compound scale.
                            prim.Position += offset;
                            prim.Scale *= asset_scale;                                              // Modify scale from Collada instance by the rescaling done in AddPositions()
                        }
                    }
                }
            }

            // Rigged meshes: one prim for each node that instances a skin
            foreach (var node in Nodes)
            {
                if (node.ControllerID.Length == 0 || !rigs.TryGetValue(node.ControllerID, out var rig)) continue;

                Prims.Add(new ModelPrim
                {
                    ID = node.ID,
                    Asset = rig.Prim.Asset,
                    BoundMin = rig.Prim.BoundMin,
                    BoundMax = rig.Prim.BoundMax,
                    Positions = rig.Prim.Positions,
                    Faces = rig.Prim.Faces,
                    Skin = rig.Prim.Skin,
                    Position = rig.Offset,
                    Scale = rig.Scale
                });
            }

            return Prims;
        }

        #region Rigs

        /// <summary>What a rigged mesh's faces need to know about its skin while they are made</summary>
        private sealed class RigWeights
        {
            /// <summary>The weights of each position of the mesh</summary>
            public VertexWeight[] Weights = Array.Empty<VertexWeight>();

            /// <summary>The scale that fits the mesh into the unit cube (1 along a flat axis)</summary>
            public Vector3 Fit = Vector3.One;

            /// <summary>
            /// The bind shape matrix undoes the fit, and a normal is drawn through the inverse transpose of that,
            /// so it is stored scaled the other way
            /// </summary>
            public Vector3 FitNormal(Vector3 normal)
            {
                var scaled = normal * Fit;
                float length = scaled.Length();
                return length > 0f && !float.IsNaN(length) && !float.IsInfinity(length) ? scaled / length : Vector3.Zero;
            }
        }

        private sealed class RigTemplate
        {
            public ModelPrim Prim = null!;
            public Vector3 Scale;
            public Vector3 Offset;
        }

        /// <summary>
        /// Builds a rigged prim for each skin that a node instances. A skin that cannot be used as one (it is
        /// not a skin, or none of its joints are the avatar's) has its nodes loaded as ordinary meshes instead,
        /// or dropped if there is no mesh to show.
        /// </summary>
        private Dictionary<string, RigTemplate> ResolveRigs()
        {
            var result = new Dictionary<string, RigTemplate>(StringComparer.Ordinal);
            if (Model == null) return result;

            var controllers = new Dictionary<string, controller>(StringComparer.Ordinal);
            foreach (var item in Model.Items)
            {
                if (item is library_controllers library && library.controller != null)
                {
                    foreach (var c in library.controller)
                    {
                        if (!string.IsNullOrEmpty(c.id)) controllers[c.id] = c;
                    }
                }
            }

            var used = new List<string>();
            foreach (var node in Nodes)
            {
                if (node.ControllerID.Length > 0 && !used.Contains(node.ControllerID)) used.Add(node.ControllerID);
            }

            foreach (var id in used)
            {
                string? geometryId = null;
                RigTemplate? rig = null;
                if (!controllers.TryGetValue(id, out var controller))
                    _diagnostics.Warn(ModelDiagnosticCode.NodeSkipped, $"Collada controller {id} is not defined, so the nodes that use it are skipped", id);
                else
                    rig = BuildRig(controller, out geometryId);

                if (rig != null)
                {
                    result[id] = rig;
                    continue;
                }

                // Not a rig after all: a mesh that stands where its node puts it, if there is a mesh
                foreach (var node in Nodes.FindAll(n => n.ControllerID == id))
                {
                    node.ControllerID = string.Empty;
                    if (geometryId != null) node.MeshID = geometryId;
                }
            }
            return result;
        }

        private RigTemplate? BuildRig(controller controller, out string? geometryId)
        {
            geometryId = null;
            var skin = controller.Item as skin;
            if (skin == null)
            {
                _diagnostics.Warn(ModelDiagnosticCode.RigDropped, $"Collada controller {controller.id} is not a skin (morph targets are not supported), so its mesh is loaded without one", controller.id);
                return null;
            }

            string? wanted = !string.IsNullOrEmpty(skin.source1) && skin.source1.Length > 1 ? skin.source1.Substring(1) : null;
            geometryId = wanted;
            mesh? geometry = null;
            if (wanted != null)
            {
                foreach (var item in Model!.Items)
                {
                    if (item is library_geometries geometries && geometries.geometry != null)
                    {
                        var found = geometries.geometry.FirstOrDefault(g => g.id == wanted);
                        if (found != null) geometry = found.Item as mesh;
                    }
                }
            }
            if (geometry == null)
            {
                _diagnostics.Warn(ModelDiagnosticCode.NodeSkipped, $"Collada skin {controller.id} has no mesh to skin, so it is skipped", controller.id);
                geometryId = null;
                return null;
            }

            if (_upAxis != UpAxisType.Z_UP || Math.Abs(_unitMeter - 1f) > 1e-6f)
            {
                _diagnostics.Warn(ModelDiagnosticCode.RigAxesNotApplied, "This Collada file is not Z-up in meters. A rigged mesh is taken exactly as it is in the file, " +
                            "as the viewer does, so it will only fit the avatar if it was made for Second Life", controller.id);
            }

            // The joints and their inverse bind matrices
            if (skin.joints?.input == null || skin.vertex_weights == null)
                throw new InvalidDataException("Collada skin has no joints or no vertex weights");

            string[]? names = null;
            double[]? matrixValues = null;
            foreach (var input in skin.joints.input)
            {
                var source = FindSkinSource(skin, input.source);
                if (input.semantic == "JOINT") names = ReadNames(source);
                else if (input.semantic == "INV_BIND_MATRIX") matrixValues = (source?.Item as float_array)?.Values;
            }
            if (names == null || names.Length == 0)
                throw new InvalidDataException("Collada skin names no joints");
            if (matrixValues == null || matrixValues.Length < names.Length * 16)
                throw new InvalidDataException("Collada skin has fewer inverse bind matrices than joints");

            var avatar = AvatarSkeletonIndex.Default;
            if (avatar == null)
                _diagnostics.Warn(ModelDiagnosticCode.SkeletonUnavailable, "The avatar skeleton is not available, so joint names are not checked");
            var selection = RigImport.SelectJoints(names, avatar);
            if (selection.Dropped.Count > 0)
                _diagnostics.Warn(ModelDiagnosticCode.JointsIgnored, $"Ignoring Collada joints that are not on the avatar skeleton: {string.Join(", ", selection.Dropped)}",
                    string.Join(", ", selection.Dropped), selection.Dropped.Count);
            if (selection.Names.Count == 0)
            {
                _diagnostics.Warn(ModelDiagnosticCode.RigDropped, "None of the joints of the Collada skin are on the avatar skeleton, so the mesh is loaded without a rig", controller.id);
                return null;
            }
            if (selection.Names.Count > MaxJoints)
                throw new LimitExceededException($"Collada skin has {selection.Names.Count} joints, and a mesh may use at most {MaxJoints}");

            var bindShape = ReadBindShape(skin.bind_shape_matrix);

            // Collada's matrices are column vector, written a row at a time: reading the same floats a column at
            // a time gives the row vector matrix
            var authored = new List<NumMatrix>();
            foreach (var j in selection.FirstOf)
            {
                int o = j * 16;
                var m = new NumMatrix(
                    (float)matrixValues[o + 0], (float)matrixValues[o + 4], (float)matrixValues[o + 8], (float)matrixValues[o + 12],
                    (float)matrixValues[o + 1], (float)matrixValues[o + 5], (float)matrixValues[o + 9], (float)matrixValues[o + 13],
                    (float)matrixValues[o + 2], (float)matrixValues[o + 6], (float)matrixValues[o + 10], (float)matrixValues[o + 14],
                    (float)matrixValues[o + 3], (float)matrixValues[o + 7], (float)matrixValues[o + 11], (float)matrixValues[o + 15]);
                authored.Add(m);
            }
            var matrices = authored;
            if (UseAvatarJointAxes && !TryAlignToAvatar(selection.Names, authored, out matrices))
                matrices = authored;

            // The weights, one list for each position of the mesh
            var weights = ReadWeights(skin, names.Length, selection, out int positionCount);

            var prim = new ModelPrim();
            AddPositions(out var assetScale, out var assetOffset, geometry, prim, Matrix4.Identity);
            if (prim.Positions.Count != positionCount)
                throw new InvalidDataException($"Collada skin weights {positionCount} vertices, but the mesh has {prim.Positions.Count}");

            var rig = new RigWeights
            {
                Weights = weights,
                Fit = new Vector3(assetScale.X == 0 ? 1f : assetScale.X, assetScale.Y == 0 ? 1f : assetScale.Y,
                    assetScale.Z == 0 ? 1f : assetScale.Z)
            };

            foreach (var item in geometry.Items)
            {
                if (item is triangles triangles)
                    AddFacesFromPolyList(Triangles2Polylist(triangles), geometry, prim, Matrix4.Identity, assetScale, rig);
                if (item is polylist polylist)
                    AddFacesFromPolyList(polylist, geometry, prim, Matrix4.Identity, assetScale, rig);
            }

            // The fit comes first, then the file's own bind shape
            var fit = NumMatrix.CreateScale(rig.Fit.X, rig.Fit.Y, rig.Fit.Z) *
                      NumMatrix.CreateTranslation(assetOffset.X, assetOffset.Y, assetOffset.Z);
            var bind = fit * bindShape;
            prim.Skin = new ModelSkin
            {
                BindShapeMatrix = RigImport.Flatten(new[] { bind }, "Collada bind shape matrix")
            };
            prim.Skin.JointNames.AddRange(selection.Names);
            prim.Skin.InverseBindMatrices = RigImport.Flatten(matrices, "Collada skin");
            prim.CreateAsset(UUID.Zero);

            return new RigTemplate { Prim = prim, Scale = assetScale, Offset = assetOffset };
        }

        /// <summary>
        /// Gives each joint the avatar's axes and size at the position its inverse bind matrix puts it. The
        /// inverse of an inverse bind matrix is where the joint sits in the space the mesh is bound in.
        /// </summary>
        private bool TryAlignToAvatar(List<string> names, List<NumMatrix> authored, out List<NumMatrix> aligned)
        {
            aligned = new List<NumMatrix>(authored.Count);
            for (int i = 0; i < authored.Count; i++)
            {
                if (!NumMatrix.Invert(authored[i], out var bindWorld))
                {
                    _diagnostics.Warn(ModelDiagnosticCode.RigApproximated, $"The inverse bind matrix of joint {names[i]} cannot be inverted, so the matrices are used as authored", names[i]);
                    return false;
                }

                var matrix = RigImport.OnAvatarRotation(names[i], bindWorld, out bool ok);
                if (!ok)
                    return false;
                aligned.Add(matrix);
            }
            return true;
        }

        private static source? FindSkinSource(skin skin, string? reference)
        {
            if (string.IsNullOrEmpty(reference) || reference!.Length < 2 || skin.source == null) return null;
            string id = reference.Substring(1);
            return skin.source.FirstOrDefault(src => src.id == id);
        }

        private static string[]? ReadNames(source? source)
        {
            if (source?.Item is Name_array names)
                return names.Values;
            if (source?.Item is IDREF_array ids && ids.Value != null)
                return ids.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return null;
        }

        private static NumMatrix ReadBindShape(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return NumMatrix.Identity;

            var values = ParseFloats(text!, "bind shape matrix");
            if (values.Length != 16)
                throw new InvalidDataException($"Collada bind shape matrix has {values.Length} values, not 16");

            return new NumMatrix(
                values[0], values[4], values[8], values[12],
                values[1], values[5], values[9], values[13],
                values[2], values[6], values[10], values[14],
                values[3], values[7], values[11], values[15]);
        }

        private static float[] ParseFloats(string text, string what)
        {
            var tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var result = new float[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!float.TryParse(tokens[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result[i]) ||
                    !Utils.IsFinite(result[i]))
                    throw new InvalidDataException($"Collada {what} has a value that is not a number: {tokens[i]}");
            }
            return result;
        }

        private static int[] ParseIntegers(string? text, string what)
        {
            if (text == null) throw new InvalidDataException($"Collada skin has no {what}");
            var tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var result = new int[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!int.TryParse(tokens[i], System.Globalization.NumberStyles.AllowLeadingSign,
                        System.Globalization.CultureInfo.InvariantCulture, out result[i]))
                    throw new InvalidDataException($"Collada {what} has a value that is not an integer: {tokens[i]}");
            }
            return result;
        }

        /// <summary>
        /// Reads <c>vertex_weights</c>: for each position of the mesh, which joints pull on it and how hard. A joint
        /// index of -1 is the bind shape matrix, which does not count. Only the four strongest influences are kept,
        /// and they are made to add up to one.
        /// </summary>
        private VertexWeight[] ReadWeights(skin skin, int jointCount, JointSelection selection, out int vertexCount)
        {
            var vw = skin.vertex_weights;
            int jointOffset = -1, weightOffset = -1, stride = 0;
            double[]? weightValues = null;
            foreach (var input in vw.input)
            {
                stride = Math.Max(stride, (int)Math.Min(input.offset, 1024UL) + 1);
                if (input.semantic == "JOINT") jointOffset = (int)Math.Min(input.offset, 1024UL);
                else if (input.semantic == "WEIGHT")
                {
                    weightOffset = (int)Math.Min(input.offset, 1024UL);
                    weightValues = (FindSkinSource(skin, input.source)?.Item as float_array)?.Values;
                }
            }
            if (jointOffset < 0 || weightOffset < 0 || weightValues == null)
                throw new InvalidDataException("Collada skin has no JOINT and WEIGHT inputs on its vertex weights");

            var counts = ParseIntegers(vw.vcount, "vertex weight counts");
            var pairs = ParseIntegers(vw.v, "vertex weights");

            long total = 0;
            foreach (var c in counts)
            {
                if (c < 0) throw new InvalidDataException("Collada vertex weight count is negative");
                total += c;
            }
            if (total * stride != pairs.Length)
                throw new InvalidDataException("Collada vertex weights do not add up to the counts of them");

            vertexCount = counts.Length;
            var result = new VertexWeight[counts.Length];
            var influences = new List<KeyValuePair<int, float>>();
            int unweighted = 0;
            long at = 0;
            for (int vertex = 0; vertex < counts.Length; vertex++)
            {
                influences.Clear();
                for (int k = 0; k < counts[vertex]; k++, at += stride)
                {
                    int joint = pairs[at + jointOffset];
                    int weightIndex = pairs[at + weightOffset];
                    if (joint == -1) continue;
                    if (joint < 0 || joint >= jointCount)
                        throw new InvalidDataException("Collada vertex weights refer to a joint the skin does not have");
                    if (weightIndex < 0 || weightIndex >= weightValues.Length)
                        throw new InvalidDataException("Collada vertex weights refer to a weight the skin does not have");

                    double weight = weightValues[weightIndex];
                    if (double.IsNaN(weight) || double.IsInfinity(weight) || weight < 0)
                        throw new InvalidDataException("Collada skin has a weight that is not a weight");

                    int mapped = selection.Map[joint];
                    if (mapped < 0) continue;
                    influences.Add(new KeyValuePair<int, float>(mapped, (float)weight));
                }

                // A vertex with nothing left to follow follows the first joint
                if (!RigImport.Reduce(influences, out result[vertex])) unweighted++;
            }

            if (unweighted > 0)
                _diagnostics.Warn(ModelDiagnosticCode.UnweightedVertices, $"{unweighted} vertices have no weight to a joint of the avatar and follow the first joint instead", null, unweighted);
            return result;
        }

        #endregion Rigs

        private source? FindSource(IEnumerable<source> sources, string id)
        {
            id = id.Substring(1);

            return sources.FirstOrDefault(src => src.id == id);
        }

        private void AddPositions(out Vector3 scale, out Vector3 offset, mesh mesh, ModelPrim prim, Matrix4 transform)
        {
            prim.Positions = new List<Vector3>();
            source? posSrc = FindSource(mesh.source, mesh.vertices.input[0].source);
            if (posSrc == null) throw new InvalidDataException("Missing position source in Collada mesh");
            double[] posVals = ((float_array)posSrc.Item).Values;

            for (int i = 0; i < posVals.Length / 3; i++)
            {
                Vector3 pos = new Vector3((float)posVals[i * 3], (float)posVals[i * 3 + 1], (float)posVals[i * 3 + 2]);
                if (!pos.IsFinite())
                    throw new InvalidDataException("Collada mesh has a position that is not a number");
                pos = Vector3.Transform(pos, transform);
                prim.Positions.Add(pos);
            }

            prim.BoundMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            prim.BoundMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (var pos in prim.Positions)
            {
                prim.BoundMax = new Vector3(
                    Math.Max(prim.BoundMax.X, pos.X),
                    Math.Max(prim.BoundMax.Y, pos.Y),
                    Math.Max(prim.BoundMax.Z, pos.Z));
                prim.BoundMin = new Vector3(
                    Math.Min(prim.BoundMin.X, pos.X),
                    Math.Min(prim.BoundMin.Y, pos.Y),
                    Math.Min(prim.BoundMin.Z, pos.Z));
            }

            scale = prim.BoundMax - prim.BoundMin;
            offset = prim.BoundMin + (scale / 2);

            // Fit vertex positions into identity cube -0.5 .. 0.5
            for (int i = 0; i < prim.Positions.Count; i++)
            {
                Vector3 pos = prim.Positions[i];
                pos = new Vector3(
                    scale.X == 0 ? 0 : ((pos.X - prim.BoundMin.X) / scale.X) - 0.5f,
                    scale.Y == 0 ? 0 : ((pos.Y - prim.BoundMin.Y) / scale.Y) - 0.5f,
                    scale.Z == 0 ? 0 : ((pos.Z - prim.BoundMin.Z) / scale.Z) - 0.5f
                    );
                prim.Positions[i] = pos;
            }
        }

        private int[] StrToArray(string s)
        {
            string[] vals = Regex.Split(s.Trim(), @"\s+");
            int[] ret = new int[vals.Length];

            for (int i = 0; i < ret.Length; i++)
            {
                int.TryParse(vals[i], out ret[i]);
            }

            return ret;
        }

        private void AddFacesFromPolyList(polylist list, mesh mesh, ModelPrim prim, Matrix4 transform, Vector3 assetScale, RigWeights? rig = null)
        {
            source? posSrc = null;
            source? normalSrc = null;
            source? uvSrc = null;

            ulong stride = 0;
            int posOffset = -1;
            int norOffset = -1;
            int uvOffset = -1;

            foreach (var inp in list.input)
            {
                stride = Math.Max(stride, inp.offset);

                switch (inp.semantic)
                {
                    case "VERTEX":
                        posSrc = FindSource(mesh.source, mesh.vertices.input[0].source);
                        posOffset = (int)inp.offset;
                        break;
                    case "NORMAL":
                        normalSrc = FindSource(mesh.source, inp.source);
                        norOffset = (int)inp.offset;
                        break;
                    case "TEXCOORD":
                        uvSrc = FindSource(mesh.source, inp.source);
                        uvOffset = (int)inp.offset;
                        break;
                }
            }

            stride += 1;

            if (posSrc == null) return;

            var vcount = StrToArray(list.vcount);
            var idx = StrToArray(list.p);

            Vector3[]? normals = null;
            if (normalSrc != null)
            {
                var norVal = ((float_array)normalSrc.Item).Values;
                normals = new Vector3[norVal.Length / 3];

                for (int i = 0; i < normals.Length; i++)
                {
                    normals[i] = new Vector3((float)norVal[i * 3 + 0], (float)norVal[i * 3 + 1], (float)norVal[i * 3 + 2]);
                    normals[i] = Vector3.Normalize(Vector3.TransformNormal(normals[i], transform));
                }
            }

            Vector2[]? uvs = null;
            if (uvSrc != null)
            {
                var uvVal = ((float_array)uvSrc.Item).Values;
                uvs = new Vector2[uvVal.Length / 2];

                for (int i = 0; i < uvs.Length; i++)
                {
                    uvs[i] = new Vector2((float)uvVal[i * 2 + 0], (float)uvVal[i * 2 + 1]);
                }

            }

            ModelFace face = new ModelFace {MaterialID = list.material};
            if (face.MaterialID != null)
            {
                if (MatSymTarget.TryGetValue(list.material, out var value))
                {
                    ModelMaterial? mat = Materials.Find(m => m.ID == value);
                    if (mat != null)
                    {
                        face.Material = mat;
                    }
                }
            }

            int curIdx = 0;

            foreach (var nvert in vcount)
            {
                if (nvert < 3 || nvert > 4)
                {
                    throw new InvalidDataException("Only triangles and quads supported");
                }

                Vertex[] verts = new Vertex[nvert];
                var positionIndices = new int[nvert];
                for (int j = 0; j < nvert; j++)
                {
                    positionIndices[j] = idx[curIdx + posOffset + (int)stride * j];
                    verts[j].Position = prim.Positions[positionIndices[j]];

                    if (normals != null)
                    {
                        // The mesh gets fitted into the unit cube, so its normals are stored to match. A rigged
                        // mesh's bind shape is exact about the scale, so it gets the matching one.
                        var normal = normals[idx[curIdx + norOffset + (int)stride * j]];
                        verts[j].Normal = rig != null ? rig.FitNormal(normal) : UnitCubeFit.FitNormal(normal, assetScale);
                    }

                    if (uvs != null)
                    {
                        verts[j].TexCoord = uvs[idx[curIdx + uvOffset + (int)stride * j]];
                    }
                }


                // The corners of the triangles this polygon makes
                int[] corners = nvert == 3 ? new[] { 0, 1, 2 } : new[] { 0, 1, 2, 0, 2, 3 };
                foreach (var corner in corners)
                {
                    // The weights belong to the position, not to the corner
                    if (rig != null)
                        face.AddVertex(verts[corner], rig.Weights[positionIndices[corner]]);
                    else
                        face.AddVertex(verts[corner]);
                }

                curIdx += (int)stride * nvert;
            }

            prim.Faces.Add(face);


        }

        private polylist Triangles2Polylist(triangles triangles)
        {
            polylist poly = new polylist
            {
                count = triangles.count,
                input = triangles.input,
                material = triangles.material,
                name = triangles.name,
                p = triangles.p
            };

            const string str = "3 ";
            System.Text.StringBuilder builder = new System.Text.StringBuilder(str.Length * (int)poly.count);
            for (int i = 0; i < (int)poly.count; i++) builder.Append(str);
            poly.vcount = builder.ToString();

            return poly;
        }

    }
}

