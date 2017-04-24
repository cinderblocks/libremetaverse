using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Xml;

using OpenTK.Graphics.OpenGL;

using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenMetaverse.Assets;

namespace AvatarPreview
{
    public partial class frmAvatar : Form
    {
        GridClient _client = new GridClient();
        Dictionary<string, GLMesh> _meshes = new Dictionary<string, GLMesh>();
        bool _wireframe = true;
        bool _showSkirt = false;

        public frmAvatar()
        {
            InitializeComponent();

            GL.ShadeModel(ShadingModel.Smooth);
            GL.ClearColor(Color.Black);
            GL.ClearDepth(1.0f);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(true);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.Hint(HintTarget.PerspectiveCorrectionHint, HintMode.Nicest);

            glControl_Resize(null, null);
        }

        private void lindenLabMeshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog {Filter = @"avatar_lad.xml|avatar_lad.xml"};

            if (dialog.ShowDialog() != DialogResult.OK) return;

            _meshes.Clear();

            try
            {
                // Parse through avatar_lad.xml to find all of the mesh references
                XmlDocument lad = new XmlDocument();
                lad.Load(dialog.FileName);

                XmlNodeList meshes = lad.GetElementsByTagName("mesh");

                foreach (XmlNode meshNode in meshes)
                {
                    string type = meshNode.Attributes.GetNamedItem("type").Value;
                    int lod = Int32.Parse(meshNode.Attributes.GetNamedItem("lod").Value);
                    string fileName = meshNode.Attributes.GetNamedItem("file_name").Value;
                    //string minPixelWidth = meshNode.Attributes.GetNamedItem("min_pixel_width").Value;

                    // Mash up the filename with the current path
                    fileName = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(dialog.FileName), fileName);

                    GLMesh mesh = (_meshes.ContainsKey(type) ? _meshes[type] : new GLMesh(type));

                    if (lod == 0)
                    {
                        mesh.LoadMesh(fileName);
                    }
                    else
                    {
                        mesh.LoadLODMesh(lod, fileName);
                    }

                    _meshes[type] = mesh;
                    glControl_Resize(null, null);
                    glControl.Invalidate();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(@"Failed to load avatar mesh: " + ex.Message);
            }
        }

        private void textureToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void wireframeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            wireframeToolStripMenuItem.Checked = !wireframeToolStripMenuItem.Checked;
            _wireframe = wireframeToolStripMenuItem.Checked;

