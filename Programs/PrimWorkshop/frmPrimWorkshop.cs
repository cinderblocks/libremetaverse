using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using ICSharpCode.SharpZipLib.Zip;
using OpenTK.Graphics.OpenGL;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Imaging;
using OpenMetaverse.Rendering;

// NOTE: Batches are divided by texture, fullbright, shiny, transparent, and glow

namespace PrimWorkshop
{
    public partial class frmPrimWorkshop : Form
    {
        #region Form Globals

        List<FacetedMesh> Prims = null;
        FacetedMesh CurrentPrim = null;
        ProfileFace? CurrentFace = null;

        bool DraggingTexture = false;
        bool Wireframe = true;
        int[] TexturePointers = new int[1];
        Dictionary<UUID, Image> Textures = new Dictionary<UUID, Image>();

        #endregion Form Globals

        public frmPrimWorkshop()
        {
            InitializeComponent();

            GL.ShadeModel(ShadingModel.Smooth);
            GL.ClearColor(Color.Black);

            GL.ClearDepth(1f);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(true);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.Hint(HintTarget.PerspectiveCorrectionHint, HintMode.Nicest);

            TexturePointers[0] = 0;

            // Call the resizing function which sets up the GL drawing window
            // and will also invalidate the GL control
            glControl_Resize(null, null);
        }

        private void frmPrimWorkshop_Shown(object sender, EventArgs e)
        {
            // Get a list of rendering plugins
            List<string> renderers = RenderingLoader.ListRenderers(".");

            foreach (string r in renderers)
            {
                DialogResult result = MessageBox.Show(
                    String.Format("Use renderer {0}?", r), "Select Rendering Plugin", MessageBoxButtons.YesNo);

                if (result == DialogResult.Yes)
                {
                    Render.Plugin = RenderingLoader.LoadRenderer(r);
                    break;
                }
            }

            if (Render.Plugin == null)
            {
                MessageBox.Show("No valid rendering plugin loaded, exiting...");
                Application.Exit();
            }
        }

        #region GLControl Callbacks

        private void glControl_Paint(object sender, PaintEventArgs e)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.LoadIdentity();

            // Setup wireframe or solid fill drawing mode
            if (Wireframe)
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            else
                GL.PolygonMode(MaterialFace.Front, PolygonMode.Fill);

            Vector3 center = Vector3.Zero;

            OpenTK.Matrix4 lookAt = OpenTK.Matrix4.LookAt(
                center.X, scrollZoom.Value * 0.1f + center.Y, center.Z,
                center.X, center.Y, center.Z,
                0f, 0f, 1f);
            GL.LoadMatrix(ref lookAt);

            // Push the world matrix
            GL.PushMatrix();
            
            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.TextureCoordArray);

            // World rotations
            GL.Rotate((float)scrollRoll.Value, 1f, 0f, 0f);
            GL.Rotate((float)scrollPitch.Value, 0f, 1f, 0f);
            GL.Rotate((float)scrollYaw.Value, 0f, 0f, 1f);

