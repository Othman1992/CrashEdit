using Crash;
using CrashEdit.Properties;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CrashEdit
{
    public sealed class OldAnimationEntryViewer : ThreeDimensionalViewer
    {
        private List<OldFrame> frames;
        private OldModelEntry model;
        private int frameid;
        private Timer animatetimer;
        private int interi;
        private int interp = 2;
        private bool colored;
        private float r, g, b;
        private bool collisionenabled;
        private bool texturesenabled = true;
        private bool normalsenabled = true;
        private bool interp_startend = false;
        private int cullmode = 0;

        private Dictionary<int,TextureChunk> texturechunks;
        private bool init;

        public OldAnimationEntryViewer(OldFrame frame,bool colored,OldModelEntry model,Dictionary<int,TextureChunk> texturechunks)
        {
            collisionenabled = Settings.Default.DisplayFrameCollision;
            frames = new List<OldFrame>() { frame };
            this.model = model;
            this.texturechunks = texturechunks;
            this.colored = colored;
            init = false;
            InitTextures(1);
            frameid = 0;
        }

        public OldAnimationEntryViewer(IEnumerable<OldFrame> frames,bool colored,OldModelEntry model,Dictionary<int,TextureChunk> texturechunks)
        {
            collisionenabled = Settings.Default.DisplayFrameCollision;
            this.frames = new List<OldFrame>(frames);
            this.model = model;
            this.texturechunks = texturechunks;
            this.colored = colored;
            init = false;
            InitTextures(1);
            frameid = 0;
            animatetimer = new Timer
            {
                Interval = 1000 / OldMainForm.GetRate() / interp,
                Enabled = true
            };
            animatetimer.Tick += delegate (object sender,EventArgs e)
            {
                animatetimer.Interval = 1000 / OldMainForm.GetRate() / interp;
                ++interi;
                if (interi >= interp)
                {
                    interi = 0;
                    frameid = (frameid + 1) % this.frames.Count;
                }
                Refresh();
            };
        }

        private int MinScale => model != null ? Math.Min(BitConv.FromInt32(model.Info, 12), Math.Min(BitConv.FromInt32(model.Info, 4), BitConv.FromInt32(model.Info, 8))) : 0x1000;
        private int MaxScale => model != null ? Math.Max(BitConv.FromInt32(model.Info, 12), Math.Max(BitConv.FromInt32(model.Info, 4), BitConv.FromInt32(model.Info, 8))) : 0x1000;

        protected override int CameraRangeMargin => 128;
        protected override float ScaleFactor => 8;
        protected override float NearPlane => 4;
        protected override float FarPlane => 64000;

        protected override IEnumerable<IPosition> CorePositions
        {
            get
            {
                var vec = model != null ? new Vector3(BitConv.FromInt32(model.Info,4),BitConv.FromInt32(model.Info,8),BitConv.FromInt32(model.Info,12))/MinScale : new Vector3(1,1,1);
                yield return new Position(128,128,128);
                foreach (OldFrame frame in frames)
                {
                    foreach (OldFrameVertex vertex in frame.Vertices)
                    {
                        float x = vertex.X + frame.XOffset;
                        float y = vertex.Y + frame.YOffset;
                        float z = vertex.Z + frame.ZOffset;
                        x *= vec.X;
                        y *= vec.Y;
                        z *= vec.Z;
                        yield return new Position(x,y,z);
                    }
                }
            }
        }

        protected override void RenderObjects()
        {
            if (!init && model != null)
            {
                init = true;
                ConvertTexturesToGL(0,texturechunks,model.Structs);
            }
            if ((frameid + 1) == frames.Count)
            {
                if (interp_startend)
                {
                    RenderFrame(frames[frameid], frames[0]);
                }
                else
                {
                    RenderFrame(frames[frameid]);
                    interi = interp - 1;
                }
            }
            else
            {
                RenderFrame(frames[frameid], frames[frameid+1]);
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.C:
                case Keys.N:
                case Keys.T:
                case Keys.U:
                    return true;
                default:
                    return base.IsInputKey(keyData);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.C:
                    collisionenabled = !collisionenabled;
                    break;
                case Keys.N:
                    normalsenabled = !normalsenabled;
                    break;
                case Keys.T:
                    texturesenabled = !texturesenabled;
                    break;
                case Keys.U:
                    cullmode = ++cullmode % 3;
                    break;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            GL.TexEnv(TextureEnvTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, (int)TextureEnvMode.Combine);
            GL.TexEnv(TextureEnvTarget.TextureEnv, TextureEnvParameter.RgbScale, 2.0f);
        }

        private void RenderFrame(OldFrame frame, OldFrame f2 = null)
        {
            if (model != null)
            {
                if (Settings.Default.DisplayAnimGrid)
                {
                    GL.PushMatrix();
                    GL.Translate(128, 128, 128);
                    GL.Scale(new Vector3(102400*4F/MinScale));
                    GL.Begin(PrimitiveType.Lines);
                    GL.Color3(Color.Red);
                    GL.Vertex3(-0.5F, 0, 0);
                    GL.Vertex3(+0.5F, 0, 0);
                    GL.Color3(Color.Green);
                    GL.Vertex3(0, -0.5F, 0);
                    GL.Vertex3(0, +0.5F, 0);
                    GL.Color3(Color.Blue);
                    GL.Vertex3(0, 0, -0.5F);
                    GL.Vertex3(0, 0, +0.5F);
                    GL.Color3(Color.Gray);
                    int gridamt = Settings.Default.AnimGridLen;
                    float gridlen = 1.0F * gridamt - 0.5F;
                    for (int i = 0; i < gridamt; ++i)
                    {
                        GL.Vertex3(+0.5F + i * 1F, 0, +gridlen);
                        GL.Vertex3(+0.5F + i * 1F, 0, -gridlen);
                        GL.Vertex3(-0.5F - i * 1F, 0, +gridlen);
                        GL.Vertex3(-0.5F - i * 1F, 0, -gridlen);
                        GL.Vertex3(+gridlen, 0, +0.5F + i * 1F);
                        GL.Vertex3(-gridlen, 0, +0.5F + i * 1F);
                        GL.Vertex3(+gridlen, 0, -0.5F - i * 1F);
                        GL.Vertex3(-gridlen, 0, -0.5F - i * 1F);
                    }
                    GL.End();
                    GL.PopMatrix();
                }
                if (cullmode < 2)
                {
                    GL.Enable(EnableCap.CullFace);
                    GL.CullFace(cullFaceModes[cullmode]);
                }
                if (texturesenabled)
                    GL.Enable(EnableCap.Texture2D);
                else
                    GL.Disable(EnableCap.Texture2D);
                if (normalsenabled && !colored)
                    GL.Enable(EnableCap.Lighting);
                else
                    GL.Disable(EnableCap.Lighting);
                GL.PushMatrix();
                GL.Scale(new Vector3(BitConv.FromInt32(model.Info,4),BitConv.FromInt32(model.Info,8),BitConv.FromInt32(model.Info,12))/MinScale);
                foreach (OldModelPolygon polygon in model.Polygons)
                {
                    OldModelStruct str = model.Structs[polygon.Unknown & 0x7FFF];
                    if (str is OldModelTexture tex)
                    {
                        BindTexture(0,polygon.Unknown & 0x7FFF);
                        GL.Color3(tex.R,tex.G,tex.B);
                        if (colored)
                        {
                            r = tex.R / 128F;
                            g = tex.G / 128F;
                            b = tex.B / 128F;
                        }
                        if (tex.N && cullmode < 2)
                        {
                            GL.Disable(EnableCap.CullFace);
                        }
                        GL.Begin(PrimitiveType.Triangles);
                        GL.TexCoord2(tex.X1,tex.Y1);
                        RenderVertex(frame,f2,polygon.VertexA / 6);
                        GL.TexCoord2(tex.X3,tex.Y3);
                        RenderVertex(frame,f2,polygon.VertexC / 6);
                        GL.TexCoord2(tex.X2,tex.Y2);
                        RenderVertex(frame,f2,polygon.VertexB / 6);
                        GL.End();
                        if (tex.N && cullmode < 2)
                        {
                            GL.Enable(EnableCap.CullFace);
                        }
                    }
                    else
                    {
                        UnbindTexture();
                        OldSceneryColor col = (OldSceneryColor)str;
                        GL.Color3(col.R,col.G,col.B);
                        if (colored)
                        {
                            r = col.R / 128F;
                            g = col.G / 128F;
                            b = col.B / 128F;
                        }
                        if (col.N && cullmode < 2)
                        {
                            GL.Disable(EnableCap.CullFace);
                        }
                        GL.Begin(PrimitiveType.Triangles);
                        RenderVertex(frame,f2,polygon.VertexA / 6);
                        RenderVertex(frame,f2,polygon.VertexC / 6);
                        RenderVertex(frame,f2,polygon.VertexB / 6);
                        GL.End();
                        if (col.N && cullmode < 2)
                        {
                            GL.Enable(EnableCap.CullFace);
                        }
                    }
                }
                GL.Disable(EnableCap.CullFace);
                GL.PopMatrix();
                GL.Disable(EnableCap.Texture2D);
                GL.Disable(EnableCap.Lighting);
            }
            else
            {
                GL.Color3(Color.White);
                GL.Begin(PrimitiveType.Points);
                for (int i = 0, m = frame.Vertices.Count; i < m; ++i)
                {
                    RenderVertex(frame, f2, i);
                }
                GL.End();
            }
            if (!colored && normalsenabled && Settings.Default.DisplayNormals)
            {
                GL.Begin(PrimitiveType.Lines);
                GL.Color3(Color.Cyan);
                foreach (OldFrameVertex vertex in frame.Vertices)
                {
                    GL.Vertex3(vertex.X + frame.XOffset,vertex.Y + frame.YOffset,vertex.Z + frame.ZOffset);
                    GL.Vertex3(vertex.X + vertex.NormalX / 127F * 4 + frame.XOffset,
                        vertex.Y + vertex.NormalY / 127F * 4 + frame.YOffset,
                        vertex.Z + vertex.NormalZ / 127F * 4 + frame.ZOffset);
                }
                GL.End();
            }
            if (collisionenabled)
            {
                RenderCollision(frame);
            }
        }

        private void RenderVertex(OldFrame f1, OldFrame f2, int id)
        {
            float f = (float)interi / interp;
            if (f2 == null)
            {
                f2 = f1;
            }
            OldFrameVertex v1 = f1.Vertices[id];
            OldFrameVertex v2 = f2.Vertices[id];
            int x1 = v1.X + f1.XOffset;
            int x2 = v2.X + f2.XOffset;
            int y1 = v1.Y + f1.YOffset;
            int y2 = v2.Y + f2.YOffset;
            int z1 = v1.Z + f1.ZOffset;
            int z2 = v2.Z + f2.ZOffset;
            if (colored)
            {
                int r1 = (byte)v1.NormalX;
                int r2 = (byte)v2.NormalX;
                int g1 = (byte)v1.NormalY;
                int g2 = (byte)v2.NormalY;
                int b1 = (byte)v1.NormalZ;
                int b2 = (byte)v2.NormalZ;
                byte nr = (byte)(NumberExt.GetFac(r1,r2,f) * r);
                byte ng = (byte)(NumberExt.GetFac(g1,g2,f) * g);
                byte nb = (byte)(NumberExt.GetFac(b1,b2,f) * b);
                GL.Color3(nr,ng,nb);
            }
            else if (normalsenabled)
            {
                int nx1 = v1.NormalX;
                int nx2 = v2.NormalX;
                int ny1 = v1.NormalY;
                int ny2 = v2.NormalY;
                int nz1 = v1.NormalZ;
                int nz2 = v2.NormalZ;
                GL.Normal3(NumberExt.GetFac(nx1,nx2,f)/127F,NumberExt.GetFac(ny1,ny2,f)/127F,NumberExt.GetFac(nz1,nz2,f)/127F);
            }
            GL.Vertex3(NumberExt.GetFac(x1,x2,f),NumberExt.GetFac(y1,y2,f),NumberExt.GetFac(z1,z2,f));
        }

        private void RenderCollision(OldFrame frame)
        {
            GL.DepthMask(false);
            GL.Color4(0f, 1f, 0f, 0.2f);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            RenderCollisionBox(frame);
            GL.Color4(0f, 1f, 0f, 1f);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            RenderCollisionBox(frame);
            GL.DepthMask(true);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
        }

        private void RenderCollisionBox(OldFrame frame)
        {
            int xcol1 = frame.X1;
            int xcol2 = frame.X2;
            int ycol1 = frame.Y1;
            int ycol2 = frame.Y2;
            int zcol1 = frame.Z1;
            int zcol2 = frame.Z2;
            GL.PushMatrix();
            GL.Translate(128,128,128);
            GL.Scale(new Vector3(4F/MinScale));
            GL.Translate(frame.XGlobal,frame.YGlobal,frame.ZGlobal);
            GL.Begin(PrimitiveType.QuadStrip);
            GL.Vertex3(xcol1,ycol1,zcol1);
            GL.Vertex3(xcol1,ycol2,zcol1);
            GL.Vertex3(xcol2,ycol1,zcol1);
            GL.Vertex3(xcol2,ycol2,zcol1);
            GL.Vertex3(xcol2,ycol1,zcol2);
            GL.Vertex3(xcol2,ycol2,zcol2);
            GL.Vertex3(xcol1,ycol1,zcol2);
            GL.Vertex3(xcol1,ycol2,zcol2);
            GL.Vertex3(xcol1,ycol1,zcol1);
            GL.Vertex3(xcol1,ycol2,zcol1);
            GL.End();
            GL.Begin(PrimitiveType.Quads);
            GL.Vertex3(xcol1,ycol1,zcol1);
            GL.Vertex3(xcol2,ycol1,zcol1);
            GL.Vertex3(xcol2,ycol1,zcol2);
            GL.Vertex3(xcol1,ycol1,zcol2);

            GL.Vertex3(xcol1,ycol2,zcol1);
            GL.Vertex3(xcol2,ycol2,zcol1);
            GL.Vertex3(xcol2,ycol2,zcol2);
            GL.Vertex3(xcol1,ycol2,zcol2);
            GL.End();
            GL.PopMatrix();
        }

        protected override void Dispose(bool disposing)
        {
            if (animatetimer != null)
            {
                animatetimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