            glControl.Invalidate();
        }

        private void skirtToolStripMenuItem_Click(object sender, EventArgs e)
        {
            skirtToolStripMenuItem.Checked = !skirtToolStripMenuItem.Checked;
            _showSkirt = skirtToolStripMenuItem.Checked;

            glControl.Invalidate();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                @"Written by John Hurliman <jhurliman@jhurliman.org> (http://www.jhurliman.org/)");
        }

        private void glControl_Paint(object sender, PaintEventArgs e)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.LoadIdentity();
            if (_wireframe)
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            else
                GL.PolygonMode(MaterialFace.Front, PolygonMode.Fill);
            // Push the world matrix
            GL.PushMatrix();
            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.TextureCoordArray);
            // World rotations
            GL.Rotate(scrollRoll.Value, 1f, 0f, 0f);
            GL.Rotate(scrollRoll.Value, 0f, 1f, 0f);
            GL.Rotate(scrollRoll.Value, 0f, 0f, 1f);

            if (_meshes.Count > 0)
            {
                foreach (GLMesh mesh in _meshes.Values)
                {
                    if (!_showSkirt && mesh.Name == "skirtMesh")
                        continue;
                    
                    GL.Color3(1f, 1f, 1f);

                    // Individual prim matrix
                    GL.PushMatrix();

                    //Gl.glTranslatef(mesh.Position.X, mesh.Position.Y, mesh.Position.Z);

                    GL.Rotate(mesh.RotationAngles.X, 1f, 0f, 0f);
                    GL.Rotate(mesh.RotationAngles.Y, 0f, 1f, 0f);
                    GL.Rotate(mesh.RotationAngles.Z, 0f, 0f, 1f);

                    GL.Scale(mesh.Scale.X, mesh.Scale.Y, mesh.Scale.Z);

                    // TODO: Texturing

                    GL.TexCoordPointer(2, TexCoordPointerType.Float, 0, mesh.RenderData.TexCoords);
                    GL.VertexPointer(3, VertexPointerType.Float, 0, mesh.RenderData.TexCoords);
                    GL.DrawElements(BeginMode.Triangles, mesh.RenderData.Indices.Length, DrawElementsType.UnsignedShort, mesh.RenderData.Indices);
                }
            }

            // Pop the world matrix
            GL.PopMatrix();
            
            GL.DisableClientState(ArrayCap.TextureCoordArray);
            GL.DisableClientState(ArrayCap.VertexArray);

            GL.Flush();
        }

        private void glControl_Resize(object sender, EventArgs e)
        {
            //GL.ClearColor(0.39f, 0.58f, 0.93f, 1.0f); // Cornflower blue anyone?
            GL.ClearColor(0f, 0f, 0f, 1f);

            GL.PushMatrix();
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
             
            GL.Viewport(0, 0, glControl.Width, glControl.Height);

            OpenTK.Matrix4 perspectiveMatrix = OpenTK.Matrix4.CreatePerspectiveFieldOfView(50.0f, 1.0f, 0.001f, 50f);
            GL.LoadMatrix(ref perspectiveMatrix);

            Vector3 center = Vector3.Zero;
            GLMesh head, lowerBody;
            if (_meshes.TryGetValue("headMesh", out head) && _meshes.TryGetValue("lowerBodyMesh", out lowerBody))
                center = (head.RenderData.Center + lowerBody.RenderData.Center) / 2f;

            OpenTK.Matrix4 lookAt = OpenTK.Matrix4.LookAt(
                new OpenTK.Vector3(center.X, scrollZoom.Value * 0.1f + center.Y, center.Z),
                new OpenTK.Vector3(center.X, scrollZoom.Value * 0.1f + center.Y + 1f, center.Z),
                new OpenTK.Vector3(0f, 0f, 1f));
            GL.LoadMatrix(ref lookAt);
            GL.MatrixMode(MatrixMode.Modelview);
        }

        private void scroll_ValueChanged(object sender, EventArgs e)
        {
            glControl_Resize(null, null);
            glControl.Invalidate();
        }

        private void pic_MouseClick(object sender, MouseEventArgs e)
        {
            PictureBox control = (PictureBox)sender;

            OpenFileDialog dialog = new OpenFileDialog();
            // TODO: Setup a dialog.Filter for supported image types

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    Image image = Image.FromFile(dialog.FileName);

                    #region Dimensions Check

                    if (control == picEyesBake)
                    {
                        // Eyes texture is 128x128
                        if (Width != 128 || Height != 128)
                        {
                            Bitmap resized = new Bitmap(128, 128, image.PixelFormat);
                            Graphics graphics = Graphics.FromImage(resized);

                            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            graphics.DrawImage(image, 0, 0, 128, 128);

                            image.Dispose();
                            image = resized;
                        }
                    }
                    else
                    {
                        // Other textures are 512x512
                        if (Width != 128 || Height != 128)
                        {
                            Bitmap resized = new Bitmap(512, 512, image.PixelFormat);
                            Graphics graphics = Graphics.FromImage(resized);

                            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            graphics.DrawImage(image, 0, 0, 512, 512);

                            image.Dispose();
                            image = resized;
                        }
                    }

                    #endregion Dimensions Check

                    // Set the control image
                    control.Image = image;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(@"Failed to load image: " + ex.Message);
                }
            }
            else
            {
                control.Image = null;
            }

            #region Baking

            var paramValues = GetParamValues();
            var layers =
                    new Dictionary<AvatarTextureIndex, AssetTexture>();
            int textureCount = 0;

            if ((string)control.Tag == "Head")
            {
                if (picHair.Image != null)
                {
                    layers.Add(AvatarTextureIndex.Hair,
                        new AssetTexture(new ManagedImage((Bitmap)picHair.Image)));
                    ++textureCount;
                }
                if (picHeadBodypaint.Image != null)
                {
                    layers.Add(AvatarTextureIndex.HeadBodypaint,
                        new AssetTexture(new ManagedImage((Bitmap)picHeadBodypaint.Image)));
                    ++textureCount;
                }

                // Compute the head bake
                Baker baker = new Baker(BakeType.Head);

                foreach (var kvp in layers)
                {
                    AppearanceManager.TextureData tdata = new AppearanceManager.TextureData {Texture = kvp.Value};
                    baker.AddTexture(tdata);
                }

                baker.Bake();

                if (baker.BakedTexture != null)
                {
                    AssetTexture bakeAsset = baker.BakedTexture;
                    // Baked textures use the alpha layer for other purposes, so we need to not use it
                    bakeAsset.Image.Channels = ManagedImage.ImageChannels.Color;
                    picHeadBake.Image = LoadTGAClass.LoadTGA(new MemoryStream(bakeAsset.Image.ExportTGA()));
                }
                else
                {
                    MessageBox.Show(@"Failed to create the bake layer, unknown error");
                }
            }
            else if ((string)control.Tag == "Upper")
            {
                if (picUpperBodypaint.Image != null)
                {
                    layers.Add(AvatarTextureIndex.UpperBodypaint,
                        new AssetTexture(new ManagedImage((Bitmap)picUpperBodypaint.Image)));
                    ++textureCount;
                }
                if (picUpperGloves.Image != null)
                {
                    layers.Add(AvatarTextureIndex.UpperGloves,
                        new AssetTexture(new ManagedImage((Bitmap)picUpperGloves.Image)));
                    ++textureCount;
                }
                if (picUpperUndershirt.Image != null)
                {
                    layers.Add(AvatarTextureIndex.UpperUndershirt,
                        new AssetTexture(new ManagedImage((Bitmap)picUpperUndershirt.Image)));
                    ++textureCount;
                }
                if (picUpperShirt.Image != null)
                {
                    layers.Add(AvatarTextureIndex.UpperShirt,
                        new AssetTexture(new ManagedImage((Bitmap)picUpperShirt.Image)));
                    ++textureCount;
                }
                if (picUpperJacket.Image != null)
                {
                    layers.Add(AvatarTextureIndex.UpperJacket,
                        new AssetTexture(new ManagedImage((Bitmap)picUpperJacket.Image)));
                    ++textureCount;
                }

                // Compute the upper body bake
                Baker baker = new Baker(BakeType.UpperBody);

                foreach (KeyValuePair<AvatarTextureIndex, AssetTexture> kvp in layers)
                {
                    AppearanceManager.TextureData tdata = new AppearanceManager.TextureData();
                    tdata.Texture = kvp.Value;
                    baker.AddTexture(tdata);
                }

                baker.Bake();

                if (baker.BakedTexture != null)
                {
                    AssetTexture bakeAsset = baker.BakedTexture;
                    // Baked textures use the alpha layer for other purposes, so we need to not use it
                    bakeAsset.Image.Channels = ManagedImage.ImageChannels.Color;
                    picUpperBodyBake.Image = LoadTGAClass.LoadTGA(new MemoryStream(bakeAsset.Image.ExportTGA()));
                }
                else
                {
                    MessageBox.Show(@"Failed to create the bake layer, unknown error");
                }
            }
            else if ((string)control.Tag == "Lower")
            {
                if (picLowerBodypaint.Image != null)
                {
                    layers.Add(AvatarTextureIndex.LowerBodypaint,
                        new AssetTexture(new ManagedImage((Bitmap)picLowerBodypaint.Image)));
                    ++textureCount;
                }
                if (picLowerUnderpants.Image != null)
                {
                    layers.Add(AvatarTextureIndex.LowerUnderpants,
                        new AssetTexture(new ManagedImage((Bitmap)picLowerUnderpants.Image)));
                    ++textureCount;
                }
                if (picLowerSocks.Image != null)
                {
                    layers.Add(AvatarTextureIndex.LowerSocks,
                        new AssetTexture(new ManagedImage((Bitmap)picLowerSocks.Image)));
                    ++textureCount;
                }
                if (picLowerShoes.Image != null)
                {
                    layers.Add(AvatarTextureIndex.LowerShoes,
                        new AssetTexture(new ManagedImage((Bitmap)picLowerShoes.Image)));
                    ++textureCount;
                }
                if (picLowerPants.Image != null)
                {
                    layers.Add(AvatarTextureIndex.LowerPants,
                        new AssetTexture(new ManagedImage((Bitmap)picLowerPants.Image)));
                    ++textureCount;
                }

                // Compute the lower body bake
                Baker baker = new Baker(BakeType.LowerBody);

                foreach (KeyValuePair<AvatarTextureIndex, AssetTexture> kvp in layers)
                {
                    AppearanceManager.TextureData tdata = new AppearanceManager.TextureData();
                    tdata.Texture = kvp.Value;
                    baker.AddTexture(tdata);
                }

                baker.Bake();

                if (baker.BakedTexture != null)
                {
                    AssetTexture bakeAsset = baker.BakedTexture;
                    // Baked textures use the alpha layer for other purposes, so we need to not use it
                    bakeAsset.Image.Channels = ManagedImage.ImageChannels.Color;
                    picLowerBodyBake.Image = LoadTGAClass.LoadTGA(new MemoryStream(bakeAsset.Image.ExportTGA()));
                }
                else
                {
                    MessageBox.Show(@"Failed to create the bake layer, unknown error");
                }
            }
            else if ((string)control.Tag == "Bake")
            {
                // Bake image has been set manually, no need to manually calculate a bake
                // FIXME:
            }

            #endregion Baking
        }

        private Dictionary<int, float> GetParamValues()
        {
            var paramValues = new Dictionary<int, float>(VisualParams.Params.Count);

            foreach (var kvp in VisualParams.Params)
            {
                VisualParam vp = kvp.Value;
                paramValues.Add(vp.ParamID, vp.DefaultValue);
            }

            return paramValues;
        }
    }
}
