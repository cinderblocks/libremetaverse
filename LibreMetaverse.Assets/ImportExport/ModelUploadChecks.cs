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
using LibreMetaverse.Rendering;

namespace LibreMetaverse.ImportExport
{
    /// <summary>
    /// What the grid would refuse, or mangle, in the models you are about to upload, found before anything is sent.
    /// <see cref="InventoryManager.UploadModelAsync"/> runs these and does not contact the grid if one is an error;
    /// run them yourself first to tell the user at once.
    /// </summary>
    /// <remarks>
    /// An error is something the reference viewer cannot upload either, or that would be written into the mesh
    /// asset wrongly: too many faces, a face with too many vertices to index with 16 bits, an empty mesh, a
    /// triangle that points at a vertex that is not there, a number that is not a number, a rig that does not hold
    /// together. A warning is something that works but not as well as it might. The checks do not build the mesh
    /// asset, so they change nothing.
    /// Positions are not required to lie in the unit cube, and a prim may have no size along an axis (a flat
    /// mesh), because the loaders produce both.
    /// </remarks>
    public static class ModelUploadChecks
    {
        /// <summary>Checks the prims that would become one uploaded mesh object</summary>
        public static IReadOnlyList<ModelDiagnostic> Check(IReadOnlyList<ModelPrim> prims)
        {
            if (prims == null) throw new ArgumentNullException(nameof(prims));

            var diagnostics = new ModelDiagnosticCollector();
            var unsized = new HashSet<ModelMaterial>();
            for (int i = 0; i < prims.Count; i++)
            {
                CheckPrim(prims[i], string.IsNullOrEmpty(prims[i].ID) ? $"model_{i}" : prims[i].ID, diagnostics, unsized);
            }
            return diagnostics.Diagnostics;
        }

        private static void CheckPrim(ModelPrim prim, string name, ModelDiagnosticCollector diagnostics, HashSet<ModelMaterial> unsized)
        {
            if (!FacetedMesh.IsFinite(prim.Position) || !FacetedMesh.IsFinite(prim.Scale) ||
                !FacetedMesh.IsFinite(prim.Rotation.X) || !FacetedMesh.IsFinite(prim.Rotation.Y) ||
                !FacetedMesh.IsFinite(prim.Rotation.Z) || !FacetedMesh.IsFinite(prim.Rotation.W))
            {
                diagnostics.Error(ModelDiagnosticCode.InvalidModel, $"{name} has a position, rotation or size that is not a number", name);
            }

            if (prim.Faces.Count == 0)
            {
                diagnostics.Error(ModelDiagnosticCode.NoGeometry, $"{name} has no faces", name);
                return;
            }
            if (prim.Faces.Count > ModelPrim.MaxFaces)
            {
                diagnostics.Error(ModelDiagnosticCode.TooManyFaces,
                    $"{name} has {prim.Faces.Count} faces, and Second Life allows {ModelPrim.MaxFaces}", name, prim.Faces.Count);
            }

            // Weights are only worth checking against a skin that makes sense
            var skin = prim.Skin;
            bool skinFits = skin != null && CheckSkin(skin, name, diagnostics);

            int empty = 0, tooBig = 0, badIndices = 0, notNumbers = 0, badWeights = 0;
            foreach (var face in prim.Faces)
            {
                if (face.Vertices.Count == 0 || face.Indices.Count == 0)
                {
                    empty++;
                    continue;
                }
                if (face.Vertices.Count > ModelPrim.MaxVerticesPerFace) tooBig++;
                if (face.Indices.Count % 3 != 0) badIndices++;
                else if (HasIndexPastTheVertices(face)) badIndices++;
                if (HasValueThatIsNotANumber(face)) notNumbers++;
                if (skinFits && !WeightsFit(face, skin!.JointNames.Count)) badWeights++;

                if (face.Material != null && face.Material.TextureData.Length > 0 &&
                    (face.Material.Width <= 0 || face.Material.Height <= 0) && unsized.Add(face.Material))
                {
                    diagnostics.Warn(ModelDiagnosticCode.TextureSizeUnknown,
                        $"The size of texture {face.Material.Texture} is not known, so the upload fee is quoted from the whole texture",
                        face.Material.Texture);
                }
            }

            if (empty > 0)
                diagnostics.Error(ModelDiagnosticCode.NoGeometry, $"{empty} faces of {name} have no triangles", name, empty);
            if (tooBig > 0)
                diagnostics.Error(ModelDiagnosticCode.LimitExceeded,
                    $"{tooBig} faces of {name} have more than {ModelPrim.MaxVerticesPerFace} vertices, which a triangle cannot index", name, tooBig);
            if (badIndices > 0)
                diagnostics.Error(ModelDiagnosticCode.InvalidModel,
                    $"{badIndices} faces of {name} have triangles that do not use the face's vertices", name, badIndices);
            if (notNumbers > 0)
                diagnostics.Error(ModelDiagnosticCode.InvalidModel,
                    $"{notNumbers} faces of {name} have a position, normal or texture coordinate that is not a number", name, notNumbers);
            if (badWeights > 0)
                diagnostics.Error(ModelDiagnosticCode.InvalidModel,
                    $"{badWeights} faces of {name} have skin weights that do not fit the vertices and the skin", name, badWeights);
        }

