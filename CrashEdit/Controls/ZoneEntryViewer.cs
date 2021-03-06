using Crash;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CrashEdit
{
    public sealed class ZoneEntryViewer : SceneryEntryViewer
    {
        private static byte[] stipplea;
        private static byte[] stippleb;

        static ZoneEntryViewer()
        {
            stipplea = new byte[128];
            stippleb = new byte[128];
            for (int i = 0; i < 128; i += 8)
            {
                stipplea[i + 0] = 0x55;
                stipplea[i + 1] = 0x55;
                stipplea[i + 2] = 0x55;
                stipplea[i + 3] = 0x55;
                stipplea[i + 4] = 0xAA;
                stipplea[i + 5] = 0xAA;
                stipplea[i + 6] = 0xAA;
                stipplea[i + 7] = 0xAA;
                stippleb[i + 0] = 0xAA;
                stippleb[i + 1] = 0xAA;
                stippleb[i + 2] = 0xAA;
                stippleb[i + 3] = 0xAA;
                stippleb[i + 4] = 0x55;
                stippleb[i + 5] = 0x55;
                stippleb[i + 6] = 0x55;
                stippleb[i + 7] = 0x55;
            }
        }

        private ZoneEntry entry;
        private ZoneEntry[] linkedentries;
        private bool renderoctree;
        private int[] octreedisplaylists;
        private Dictionary<short,Color> octreevalues;
        private int octreeselection;
        private bool deletelists;
        private bool polygonmode;
        private bool allentries;
        private bool timetrialmode;
        private Form frmoctree;

        public ZoneEntryViewer(ZoneEntry entry,SceneryEntry[] linkedsceneryentries,TextureChunk[][] texturechunks,ZoneEntry[] linkedentries)
            : base(linkedsceneryentries,texturechunks)
        {
            this.entry = entry;
            this.linkedentries = linkedentries;
            renderoctree = false;
            octreedisplaylists = new int[linkedentries.Length + 1];
            for (int i = 0; i < octreedisplaylists.Length; i++)
            {
                octreedisplaylists[i] = -1;
            }
            octreevalues = new Dictionary<short,Color>();
            octreeselection = -1;
            deletelists = false;
            polygonmode = false;
            allentries = false;
            timetrialmode = false;
        }

        protected override IEnumerable<IPosition> CorePositions
        {
            get
            {
                int xoffset = BitConv.FromInt32(entry.Layout,0);
                int yoffset = BitConv.FromInt32(entry.Layout,4);
                int zoffset = BitConv.FromInt32(entry.Layout,8);
                yield return new Position(xoffset,yoffset,zoffset);
                int x2 = BitConv.FromInt32(entry.Layout,12);
                int y2 = BitConv.FromInt32(entry.Layout,16);
                int z2 = BitConv.FromInt32(entry.Layout,20);
                yield return new Position(x2 + xoffset,y2 + yoffset,z2 + zoffset);
                foreach (Entity entity in entry.Entities)
                {
                    if (entry.Entities.IndexOf(entity) % 3 == 0 || entity.ID != null)
                    {
                        foreach (EntityPosition position in entity.Positions)
                        {
                            int x = position.X + xoffset;
                            int y = position.Y + yoffset;
                            int z = position.Z + zoffset;
                            yield return new Position(x,y,z);
                        }
                    }
                }
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.X:
                case Keys.C:
                case Keys.R:
                case Keys.V:
                case Keys.F:
                case Keys.O:
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
                case Keys.X:
                    renderoctree = !renderoctree;
                    break;
                case Keys.C:
                    if (frmoctree == null || frmoctree.IsDisposed)
                    {
                        frmoctree = new Form();
                        frmoctree.FormClosed += (object sender, FormClosedEventArgs ee) =>
                        {
                            octreeselection = -1;
                            deletelists = true;
                        };
                        UpdateOctreeFormList();
                        frmoctree.Show();
                    }
                    else
                    {
                        frmoctree.Select();
                    }
                    break;
                case Keys.R:
                    deletelists = true;
                    UpdateOctreeFormList();
                    break;
                case Keys.V:
                    polygonmode = !polygonmode;
                    break;
                case Keys.F:
                    allentries = !allentries;
                    UpdateOctreeFormList();
                    break;
                case Keys.O:
                    timetrialmode = !timetrialmode;
                    break;
            }
        }

        internal void UpdateOctreeFormList()
        {
            if (frmoctree != null && !frmoctree.IsDisposed)
            {
                frmoctree.Controls.Clear();
                ListView lst = new ListView();
                lst.Dock = DockStyle.Fill;
                foreach (KeyValuePair<short,Color> color in octreevalues)
                {
                    ListViewItem lsi = new ListViewItem();
                    lsi.Text = string.Format("{2:X2}:{1:X2}:{0:X1}",color.Key >> 1 & 0x7,color.Key >> 4 & 0x3F,color.Key >> 10 & 0x3F);
                    lsi.BackColor = color.Value;
                    lsi.ForeColor = color.Value.GetBrightness() >= 0.5 ? Color.Black : Color.White;
                    lsi.Tag = color.Key;
                    lst.Items.Add(lsi);
                }
                lst.SelectedIndexChanged += delegate (object sender,EventArgs ee)
                {
                    if (lst.SelectedItems.Count == 0)
                    {
                        octreeselection = -1;
                    }
                    else
                    {
                        octreeselection = (ushort)(short)lst.SelectedItems[0].Tag;
                    }
                    deletelists = true;
                };
                frmoctree.Controls.Add(lst);
            }
        }

        protected override void RenderObjects()
        {
            GL.Disable(EnableCap.Texture2D);
            GL.TexEnv(TextureEnvTarget.TextureEnv, TextureEnvParameter.RgbScale, 1.0f);
            RenderEntry(entry,ref octreedisplaylists[0]);
            GL.Enable(EnableCap.PolygonStipple);
            for (int i = 0; i < linkedentries.Length; i++)
            {
                ZoneEntry linkedentry = linkedentries[i];
                if (linkedentry == entry)
                    continue;
                if (linkedentry == null)
                    continue;
                RenderLinkedEntry(linkedentry,ref octreedisplaylists[i + 1]);
            }
            GL.Disable(EnableCap.PolygonStipple);
            if (deletelists)
                deletelists = false;
            GL.TexEnv(TextureEnvTarget.TextureEnv, TextureEnvParameter.RgbScale, 2.0f);
            GL.Enable(EnableCap.Texture2D);
            base.RenderObjects();
        }

        private void RenderEntry(ZoneEntry entry,ref int octreedisplaylist)
        {
            int xoffset = BitConv.FromInt32(entry.Layout,0);
            int yoffset = BitConv.FromInt32(entry.Layout,4);
            int zoffset = BitConv.FromInt32(entry.Layout,8);
            int x2 = BitConv.FromInt32(entry.Layout,12);
            int y2 = BitConv.FromInt32(entry.Layout,16);
            int z2 = BitConv.FromInt32(entry.Layout,20);
            GL.PushMatrix();
            GL.Translate(xoffset,yoffset,zoffset);
            if (deletelists)
            {
                GL.DeleteLists(octreedisplaylist,1);
                octreedisplaylist = -1;
            }
            if (renderoctree)
            {
                if (!polygonmode)
                    GL.PolygonMode(MaterialFace.FrontAndBack,PolygonMode.Line);
                if (octreedisplaylist == -1)
                {
                    octreedisplaylist = GL.GenLists(1);
                    GL.NewList(octreedisplaylist,ListMode.CompileAndExecute);
                    GL.PushMatrix();
                    int xmax = (ushort)BitConv.FromInt16(entry.Layout,0x1E);
                    int ymax = (ushort)BitConv.FromInt16(entry.Layout,0x20);
                    int zmax = (ushort)BitConv.FromInt16(entry.Layout,0x22);
                    RenderOctree(entry.Layout,0x1C,0,0,0,x2,y2,z2,xmax,ymax,zmax);
                    GL.PopMatrix();
                    GL.EndList();
                }
                else
                {
                    GL.CallList(octreedisplaylist);
                }
                GL.PolygonMode(MaterialFace.FrontAndBack,PolygonMode.Fill);
            }
            GL.Color3(Color.White);
            GL.Begin(PrimitiveType.LineStrip);
            GL.Vertex3(0,0,0);
            GL.Vertex3(x2,0,0);
            GL.Vertex3(x2,y2,0);
            GL.Vertex3(0,y2,0);
            GL.Vertex3(0,0,0);
            GL.Vertex3(0,0,z2);
            GL.Vertex3(x2,0,z2);
            GL.Vertex3(x2,y2,z2);
            GL.Vertex3(0,y2,z2);
            GL.Vertex3(0,0,z2);
            GL.Vertex3(x2,0,z2);
            GL.Vertex3(x2,0,0);
            GL.Vertex3(x2,y2,0);
            GL.Vertex3(x2,y2,z2);
            GL.Vertex3(0,y2,z2);
            GL.Vertex3(0,y2,0);
            GL.End();
            GL.Scale(4,4,4);
            foreach (Entity entity in entry.Entities)
            {
                if (entity.ID != null)
                {
                    RenderEntity(entity,false);
                }
                else if (entry.Entities.IndexOf(entity) % 3 == 0)
                {
                    RenderEntity(entity,true);
                }
            }
            GL.PopMatrix();
        }

        private void RenderLinkedEntry(ZoneEntry entry,ref int octreedisplaylist)
        {
            int xoffset = BitConv.FromInt32(entry.Layout,0);
            int yoffset = BitConv.FromInt32(entry.Layout,4);
            int zoffset = BitConv.FromInt32(entry.Layout,8);
            int x2 = BitConv.FromInt32(entry.Layout,12);
            int y2 = BitConv.FromInt32(entry.Layout,16);
            int z2 = BitConv.FromInt32(entry.Layout,20);
            GL.PushMatrix();
            GL.Translate(xoffset,yoffset,zoffset);
            if (allentries)
            {
                if (deletelists)
                {
                    GL.DeleteLists(octreedisplaylist,1);
                    octreedisplaylist = -1;
                }
                if (renderoctree)
                {
                    GL.Disable(EnableCap.PolygonStipple);
                    if (!polygonmode)
                        GL.PolygonMode(MaterialFace.FrontAndBack,PolygonMode.Line);
                    if (octreedisplaylist == -1)
                    {
                        octreedisplaylist = GL.GenLists(1);
                        GL.NewList(octreedisplaylist,ListMode.CompileAndExecute);
                        GL.PushMatrix();
                        int xmax = (ushort)BitConv.FromInt16(entry.Layout,0x1E);
                        int ymax = (ushort)BitConv.FromInt16(entry.Layout,0x20);
                        int zmax = (ushort)BitConv.FromInt16(entry.Layout,0x22);
                        RenderOctree(entry.Layout,0x1C,0,0,0,x2,y2,z2,xmax,ymax,zmax);
                        GL.PopMatrix();
                        GL.EndList();
                    }
                    else
                    {
                        GL.CallList(octreedisplaylist);
                    }
                    GL.PolygonMode(MaterialFace.FrontAndBack,PolygonMode.Fill);
                    GL.Enable(EnableCap.PolygonStipple);
                }
            }
            GL.Scale(4,4,4);
            foreach (Entity entity in entry.Entities)
            {
                if (entity.ID != null)
                {
                    RenderEntity(entity,false);
                }
                else if (entry.Entities.IndexOf(entity) % 3 == 0)
                {
                    RenderEntity(entity,true);
                }
            }
            GL.PopMatrix();
        }

        private void RenderOctree(byte[] data,int offset,double x,double y,double z,double w,double h,double d,int xmax,int ymax,int zmax)
        {
            int value = (ushort)BitConv.FromInt16(data,offset);
            if ((value & 1) != 0)
            {
                Color color;
                if (!octreevalues.TryGetValue((short)value,out color))
                {
                    byte[] colorbuf = new byte[3];
                    Random random = new Random(value);
                    random.NextBytes(colorbuf);
                    color = Color.FromArgb(255,colorbuf[0],colorbuf[1],colorbuf[2]);
                    octreevalues.Add((short)value,color);
                }
                if (octreeselection != -1 && octreeselection != value)
                    return;
                Color c1 = Color.FromArgb((color.R + 4) % 256,(color.G + 4) % 256,(color.B + 4) % 256);
                Color c2 = Color.FromArgb((color.R + 8) % 256,(color.G + 8) % 256,(color.B + 8) % 256);
                Color c3 = Color.FromArgb((color.R + 12) % 256,(color.G + 12) % 256,(color.B + 12) % 256);
                Color c4 = Color.FromArgb((color.R + 16) % 256,(color.G + 16) % 256,(color.B + 16) % 256);
                GL.Color3(color);
                GL.Begin(PrimitiveType.Quads);
                // Bottom
                GL.Color3(c1);
                GL.Vertex3(x + 0,y + 0,z + 0);
                GL.Color3(c2);
                GL.Vertex3(x + w,y + 0,z + 0);
                GL.Color3(c3);
                GL.Vertex3(x + w,y + 0,z + d);
                GL.Color3(c4);
                GL.Vertex3(x + 0,y + 0,z + d);

                // Top
                GL.Color3(c1);
                GL.Vertex3(x + 0,y + h,z + 0);
                GL.Color3(c2);
                GL.Vertex3(x + w,y + h,z + 0);
                GL.Color3(c3);
                GL.Vertex3(x + w,y + h,z + d);
                GL.Color3(c4);
                GL.Vertex3(x + 0,y + h,z + d);

                // Left
                GL.Color3(c1);
                GL.Vertex3(x + 0,y + 0,z + 0);
                GL.Color3(c2);
                GL.Vertex3(x + 0,y + h,z + 0);
                GL.Color3(c3);
                GL.Vertex3(x + 0,y + h,z + d);
                GL.Color3(c4);
                GL.Vertex3(x + 0,y + 0,z + d);

                // Right
                GL.Color3(c1);
                GL.Vertex3(x + w,y + 0,z + 0);
                GL.Color3(c2);
                GL.Vertex3(x + w,y + h,z + 0);
                GL.Color3(c3);
                GL.Vertex3(x + w,y + h,z + d);
                GL.Color3(c4);
                GL.Vertex3(x + w,y + 0,z + d);

                // Front
                GL.Color3(c1);
                GL.Vertex3(x + 0,y + 0,z + 0);
                GL.Color3(c2);
                GL.Vertex3(x + w,y + 0,z + 0);
                GL.Color3(c3);
                GL.Vertex3(x + w,y + h,z + 0);
                GL.Color3(c4);
                GL.Vertex3(x + 0,y + h,z + 0);

                // Back
                GL.Color3(c1);
                GL.Vertex3(x + 0,y + 0,z + d);
                GL.Color3(c2);
                GL.Vertex3(x + w,y + 0,z + d);
                GL.Color3(c3);
                GL.Vertex3(x + w,y + h,z + d);
                GL.Color3(c4);
                GL.Vertex3(x + 0,y + h,z + d);
                GL.End();
            }
            else if (value != 0)
            {
                RenderOctreeX(data,ref value,x,y,z,w,h,d,xmax,ymax,zmax);
            }
        }

        private void RenderOctreeX(byte[] data,ref int offset,double x,double y,double z,double w,double h,double d,int xmax,int ymax,int zmax)
        {
            if (xmax > 0)
            {
                RenderOctreeY(data,ref offset,x + 0 / 2,y,z,w / 2,h,d,xmax - 1,ymax,zmax);
                RenderOctreeY(data,ref offset,x + w / 2,y,z,w / 2,h,d,xmax - 1,ymax,zmax);
            }
            else
            {
                RenderOctreeY(data,ref offset,x,y,z,w,h,d,xmax - 1,ymax,zmax);
            }
        }

        private void RenderOctreeY(byte[] data,ref int offset,double x,double y,double z,double w,double h,double d,int xmax,int ymax,int zmax)
        {
            if (ymax > 0)
            {
                RenderOctreeZ(data,ref offset,x,y + 0 / 2,z,w,h / 2,d,xmax,ymax - 1,zmax);
                RenderOctreeZ(data,ref offset,x,y + h / 2,z,w,h / 2,d,xmax,ymax - 1,zmax);
            }
            else
            {
                RenderOctreeZ(data,ref offset,x,y,z,w,h,d,xmax,ymax - 1,zmax);
            }
        }

        private void RenderOctreeZ(byte[] data,ref int offset,double x,double y,double z,double w,double h,double d,int xmax,int ymax,int zmax)
        {
            if (zmax > 0)
            {
                RenderOctree(data,offset,x,y,z + 0 / 2,w,h,d / 2,xmax,ymax,zmax - 1);
                offset += 2;
                RenderOctree(data,offset,x,y,z + d / 2,w,h,d / 2,xmax,ymax,zmax - 1);
                offset += 2;
            }
            else
            {
                RenderOctree(data,offset,x,y,z,w,h,d,xmax,ymax,zmax - 1);
                offset += 2;
            }
        }

        private void RenderEntity(Entity entity,bool camera)
        {
            if (camera)
                GL.PolygonStipple(stippleb);
            else
                GL.PolygonStipple(stipplea);
            if (entity.Positions.Count == 1)
            {
                EntityPosition position = entity.Positions[0];
                GL.PushMatrix();
                if (camera)
                    GL.Scale(0.25F,0.25F,0.25F);
                GL.Translate(position.X,position.Y,position.Z);
                if (camera)
                    GL.Scale(4,4,4);
                switch (entity.Type)
                {
                    case 0x3:
                        if (entity.Subtype.HasValue)
                        {
                            RenderPickup(entity.Subtype.Value);
                        }
                        break;
                    case 0x22:
                        if (entity.Subtype.HasValue)
                        {
                            RenderBox(entity.Subtype.Value, entity.TimeTrialReward.HasValue ? entity.TimeTrialReward.Value >> 8 : 0);
                        }
                        break;
                    default:
                        if (camera)
                            GL.Color3(Color.Yellow);
                        else
                            GL.Color3(Color.White);
                        LoadTexture(OldResources.PointTexture);
                        RenderSprite();
                        break;
                }
                GL.PopMatrix();
            }
            else
            {
                if (camera)
                    GL.Color3(Color.Green);
                else
                    GL.Color3(Color.Blue);
                GL.PushMatrix();
                if (camera)
                    GL.Scale(0.25F,0.25F,0.25F);
                GL.Begin(PrimitiveType.LineStrip);
                foreach (EntityPosition position in entity.Positions)
                {
                    GL.Vertex3(position.X,position.Y,position.Z);
                }
                GL.End();
                if (camera)
                    GL.Color3(Color.Yellow);
                else
                    GL.Color3(Color.Red);
                LoadTexture(OldResources.PointTexture);
                foreach (EntityPosition position in entity.Positions)
                {
                    GL.PushMatrix();
                    GL.Translate(position.X,position.Y,position.Z);
                    if (camera)
                        GL.Scale(4,4,4);
                    RenderSprite();
                    GL.PopMatrix();
                }
                GL.PopMatrix();
            }
        }

        private void RenderSprite()
        {
            GL.Enable(EnableCap.Texture2D);
            GL.PushMatrix();
            GL.Rotate(-rotx,0,1,0);
            GL.Rotate(-roty,1,0,0);
            GL.Begin(PrimitiveType.Quads);
            GL.TexCoord2(0,0);
            GL.Vertex2(-50,+50);
            GL.TexCoord2(1,0);
            GL.Vertex2(+50,+50);
            GL.TexCoord2(1,1);
            GL.Vertex2(+50,-50);
            GL.TexCoord2(0,1);
            GL.Vertex2(-50,-50);
            GL.End();
            GL.PopMatrix();
            GL.Disable(EnableCap.Texture2D);
        }

        private void RenderPickup(int subtype)
        {
            GL.Translate(0,50,0);
            GL.Color3(Color.White);
            LoadPickupTexture(subtype);
            GL.Enable(EnableCap.Texture2D);
            GL.PushMatrix();
            GL.Rotate(-rotx,0,1,0);
            GL.Rotate(-roty,1,0,0);
            ScalePickup(subtype);
            GL.Begin(PrimitiveType.Quads);
            GL.TexCoord2(0,0);
            GL.Vertex2(-50,+50);
            GL.TexCoord2(1,0);
            GL.Vertex2(+50,+50);
            GL.TexCoord2(1,1);
            GL.Vertex2(+50,-50);
            GL.TexCoord2(0,1);
            GL.Vertex2(-50,-50);
            GL.End();
            GL.PopMatrix();
            GL.Disable(EnableCap.Texture2D);
        }

        private void RenderBox(int subtype, int timetrialreward)
        {
            GL.Translate(0,50,0);
            GL.Enable(EnableCap.Texture2D);
            GL.Color3(Color.White);
            LoadBoxSideTexture(subtype, timetrialreward);
            GL.PushMatrix();
            GL.Color3(93/128F,93/128F,93/128F);
            RenderBoxFace();
            GL.Rotate(90,0,1,0);
            GL.Color3(51/128F,51/128F,76/128F);
            RenderBoxFace();
            GL.Rotate(90,0,1,0);
            RenderBoxFace();
            GL.Rotate(90,0,1,0);
            GL.Color3(115/128F,115/128F,92/128F);
            RenderBoxFace();
            GL.PopMatrix();
            LoadBoxTopTexture(subtype, timetrialreward);
            GL.PushMatrix();
            GL.Rotate(90,1,0,0);
            GL.Color3(33/128F,33/128F,59/128F);
            RenderBoxFace();
            GL.Rotate(180,1,0,0);
            GL.Color3(115/128F,115/128F,92/128F);
            RenderBoxFace();
            GL.PopMatrix();
            GL.Disable(EnableCap.Texture2D);
        }

        private void RenderBoxFace()
        {
            GL.Begin(PrimitiveType.Quads);
            GL.TexCoord2(0,0);
            GL.Vertex3(-50,+50,50);
            GL.TexCoord2(1,0);
            GL.Vertex3(+50,+50,50);
            GL.TexCoord2(1,1);
            GL.Vertex3(+50,-50,50);
            GL.TexCoord2(0,1);
            GL.Vertex3(-50,-50,50);
            GL.End();
        }

        private void LoadPickupTexture(int subtype)
        {
            switch (subtype)
            {
                case 5: // Life
                    LoadTexture(OldResources.LifeTexture);
                    break;
                case 6: // Mask
                    LoadTexture(OldResources.MaskTexture);
                    break;
                case 16: // Apple
                    LoadTexture(OldResources.AppleTexture);
                    break;
                default:
                    LoadTexture(OldResources.UnknownPickupTexture);
                    break;
            }
        }

        private void ScalePickup(int subtype)
        {
            switch (subtype)
            {
                case 5: // Life
                case 6: // Mask
                    GL.Scale(1.8f,1.125f,1);
                    break;
                case 16: // Apple
                    GL.Scale(0.675f,0.84375f,1);
                    break;
            }
        }

        private void LoadBoxTopTexture(int subtype, int timetrialreward)
        {
            switch (subtype)
            {
                case 0: // TNT
                    LoadTexture(OldResources.TNTBoxTopTexture);
                    break;
                case 2: // Empty
                case 3: // Spring
                case 6: // Fruit
                case 8: // Life
                case 9: // Doctor
                case 10: // Pickup
                    if (timetrialmode && subtype != 3 && timetrialreward >= 111 && timetrialreward <= 113)
                        LoadTexture(OldResources.TimeBoxTopTexture);
                    else
                        LoadTexture(OldResources.EmptyBoxTexture);
                    break;
                case 4: // Continue
                    if (timetrialmode)
                        LoadTexture(OldResources.EmptyBoxTexture);
                    else
                        LoadTexture(OldResources.ContinueBoxTexture);
                    break;
                case 5: // Iron
                case 7: // Action
                case 15: // Iron Spring
                    LoadTexture(OldResources.IronBoxTexture);
                    break;
                case 18: // Nitro
                    LoadTexture(OldResources.NitroBoxTopTexture);
                    break;
                case 23: // Steel
                    LoadTexture(OldResources.SteelBoxTexture);
                    break;
                case 24: // Action Nitro
                    LoadTexture(OldResources.ActionNitroBoxTopTexture);
                    break;
                default:
                    LoadTexture(OldResources.UnknownBoxTopTexture);
                    break;
            }
        }

        private void LoadBoxSideTexture(int subtype, int timetrialreward)
        {
            if (!timetrialmode)
            {
                switch (subtype)
                {
                    case 0: // TNT
                        LoadTexture(OldResources.TNTBoxTexture);
                        break;
                    case 2: // Empty
                        LoadTexture(OldResources.EmptyBoxTexture);
                        break;
                    case 3: // Spring
                        LoadTexture(OldResources.SpringBoxTexture);
                        break;
                    case 4: // Continue
                        LoadTexture(OldResources.ContinueBoxTexture);
                        break;
                    case 5: // Iron
                        LoadTexture(OldResources.IronBoxTexture);
                        break;
                    case 6: // Fruit
                        LoadTexture(OldResources.FruitBoxTexture);
                        break;
                    case 7: // Action
                        LoadTexture(OldResources.ActionBoxTexture);
                        break;
                    case 8: // Life
                        LoadTexture(OldResources.LifeBoxTexture);
                        break;
                    case 9: // Doctor
                        LoadTexture(OldResources.DoctorBoxTexture);
                        break;
                    case 10: // Pickup
                        LoadTexture(OldResources.PickupBoxTexture);
                        break;
                    case 13: // Ghost
                        LoadTexture(OldResources.UnknownBoxTopTexture);
                        break;
                    case 15: // Iron Spring
                        LoadTexture(OldResources.IronSpringBoxTexture);
                        break;
                    case 18: // Nitro
                        LoadTexture(OldResources.NitroBoxTexture);
                        break;
                    case 23: // Steel
                        LoadTexture(OldResources.SteelBoxTexture);
                        break;
                    case 24: // Action Nitro
                        LoadTexture(OldResources.ActionNitroBoxTexture);
                        break;
                    default:
                        LoadTexture(OldResources.UnknownBoxTexture);
                        break;
                }
            }
            else
            {
                switch (subtype)
                {
                    case 0: // TNT
                        LoadTexture(OldResources.TNTBoxTexture);
                        break;
                    case 2: // Empty
                    case 4: // Continue
                    case 6: // Fruit
                    case 8: // Life
                    case 10: // Pickup
                        LoadBoxSideTextureTimeTrial(timetrialreward);
                        break;
                    case 3: // Spring
                        LoadTexture(OldResources.SpringBoxTexture);
                        break;
                    case 5: // Iron
                        LoadTexture(OldResources.IronBoxTexture);
                        break;
                    case 7: // Action
                        LoadTexture(OldResources.ActionBoxTexture);
                        break;
                    case 9: // Doctor
                        LoadTexture(OldResources.DoctorBoxTexture);
                        break;
                    case 13: // Ghost
                        LoadTexture(OldResources.UnknownBoxTopTexture);
                        break;
                    case 15: // Iron Spring
                        LoadTexture(OldResources.IronSpringBoxTexture);
                        break;
                    case 18: // Nitro
                        LoadTexture(OldResources.NitroBoxTexture);
                        break;
                    case 23: // Steel
                        LoadTexture(OldResources.SteelBoxTexture);
                        break;
                    case 24: // Action Nitro
                        LoadTexture(OldResources.ActionNitroBoxTexture);
                        break;
                    default:
                        LoadTexture(OldResources.UnknownBoxTexture);
                        break;
                }
            }
        }

        private void LoadBoxSideTextureTimeTrial(int timetrialreward)
        {
            switch (timetrialreward)
            {
                case 102: // Doctor
                    LoadTexture(OldResources.DoctorBoxTexture);
                    break;
                case 111: // Time 1
                    LoadTexture(OldResources.Time1BoxTexture);
                    break;
                case 112: // Time 2
                    LoadTexture(OldResources.Time2BoxTexture);
                    break;
                case 113: // Time 3
                    LoadTexture(OldResources.Time3BoxTexture);
                    break;
                default: // Empty
                    LoadTexture(OldResources.EmptyBoxTexture);
                    break;
            }
        }
    }
}