            if (Prims != null)
            {
                for (int i = 0; i < Prims.Count; i++)
                {
                    Primitive prim = Prims[i].Prim;

                    if (i == cboPrim.SelectedIndex)
                        GL.Color3(1f, 0f, 0f);
                    else
                        GL.Color3(1f, 1f, 1f);

                    // Individual prim matrix
                    GL.PushMatrix();

                    // The root prim position is sim-relative, while child prim positions are
                    // parent-relative. We want to apply parent-relative translations but not
                    // sim-relative ones
                    if (Prims[i].Prim.ParentID != 0)
                    {
                        // Apply prim translation and rotation
                        GL.MultMatrix(Math3D.CreateTranslationMatrix(prim.Position));
                        GL.MultMatrix(Math3D.CreateRotationMatrix(prim.Rotation));
                    }

                    // Prim scaling
                    GL.Scale(prim.Scale.X, prim.Scale.Y, prim.Scale.Z);

                    // Draw the prim faces
                    for (int j = 0; j < Prims[i].Faces.Count; j++)
                    {
                        if (i == cboPrim.SelectedIndex)
                        {
                            // This prim is currently selected in the dropdown
                            //Gl.glColor3f(0f, 1f, 0f);
                            GL.Color3(1f, 1f, 1f);

                            if (j == cboFace.SelectedIndex)
                            {
                                // This face is currently selected in the dropdown
                            }
                            else
                            {
                                // This face is not currently selected in the dropdown
                            }
                        }
                        else
                        {
                            // This prim is not currently selected in the dropdown
                            GL.Color3(1f, 1f, 1f);
                        }

                        #region Texturing

                        Face face = Prims[i].Faces[j];
                        FaceData data = (FaceData)face.UserData;

                        if (data.TexturePointer != 0)
                        {
                            // Set the color to solid white so the texture is not altered
                            //Gl.glColor3f(1f, 1f, 1f);
                            // Enable texturing for this face
                            GL.Enable(EnableCap.Texture2D);
                        }
                        else
                        {
                            GL.Disable(EnableCap.Texture2D);
                        }

                        // Bind the texture
                        GL.BindTexture(TextureTarget.Texture2D, data.TexturePointer);

                        #endregion Texturing

                        GL.TexCoordPointer(2, TexCoordPointerType.Float, 0, data.TexCoords);
                        GL.VertexPointer(3, VertexPointerType.Float, 0, data.Vertices);
                        GL.DrawElements(BeginMode.Triangles, data.Indices.Length, DrawElementsType.UnsignedShort, data.Indices);
                    }

                    // Pop the prim matrix
                    GL.PopMatrix();
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
            GL.ClearColor(0.39f, 0.58f, 0.93f, 1.0f);

            GL.Viewport(0, 0, glControl.Width, glControl.Height);

            GL.PushMatrix();
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();

            OpenTK.Matrix4 perspectiveMatrix = OpenTK.Matrix4.CreatePerspectiveFieldOfView(50.0f, 1.0f, 0.1f, 256f);
            GL.LoadMatrix(ref perspectiveMatrix);

            GL.MatrixMode(MatrixMode.Modelview);
            GL.PopMatrix();
        }

        #endregion GLControl Callbacks

        #region Menu Callbacks

        private void openPrimXMLToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            Prims = null;
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Prim Package (*.zip)|*.zip|Sculpt Map (*.png)|*.png|OAR XML (*.xml)|*.xml";

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                if (dialog.FileName.ToLowerInvariant().EndsWith(".zip"))
                {
                    LoadPrimPackage(dialog.FileName);
                }
                else if (dialog.FileName.ToLowerInvariant().EndsWith(".xml"))
                {
                    LoadXmlPrim(dialog.FileName);
                }
                else
                {
                    LoadSculpt(dialog.FileName);
                }
            }
        }

        private void LoadDebugPrim()
        {
            Prims = new List<FacetedMesh>();
            Primitive prim = new Primitive();
            prim.Textures = new Primitive.TextureEntry(UUID.Zero);
            prim.Scale = Vector3.One;
            prim.PrimData = ObjectManager.BuildBasicShape(PrimType.Cylinder);
            prim.PrimData.ProfileHollow = 0.95f;
            SimpleMesh simpleMesh = Render.Plugin.GenerateSimpleMesh(prim, DetailLevel.High);
            FacetedMesh facetedMesh = new FacetedMesh();
            facetedMesh.Faces = new List<Face> { new Face { Vertices = simpleMesh.Vertices, Indices = simpleMesh.Indices } };
            facetedMesh.Path = simpleMesh.Path;
            facetedMesh.Profile = simpleMesh.Profile;
            facetedMesh.Prim = prim;
            LoadMesh(facetedMesh, ".");
            PopulatePrimCombobox();
            glControl.Invalidate();
        }

        private void LoadXmlPrim(string filename)
        {
            byte[] data = File.ReadAllBytes(filename);

            OpenMetaverse.Assets.OarFile.LoadObjects(data, XmlObjectLoadedHandler, 0, data.Length);
        }

        private void XmlObjectLoadedHandler(OpenMetaverse.Assets.AssetPrim linkset, long bytesRead, long totalBytes)
        {
            Prims = new List<FacetedMesh>(linkset.Children.Count + 1);

            Primitive parent = linkset.Parent.ToPrimitive();
            {
                FacetedMesh mesh = null;

                if (parent.Sculpt == null || parent.Sculpt.SculptTexture == UUID.Zero)
                    mesh = Render.Plugin.GenerateFacetedMesh(parent, DetailLevel.Highest);
                if (mesh != null)
                    LoadMesh(mesh, null);
            }

            for (int i = 0; i < linkset.Children.Count; i++)
            {
                Primitive child = linkset.Children[i].ToPrimitive();
                FacetedMesh mesh = null;

                if (parent.Sculpt == null || child.Sculpt.SculptTexture == UUID.Zero)
                    mesh = Render.Plugin.GenerateFacetedMesh(child, DetailLevel.Highest);
                if (mesh != null)
                    LoadMesh(mesh, null);
            }

            PopulatePrimCombobox();

            glControl.Invalidate();
        }

        private void LoadSculpt(string filename)
        {
            // Try to parse this as an image file
            Image sculptTexture = Image.FromFile(filename);

            Primitive prim = new Primitive();
            prim.PrimData = ObjectManager.BuildBasicShape(PrimType.Sculpt);
            prim.Sculpt = new Primitive.SculptData { SculptTexture = UUID.Random(), Type = SculptType.Sphere };
            prim.Textures = new Primitive.TextureEntry(UUID.Zero);
            prim.Scale = Vector3.One * 3f;

            FacetedMesh mesh = Render.Plugin.GenerateFacetedSculptMesh(prim, (Bitmap)sculptTexture, DetailLevel.Highest);
            if (mesh != null)
            {
                Prims = new List<FacetedMesh>(1);
                LoadMesh(mesh, null);
            }

            glControl.Invalidate();
        }

        private void LoadPrimPackage(string filename)
        {
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());

            try
            {
                // Create a temporary directory
                Directory.CreateDirectory(tempPath);

                FastZip fastzip = new FastZip();
                fastzip.ExtractZip(filename, tempPath, String.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }

            // Check for the prims.xml file
            string primsFile = System.IO.Path.Combine(tempPath, "prims.xml");
            if (!File.Exists(primsFile))
            {
                MessageBox.Show("prims.xml not found in the archive");
                return;
            }

            OSD osd = null;

            try { osd = OSDParser.DeserializeLLSDXml(File.ReadAllText(primsFile)); }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

            if (osd != null && osd.Type == OSDType.Map)
            {
                List<Primitive> primList = Helpers.OSDToPrimList(osd);
                Prims = new List<FacetedMesh>(primList.Count);

                for (int i = 0; i < primList.Count; i++)
                {
                    Primitive prim = primList[i];
                    FacetedMesh mesh = null;

                    if (prim.Sculpt.SculptTexture != UUID.Zero)
                    {
                        Image sculptTexture = null;
                        if (LoadTexture(tempPath, prim.Sculpt.SculptTexture, ref sculptTexture))
                            mesh = Render.Plugin.GenerateFacetedSculptMesh(prim, (Bitmap)sculptTexture, DetailLevel.Highest);
                    }
                    else
                    {
                        mesh = Render.Plugin.GenerateFacetedMesh(prim, DetailLevel.Highest);
                    }

                    if (mesh != null)
                        LoadMesh(mesh, tempPath);
                }

                // Setup the dropdown list of prims
                PopulatePrimCombobox();

                glControl.Invalidate();
            }
            else
            {
                MessageBox.Show("Failed to load LLSD formatted primitive data from " + filename);
            }

            Directory.Delete(tempPath);
        }

        private void LoadMesh(FacetedMesh mesh, string basePath)
        {
            // Create a FaceData struct for each face that stores the 3D data
            // in a Tao.OpenGL friendly format
            for (int j = 0; j < mesh.Faces.Count; j++)
            {
                Face face = mesh.Faces[j];
                FaceData data = new FaceData();

                // Vertices for this face
                data.Vertices = new float[face.Vertices.Count * 3];
                for (int k = 0; k < face.Vertices.Count; k++)
                {
                    data.Vertices[k * 3 + 0] = face.Vertices[k].Position.X;
                    data.Vertices[k * 3 + 1] = face.Vertices[k].Position.Y;
                    data.Vertices[k * 3 + 2] = face.Vertices[k].Position.Z;
                }

                // Indices for this face
                data.Indices = face.Indices.ToArray();

                // Texture transform for this face
                Primitive.TextureEntryFace teFace = mesh.Prim.Textures.GetFace((uint)j);
                Render.Plugin.TransformTexCoords(face.Vertices, face.Center, teFace, mesh.Prim.Scale);

                // Texcoords for this face
                data.TexCoords = new float[face.Vertices.Count * 2];
                for (int k = 0; k < face.Vertices.Count; k++)
                {
                    data.TexCoords[k * 2 + 0] = face.Vertices[k].TexCoord.X;
                    data.TexCoords[k * 2 + 1] = face.Vertices[k].TexCoord.Y;
                }

                // Texture for this face
                if (!String.IsNullOrEmpty(basePath) && LoadTexture(basePath, teFace.TextureID, ref data.Texture))
                {
                    Bitmap bitmap = new Bitmap(data.Texture);
                    bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
                    Rectangle rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                    BitmapData bitmapData = bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

                    GL.GenTextures(1, out data.TexturePointer);
                    GL.BindTexture(TextureTarget.Texture2D, data.TexturePointer);

                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)All.LinearMipmapLinear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)All.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)All.ClampToEdge);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)All.ClampToEdge);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.GenerateMipmap, 1);
                    
                    OpenTK.Graphics.Glu.Build2DMipmap(OpenTK.Graphics.TextureTarget.Texture2D, (int)OpenTK.Graphics.PixelInternalFormat.Rgb8,
                        bitmap.Width, bitmap.Height, OpenTK.Graphics.PixelFormat.Bgr, OpenTK.Graphics.PixelType.UnsignedByte, bitmapData.Scan0);

                    bitmap.UnlockBits(bitmapData);
                    bitmap.Dispose();
                }

                // Set the UserData for this face to our FaceData struct
                face.UserData = data;
                mesh.Faces[j] = face;
            }

            Prims.Add(mesh);
        }

        private bool LoadTexture(string basePath, UUID textureID, ref System.Drawing.Image texture)
        {
            if (Textures.ContainsKey(textureID))
            {
                texture = Textures[textureID];
                return true;
            }

            string texturePath = System.IO.Path.Combine(basePath, textureID.ToString());

            if (File.Exists(texturePath + ".tga"))
            {
                try
                {
                    texture = (Image)LoadTGAClass.LoadTGA(texturePath + ".tga");
                    Textures[textureID] = texture;
                    return true;
                }
                catch (Exception)
                {
                }
            }
            else if (File.Exists(texturePath + ".jp2"))
            {
                try
                {
                    ManagedImage managedImage;
                    if (OpenJPEG.DecodeToImage(File.ReadAllBytes(texturePath + ".jp2"), out managedImage, out texture))
                    {
                        Textures[textureID] = texture;
                        return true;
                    }
                }
                catch (Exception)
                {
                }
            }

            return false;
        }

        private void textureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            picTexture.Image = null;
            TexturePointers[0] = 0;

            OpenFileDialog dialog = new OpenFileDialog();

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    picTexture.Image = System.Drawing.Image.FromFile(dialog.FileName);
                    Bitmap bitmap = new Bitmap(picTexture.Image);
                    bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);

                    // Create the GL texture space
                    GL.GenTextures(1, TexturePointers);
                    Rectangle rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                    BitmapData bitmapData = bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

                    GL.BindTexture(TextureTarget.Texture2D, TexturePointers[0]);

                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)All.LinearMipmapLinear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)All.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)All.ClampToEdge);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)All.ClampToEdge);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.GenerateMipmap, 1);

                    OpenTK.Graphics.Glu.Build2DMipmap(OpenTK.Graphics.TextureTarget.Texture2D, (int)OpenTK.Graphics.PixelInternalFormat.Rgb8,
                        bitmap.Width, bitmap.Height, OpenTK.Graphics.PixelFormat.Bgr, OpenTK.Graphics.PixelType.UnsignedByte, bitmapData.Scan0);

                    bitmap.UnlockBits(bitmapData);
                    bitmap.Dispose();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to load image from file " + dialog.FileName + ": " + ex.Message);
                }
            }
        }

        private void savePrimXMLToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void saveTextureToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void oBJToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "OBJ files (*.obj)|*.obj";

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                if (!MeshToOBJ.MeshesToOBJ(Prims, dialog.FileName))
                {
                    MessageBox.Show("Failed to save file " + dialog.FileName +
                        ". Ensure that you have permission to write to that file and it is currently not in use");
                }
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                "Written by John Hurliman <jhurliman@jhurliman.org> (http://www.jhurliman.org/)");
        }

        #endregion Menu Callbacks

        #region Scrollbar Callbacks

        private void scroll_ValueChanged(object sender, EventArgs e)
        {
            glControl.Invalidate();
        }

        private void scrollZoom_ValueChanged(object sender, EventArgs e)
        {
            glControl_Resize(null, null);
            glControl.Invalidate();
        }

        #endregion Scrollbar Callbacks

        #region PictureBox Callbacks

        private void picTexture_MouseDown(object sender, MouseEventArgs e)
        {
            DraggingTexture = true;
        }

        private void picTexture_MouseUp(object sender, MouseEventArgs e)
        {
            DraggingTexture = false;
        }

        private void picTexture_MouseLeave(object sender, EventArgs e)
        {
            DraggingTexture = false;
        }

        private void picTexture_MouseMove(object sender, MouseEventArgs e)
        {
            if (DraggingTexture)
            {
                // What is the current action?
                // None, DraggingEdge, DraggingCorner, DraggingWhole
            }
            else
            {
                // Check if the mouse is close to the edge or corner of a selection
                // rectangle

                // If so, change the cursor accordingly
            }
        }

        private void picTexture_Paint(object sender, PaintEventArgs e)
        {
            // Draw the current selection rectangles
        }

        #endregion PictureBox Callbacks

        private void cboPrim_SelectedIndexChanged(object sender, EventArgs e)
        {
            CurrentPrim = (FacetedMesh)cboPrim.Items[cboPrim.SelectedIndex];
            PopulateFaceCombobox();

            glControl.Invalidate();
        }

        private void cboFace_SelectedIndexChanged(object sender, EventArgs e)
        {
            CurrentFace = (ProfileFace)cboFace.Items[cboFace.SelectedIndex];

            glControl.Invalidate();
        }

        private void PopulatePrimCombobox()
        {
            cboPrim.Items.Clear();

            if (Prims != null)
            {
                for (int i = 0; i < Prims.Count; i++)
                    cboPrim.Items.Add(Prims[i]);
            }

            if (cboPrim.Items.Count > 0)
                cboPrim.SelectedIndex = 0;
        }

        private void PopulateFaceCombobox()
        {
            cboFace.Items.Clear();

            if (CurrentPrim != null && CurrentPrim.Profile.Faces != null)
            {
                for (int i = 0; i < CurrentPrim.Profile.Faces.Count; i++)
                    cboFace.Items.Add(CurrentPrim.Profile.Faces[i]);
            }

            if (cboFace.Items.Count > 0)
                cboFace.SelectedIndex = 0;
        }

        private void wireframeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            wireframeToolStripMenuItem.Checked = !wireframeToolStripMenuItem.Checked;
            Wireframe = wireframeToolStripMenuItem.Checked;

            glControl.Invalidate();
        }

        private void worldBrowserToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmBrowser browser = new frmBrowser();
            browser.ShowDialog();
        }
    }

    public struct FaceData
    {
        public float[] Vertices;
        public ushort[] Indices;
        public float[] TexCoords;
        public int TexturePointer;
        public System.Drawing.Image Texture;
        // TODO: Normals
    }

    public static class Render
    {
        public static IRendering Plugin;
    }
}