        /// <returns>False if the skin itself is wrong</returns>
        private static bool CheckSkin(ModelSkin skin, string name, ModelDiagnosticCollector diagnostics)
        {
            int joints = skin.JointNames.Count;
            if (joints == 0)
            {
                diagnostics.Error(ModelDiagnosticCode.InvalidModel, $"The skin of {name} has no joints", name);
                return false;
            }
            if (joints > ModelSkin.MaxJoints)
            {
                diagnostics.Error(ModelDiagnosticCode.LimitExceeded,
                    $"The skin of {name} has {joints} joints, and the viewer uses at most {ModelSkin.MaxJoints}", name, joints);
                return false;
            }
            if (skin.InverseBindMatrices.Length != joints * 16 || skin.BindShapeMatrix.Length != 16)
            {
                diagnostics.Error(ModelDiagnosticCode.InvalidModel,
                    $"The skin of {name} does not have one inverse bind matrix for each joint and a 4x4 bind shape matrix", name);
                return false;
            }
            foreach (var value in skin.InverseBindMatrices)
            {
                if (!FacetedMesh.IsFinite(value))
                {
                    diagnostics.Error(ModelDiagnosticCode.InvalidModel, $"The skin of {name} has an inverse bind matrix with a value that is not a number", name);
                    return false;
                }
            }
            foreach (var value in skin.BindShapeMatrix)
            {
                if (!FacetedMesh.IsFinite(value))
                {
                    diagnostics.Error(ModelDiagnosticCode.InvalidModel, $"The skin of {name} has a bind shape matrix with a value that is not a number", name);
                    return false;
                }
            }
            return true;
        }

        private static bool HasIndexPastTheVertices(ModelFace face)
        {
            foreach (var index in face.Indices)
            {
                if (index >= face.Vertices.Count) return true;
            }
            return false;
        }

        private static bool HasValueThatIsNotANumber(ModelFace face)
        {
            foreach (var v in face.Vertices)
            {
                if (!FacetedMesh.IsFinite(v.Position) || !FacetedMesh.IsFinite(v.Normal) || !FacetedMesh.IsFinite(v.TexCoord))
                    return true;
            }
            return false;
        }

        private static bool WeightsFit(ModelFace face, int jointCount)
        {
            if (face.Weights == null || face.Weights.Count != face.Vertices.Count) return false;

            foreach (var w in face.Weights)
            {
                if (!Fits(w.Joint0, w.Weight0, jointCount) || !Fits(w.Joint1, w.Weight1, jointCount) ||
                    !Fits(w.Joint2, w.Weight2, jointCount) || !Fits(w.Joint3, w.Weight3, jointCount))
                    return false;
            }
            return true;
        }

        // A weight of zero is not written, so the joint it names does not matter
        private static bool Fits(int joint, float weight, int jointCount)
        {
            if (!FacetedMesh.IsFinite(weight)) return false;
            return !(weight > 0f) || (joint >= 0 && joint < jointCount);
        }
    }
}
