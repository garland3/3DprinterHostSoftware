﻿/*
   Copyright 2011 repetier repetierdev@gmail.com

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using OpenTK.Graphics.OpenGL;
using OpenTK.Platform.Windows;
using OpenTK;
using System.Diagnostics;
using System.Globalization;
using RepetierHost.model;

namespace RepetierHost.view
{
    //public delegate void onObjectMoved(float dx, float dy);
    //public delegate void onObjectSelected(ThreeDModel selModel);


    /// <summary>
    /// The user control ThreeDControl is the .stl OpenGL frame and Controller. Most of the rendering works happens here. 
    /// Basically a OpenGL wrapper as a userControl. 
    /// </summary>
    public partial class ThreeDControl : UserControl
    {
        FormPrinterSettings ps = Main.printerSettings;
        bool loaded = false;
        float xDown, yDown;
        float xPos, yPos;
        float speedX, speedY;
        // float zoom = 1.0f;
        Vector3 startViewCenter;
        Vector3 startUserPosition;
        double normX = 0, normY = 0;
        float startRotZ = 0, startRotX = 0;
        float lastX, lastY;
        Stopwatch sw = new Stopwatch();
        Stopwatch fpsTimer = new Stopwatch();
        int mode = 0;
        int slowCounter = 0; // Indicates slow framerates
        uint timeCall = 0;
        bool perspectiveModeisParrallel = false;


        public ThreeDView view = null;

        public ThreeDControl()
        {
            InitializeComponent();
            gl.MouseWheel += gl_MouseWheel;
            timer.Start();
            translate();
            Main.main.languageChanged += translate;
        }
        private void translate()
        {
          
        }
        public void SetView(ThreeDView view)
        {
            this.view = view;
            toolStripClear.Visible = view.autoupdateable;
            if (view.editor)
            {
                //////{
                ////    //toolMoveObject.Visible = true;
                ////    //toolStripClear.Enabled = false;
                //////}
                //////toolMoveObject.Enabled = view.objectsSelected;
                toolStripClear.Enabled = view.objectsSelected;
            }

            UpdateChanges();
        }
        public void MakeVisible(bool vis)
        {
            //return;
            //Console.WriteLine("Vis = " + vis);
            if (vis)
            {
                if (!Controls.Contains(gl))
                    Controls.Add(gl);
                //gl.Dock = DockStyle.None;
                //gl.Width = 0;
                //gl.Height = 0;
            }
            else
            {
                if (Controls.Contains(gl))
                    Controls.Remove(gl);
                //gl.Dock = DockStyle.Fill;
            }
            gl.Visible = vis;
        }
        public void SetObjectSelected(bool sel)
        {
            //toolMoveObject.Enabled = sel;
            toolStripClear.Enabled = sel;
            view.objectsSelected = sel;
        }
        public bool AutoUpdateable
        {
            get { return view.autoupdateable; }
            set
            {
                view.autoupdateable = value;
                if (view.autoupdateable)
                {
                    timer.Start();
                }
                else
                {
                    timer.Stop();
                }
                toolStripClear.Visible = value;
            }
        }
        public void UpdateChanges()
        {
            gl.Invalidate();
        }
        protected override void OnPaint(PaintEventArgs pe)
        {
            base.OnPaint(pe);
        }

        /// <summary>
        /// Sets up the openTL viewport based on the view Center and current Zoom. 
        /// </summary>
        private void SetupViewport()
        {
            try
            {
                int w = gl.Width;
                int h = gl.Height;
                GL.Viewport(0, 0, w, h); // Use all of the glControl painting area
                GL.MatrixMode(MatrixMode.Projection);
                //GL.LoadIdentity();
                float dx = view.viewCenter.X - view.userPosition.X;
                float dy = view.viewCenter.Y - view.userPosition.Y;
                float dz = view.viewCenter.Z - view.userPosition.Z;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                view.nearHeight = 2.0f * (float)Math.Tan(view.zoom * 15f * Math.PI / 180f) * view.nearDist;
                view.aspectRatio = (float)w / (float)h;
                view.nearDist = Math.Max(10, dist - 2f * ps.PrintAreaDepth);
                view.farDist = dist + 2 * ps.PrintAreaDepth;
                if (perspectiveModeisParrallel ==  true)
                {
                    view.persp = Matrix4.CreateOrthographic(4.0f * view.nearHeight * view.aspectRatio, 4.0f * view.nearHeight, view.nearDist, view.farDist);
                }
                else
                {
                    view.persp = Matrix4.CreatePerspectiveFieldOfView((float)(view.zoom * 30f * Math.PI / 180f), view.aspectRatio, view.nearDist, view.farDist);
                }
                GL.LoadMatrix(ref view.persp);
                // GL.Ortho(0, w, 0, h, -1, 1); // Bottom-left corner pixel has coordinate (0, 0)

            }
            catch { }
        }

        private void gl_Paint(object sender, PaintEventArgs e)
        {
            if (view == null)
            {
                return;
            }

            try
            {
                if (!loaded) return;
                // Check drawing method
                //// Main.threeDSettings.currentDrawMethod == view.ThreeDSettings.drawMethod.VBO;
                ThreeDSettings.drawMethod tempMethod = ThreeDSettings.currentDrawMethod; // hreeDSettings.currentDrawMethod;
                ////int om = Main.threeDSettings.drawMethod;
                switch (Main.threeDSettings.comboDrawMethod.SelectedIndex)
                {
                    case 0: // Autodetect;
                        if (Main.threeDSettings.useVBOs && Main.threeDSettings.openGLVersion >= 1.499)
                        {
                            ThreeDSettings.currentDrawMethod = ThreeDSettings.drawMethod.VBO;
                            //// Main.threeDSettings.drawMethod = 2;
                        }
                        else if (Main.threeDSettings.openGLVersion >= 1.099)
                        {
                            ThreeDSettings.currentDrawMethod = ThreeDSettings.drawMethod.Elements;
                           //// Main.threeDSettings.drawMethod = 1;
                        }
                        else
                        {
                            ThreeDSettings.currentDrawMethod = ThreeDSettings.drawMethod.DrawElements;
                            ////Main.threeDSettings.drawMethod = 0;
                        }

                        break;
                    case 1: // VBOs
                        ThreeDSettings.currentDrawMethod = ThreeDSettings.drawMethod.VBO;
                        ////Main.threeDSettings.drawMethod = 2;
                        break;
                    case 2: // drawElements
                        ThreeDSettings.currentDrawMethod = ThreeDSettings.drawMethod.Elements;
                        ////Main.threeDSettings.drawMethod = 1;
                        break;
                    case 3: // elements
                        ThreeDSettings.currentDrawMethod = ThreeDSettings.drawMethod.DrawElements;
                        //// Main.threeDSettings.drawMethod = 0;
                        break;
                }

                //// if (tempMethod != Main.threeDSettings.drawMethod)
                if (tempMethod != ThreeDSettings.currentDrawMethod)
                {
                    Main.main.updateTravelMoves();
                }
               
                fpsTimer.Reset(); // Reset the stop watch
                fpsTimer.Start(); // Start the stop watch
                gl.MakeCurrent(); // Set all GL commands to the current rendering. 
               
                ////GL.Enable(EnableCap.Multisample);
                GL.ClearColor(Main.threeDSettings.background.BackColor); // Set the color that clears the screen. 
                 GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
              
                GL.Enable(EnableCap.DepthTest);            
                SetupViewport();
               
                view.lookAt = Matrix4.LookAt(view.userPosition.X, view.userPosition.Y, view.userPosition.Z, view.viewCenter.X, view.viewCenter.Y, view.viewCenter.Z, 0, 0, 1.0f);
               
                if (SimpleTest() == true)
                {
                    return;
                }

                GL.MatrixMode(MatrixMode.Modelview); // Change to the Model View Matrix. 
                GL.LoadMatrix(ref view.lookAt);
                GL.ShadeModel(ShadingModel.Smooth);
                // GL.Enable(EnableCap.LineSmooth);
                //Enable lighting
                GL.Light(LightName.Light0, LightParameter.Ambient, new float[] { 0.2f, 0.2f, 0.2f, 1f });
                GL.Light(LightName.Light0, LightParameter.Diffuse, new float[] { 0, 0, 0, 0 });
                GL.Light(LightName.Light0, LightParameter.Specular, new float[] { 0, 0, 0, 0 });
                GL.Enable(EnableCap.Light0);
                if (Main.threeDSettings.enableLight1.Checked)
                {
                    GL.Light(LightName.Light1, LightParameter.Ambient, Main.threeDSettings.Ambient1());
                    GL.Light(LightName.Light1, LightParameter.Diffuse, Main.threeDSettings.Diffuse1());
                    GL.Light(LightName.Light1, LightParameter.Specular, Main.threeDSettings.Specular1());
                    GL.Light(LightName.Light1, LightParameter.Position, Main.threeDSettings.Dir1());
                    //  GL.Light(LightName.Light1, LightParameter.SpotExponent, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
                    GL.Enable(EnableCap.Light1);
                }
                else
                {
                    GL.Disable(EnableCap.Light1);
                }

                if (Main.threeDSettings.enableLight2.Checked)
                {
                    GL.Light(LightName.Light2, LightParameter.Ambient, Main.threeDSettings.Ambient2());
                    GL.Light(LightName.Light2, LightParameter.Diffuse, Main.threeDSettings.Diffuse2());
                    GL.Light(LightName.Light2, LightParameter.Specular, Main.threeDSettings.Specular2());
                    GL.Light(LightName.Light2, LightParameter.Position, Main.threeDSettings.Dir2());
                    /*  GL.Light(LightName.Light2, LightParameter.Diffuse, new float[] { 0.7f, 0.7f, 0.7f, 1f });
                      GL.Light(LightName.Light2, LightParameter.Specular, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
                      GL.Light(LightName.Light2, LightParameter.Position, (new Vector4(100f, 200f, 300f, 0)));*/
                    GL.Light(LightName.Light2, LightParameter.SpotExponent, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
                    GL.Enable(EnableCap.Light2);
                }
                else
                {
                    GL.Disable(EnableCap.Light2);
                }

                if (Main.threeDSettings.enableLight3.Checked)
                {
                    GL.Light(LightName.Light3, LightParameter.Ambient, Main.threeDSettings.Ambient3());
                    GL.Light(LightName.Light3, LightParameter.Diffuse, Main.threeDSettings.Diffuse3());
                    GL.Light(LightName.Light3, LightParameter.Specular, Main.threeDSettings.Specular3());
                    GL.Light(LightName.Light3, LightParameter.Position, Main.threeDSettings.Dir3());
                    /*  GL.Light(LightName.Light3, LightParameter.Diffuse, new float[] { 0.8f, 0.8f, 0.8f, 1f });
                      GL.Light(LightName.Light3, LightParameter.Specular, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
                      GL.Light(LightName.Light3, LightParameter.Position, (new Vector4(100f, -200f, 200f, 0)));*/
                    GL.Light(LightName.Light3, LightParameter.SpotExponent, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
                    GL.Enable(EnableCap.Light3);
                }
                else
                {
                    GL.Disable(EnableCap.Light3);
                }

                if (Main.threeDSettings.enableLight4.Checked)
                {
                    GL.Light(LightName.Light4, LightParameter.Ambient, Main.threeDSettings.Ambient4());
                    GL.Light(LightName.Light4, LightParameter.Diffuse, Main.threeDSettings.Diffuse4());
                    GL.Light(LightName.Light4, LightParameter.Specular, Main.threeDSettings.Specular4());
                    GL.Light(LightName.Light4, LightParameter.Position, Main.threeDSettings.Dir4());
                    /* GL.Light(LightName.Light4, LightParameter.Diffuse, new float[] { 0.7f, 0.7f, 0.7f, 1f });
                     GL.Light(LightName.Light4, LightParameter.Specular, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
                     GL.Light(LightName.Light4, LightParameter.Position, (new Vector4(170f, -100f, -250f, 0)));*/
                    GL.Light(LightName.Light4, LightParameter.SpotExponent, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
                    GL.Enable(EnableCap.Light4);
                }
                else
                {
                    GL.Disable(EnableCap.Light4);
                }

                GL.Enable(EnableCap.Lighting);
                //Enable Backfaceculling
                GL.Enable(EnableCap.CullFace);
                GL.Enable(EnableCap.LineSmooth);
                //GL.Enable(EnableCap.PolygonSmooth);
                GL.Enable(EnableCap.Blend);
                GL.LineWidth(2f);
                GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);
                GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
                Color col = Main.threeDSettings.printerBase.BackColor;
                // Draw viewpoint
                /*GL.Material(
                    MaterialFace.Front,
                    MaterialParameter.Emission,
                    new OpenTK.Graphics.Color4(col.R, col.G, col.B, col.A));
                GL.Begin(BeginMode.Lines);
                GL.Vertex3(viewCenter.X - 2, viewCenter.Y, viewCenter.Z);
                GL.Vertex3(viewCenter.X + 2, viewCenter.Y, viewCenter.Z);
                GL.Vertex3(viewCenter.X, viewCenter.Y - 2, viewCenter.Z);
                GL.Vertex3(viewCenter.X, viewCenter.Y + 2, viewCenter.Z);
                GL.Vertex3(viewCenter.X, viewCenter.Y, viewCenter.Z - 2);
                GL.Vertex3(viewCenter.X, viewCenter.Y, viewCenter.Z + 2);
                GL.End();*/

                GL.Rotate(view.rotX, 1, 0, 0);
                GL.Rotate(view.rotZ, 0, 0, 1);
                GL.Translate(-ps.BedLeft - ps.PrintAreaWidth * 0.5f, -ps.BedFront - ps.PrintAreaDepth * 0.5f, -0.5f * ps.PrintAreaHeight);
                GL.GetFloat(GetPName.ModelviewMatrix, out view.modelView);
                GL.Material(
                    MaterialFace.Front,
                    MaterialParameter.Specular,
                    new OpenTK.Graphics.Color4(255, 255, 255, 255));

                float dx1 = ps.DumpAreaLeft;
                float dx2 = dx1 + ps.DumpAreaWidth;
                float dy1 = ps.DumpAreaFront;
                float dy2 = dy1 + ps.DumpAreaDepth;
                if (Main.threeDSettings.showPrintbed.Checked)
                {
                    col = Main.threeDSettings.printerBase.BackColor;
                    GL.Material(MaterialFace.FrontAndBack, MaterialParameter.AmbientAndDiffuse, new OpenTK.Graphics.Color4(0, 0, 0, 255));
                    GL.Material(MaterialFace.Front, MaterialParameter.Emission, new OpenTK.Graphics.Color4(0, 0, 0, 0));
                    GL.Material(MaterialFace.Front, MaterialParameter.Specular, new float[] { 0.0f, 0.0f, 0.0f, 1.0f });
                    GL.Material(
                        MaterialFace.Front,
                        MaterialParameter.Emission,
                        new OpenTK.Graphics.Color4(col.R, col.G, col.B, col.A));
                    int i;
                    // Draw origin
                    GL.Disable(EnableCap.CullFace);
                    GL.Begin(BeginMode.Triangles);
                    GL.Normal3(0, 0, 1);
                    double delta = Math.PI / 8;
                    double rad = 2.5;
                    for (i = 0; i < 16; i++)
                    {
                        GL.Vertex3(0, 0, 0);
                        GL.Vertex3(rad * Math.Sin(i * delta), rad * Math.Cos(i * delta), 0);
                        GL.Vertex3(rad * Math.Sin((i + 1) * delta), rad * Math.Cos((i + 1) * delta), 0);
                    }

                    GL.End();
                    GL.Begin(BeginMode.Lines);
                    if (ps.printerType < 2)
                    {
                        // Print cube
                        GL.Vertex3(ps.BedLeft, ps.BedFront, 0);
                        GL.Vertex3(ps.BedLeft, ps.BedFront, ps.PrintAreaHeight);
                        GL.Vertex3(ps.BedLeft + ps.PrintAreaWidth, ps.BedFront, 0);
                        GL.Vertex3(ps.BedLeft + ps.PrintAreaWidth, ps.BedFront, ps.PrintAreaHeight);
                        GL.Vertex3(ps.BedLeft, ps.BedFront + ps.PrintAreaDepth, 0);
                        GL.Vertex3(ps.BedLeft, ps.BedFront + ps.PrintAreaDepth, ps.PrintAreaHeight);
                        GL.Vertex3(ps.BedLeft + ps.PrintAreaWidth, ps.BedFront + ps.PrintAreaDepth, 0);
                        GL.Vertex3(ps.BedLeft + ps.PrintAreaWidth, ps.BedFront + ps.PrintAreaDepth, ps.PrintAreaHeight);
                        GL.Vertex3(ps.BedLeft, ps.BedFront, ps.PrintAreaHeight);
                        GL.Vertex3(ps.BedLeft + ps.PrintAreaWidth, ps.BedFront, ps.PrintAreaHeight);
                        GL.Vertex3(ps.BedLeft + ps.PrintAreaWidth, ps.BedFront, ps.PrintAreaHeight);
                        GL.Vertex3(ps.BedLeft + ps.PrintAreaWidth, ps.BedFront + ps.PrintAreaDepth, ps.PrintAreaHeight);
                        GL.Vertex3(ps.BedLeft + ps.PrintAreaWidth, ps.BedFront + ps.PrintAreaDepth, ps.PrintAreaHeight);
                        GL.Vertex3(ps.BedLeft, ps.BedFront + ps.PrintAreaDepth, ps.PrintAreaHeight);
                        GL.Vertex3(ps.BedLeft, ps.BedFront + ps.PrintAreaDepth, ps.PrintAreaHeight);
                        GL.Vertex3(ps.BedLeft, ps.BedFront, ps.PrintAreaHeight);
                        if (ps.printerType == 1)
                        {
                            if (dy1 != 0)
                            {
                                GL.Vertex3(ps.BedLeft + dx1, ps.BedFront + dy1, 0);
                                GL.Vertex3(ps.BedLeft + dx2, ps.BedFront + dy1, 0);
                            }
                            GL.Vertex3(ps.BedLeft + dx2, ps.BedFront + dy1, 0);
                            GL.Vertex3(ps.BedLeft + dx2, ps.BedFront + dy2, 0);
                            GL.Vertex3(ps.BedLeft + dx2, ps.BedFront + dy2, 0);
                            GL.Vertex3(ps.BedLeft + dx1, ps.BedFront + dy2, 0);
                            GL.Vertex3(ps.BedLeft + dx1, ps.BedFront + dy2, 0);
                            GL.Vertex3(ps.BedLeft + dx1, ps.BedFront + dy1, 0);
                        }
                        float dx = 10; // ps.PrintAreaWidth / 20f;
                        float dy = 10; // ps.PrintAreaDepth / 20f;
                        float x, y;
                        for (i = 0; i < 200; i++)
                        {
                            x = (float)i * dx;
                            if (x >= ps.PrintAreaWidth)
                            {
                                x = ps.PrintAreaWidth;
                            }

                            if (ps.printerType == 1 && x >= dx1 && x <= dx2)
                            {
                                GL.Vertex3(ps.BedLeft + x, ps.BedFront, 0);
                                GL.Vertex3(ps.BedLeft + x, ps.BedFront + dy1, 0);
                                GL.Vertex3(ps.BedLeft + x, ps.BedFront + dy2, 0);
                                GL.Vertex3(ps.BedLeft + x, ps.BedFront + ps.PrintAreaDepth, 0);
                            }
                            else
                            {
                                GL.Vertex3(ps.BedLeft + x, ps.BedFront, 0);
                                GL.Vertex3(ps.BedLeft + x, ps.BedFront + ps.PrintAreaDepth, 0);
                            }

                            if (x >= ps.PrintAreaWidth) break;
                        }

                        for (i = 0; i < 200; i++)
                        {
                            y = (float)i * dy;
                            if (y > ps.PrintAreaDepth)
                            {
                                y = ps.PrintAreaDepth;
                            }

                            if (ps.printerType == 1 && y >= dy1 && y <= dy2)
                            {
                                GL.Vertex3(ps.BedLeft, ps.BedFront + y, 0);
                                GL.Vertex3(ps.BedLeft + dx1, ps.BedFront + y, 0);
                                GL.Vertex3(ps.BedLeft + dx2, ps.BedFront + y, 0);
                                GL.Vertex3(ps.BedLeft + ps.PrintAreaWidth, ps.BedFront + y, 0);
                            }
                            else
                            {
                                GL.Vertex3(ps.BedLeft, ps.BedFront + y, 0);
                                GL.Vertex3(ps.BedLeft + ps.PrintAreaWidth, ps.BedFront + y, 0);
                            }

                            if (y >= ps.PrintAreaDepth)
                            {
                                break;
                            }
                        }
                    }
                    else if (ps.printerType == 2) // Cylinder shape
                    {
                        int ncirc = 32;
                        int vertexevery = 4;
                        delta = (float)(Math.PI * 2 / ncirc);
                        float alpha = 0;
                        for (i = 0; i < ncirc; i++)
                        {
                            float alpha2 = (float)(alpha + delta);
                            float x1 = (float)(ps.rostockRadius * Math.Sin(alpha));
                            float y1 = (float)(ps.rostockRadius * Math.Cos(alpha));
                            float x2 = (float)(ps.rostockRadius * Math.Sin(alpha2));
                            float y2 = (float)(ps.rostockRadius * Math.Cos(alpha2));
                            GL.Vertex3(x1, y1, 0);
                            GL.Vertex3(x2, y2, 0);
                            GL.Vertex3(x1, y1, ps.rostockHeight);
                            GL.Vertex3(x2, y2, ps.rostockHeight);
                            if ((i % vertexevery) == 0)
                            {
                                GL.Vertex3(x1, y1, 0);
                                GL.Vertex3(x1, y1, ps.rostockHeight);
                            }

                            alpha = alpha2;
                        }

                        delta = 10;
                        float x = (float)(Math.Floor(ps.rostockRadius / delta) * delta);
                        while (x > -ps.rostockRadius)
                        {
                            alpha = (float)Math.Acos(x / ps.rostockRadius);
                            float y = (float)(ps.rostockRadius * Math.Sin(alpha));
                            GL.Vertex3(x, -y, 0);
                            GL.Vertex3(x, y, 0);
                            GL.Vertex3(y, x, 0);
                            GL.Vertex3(-y, x, 0);
                            x -= (float)delta;
                        }
                    }
                    else if (ps.printerType == 3)
                    {
                        float dx = 10; // ps.PrintAreaWidth / 20f;
                        float dy = 10; // ps.PrintAreaDepth / 20f;
                        float x, y;
                        for (i = 0; i < 200; i++)
                        {
                            x = (float)i * dx;
                            if (x >= ps.PrintAreaWidth)
                                x = ps.PrintAreaWidth;
                            if (ps.printerType == 1 && x >= dx1 && x <= dx2)
                            {
                                GL.Vertex3(ps.BedLeft + x, ps.BedFront, 0);
                                GL.Vertex3(ps.BedLeft + x, ps.BedFront + dy1, 0);
                                GL.Vertex3(ps.BedLeft + x, ps.BedFront + dy2, 0);
                                GL.Vertex3(ps.BedLeft + x, ps.BedFront + ps.PrintAreaDepth, 0);
                            }
                            else
                            {
                                GL.Vertex3(ps.BedLeft + x, ps.BedFront, 0);
                                GL.Vertex3(ps.BedLeft + x, ps.BedFront + ps.PrintAreaDepth, 0);
                            }

                            if (x >= ps.PrintAreaWidth)
                            {
                                break;
                            }
                        }
                        for (i = 0; i < 200; i++)
                        {
                            y = (float)i * dy;
                            if (y > ps.PrintAreaDepth)
                                y = ps.PrintAreaDepth;
                            if (ps.printerType == 1 && y >= dy1 && y <= dy2)
                            {
                                GL.Vertex3(ps.BedLeft, ps.BedFront + y, 0);
                                GL.Vertex3(ps.BedLeft + dx1, ps.BedFront + y, 0);
                                GL.Vertex3(ps.BedLeft + dx2, ps.BedFront + y, 0);
                                GL.Vertex3(ps.BedLeft + ps.PrintAreaWidth, ps.BedFront + y, 0);
                            }
                            else
                            {
                                GL.Vertex3(ps.BedLeft, ps.BedFront + y, 0);
                                GL.Vertex3(ps.BedLeft + ps.PrintAreaWidth, ps.BedFront + y, 0);
                            }

                            if (y >= ps.PrintAreaDepth)
                            {
                                break;
                            }
                        }
                    }

                    GL.End();
                }

                ////if (Main.main.tab.SelectedIndex > 1)
                if ((Main.main.current3Dview == Main.ThreeDViewOptions.gcode) || (Main.main.current3Dview == Main.ThreeDViewOptions.livePrinting))
                {
                    GL.Enable(EnableCap.CullFace);
                }
                else
                {
                    GL.Disable(EnableCap.CullFace);
                }

                GL.Disable(EnableCap.LineSmooth);

                // For all the models in the current view. Draw them. 
                foreach (ThreeDModel model in view.models)
                {
                    GL.PushMatrix();
                    model.AnimationBefore();
                    GL.Translate(model.Position.x, model.Position.y, model.Position.z);
                    GL.Rotate(model.Rotation.z, Vector3.UnitZ);
                    GL.Rotate(model.Rotation.y, Vector3.UnitY);
                    GL.Rotate(model.Rotation.x, Vector3.UnitX);
                    GL.Scale(model.Scale.x, model.Scale.y, model.Scale.z);

                    // Paint the model using Open
                    model.Paint();
                    model.AnimationAfter();
                    GL.PopMatrix();

                    // If the model is selected, then draw a box around it. 
                    if (model.Selected)
                    {
                        GL.PushMatrix();
                        model.AnimationBefore();
                        col = Main.threeDSettings.selectionBox.BackColor;
                        GL.Material(MaterialFace.FrontAndBack, MaterialParameter.AmbientAndDiffuse, new OpenTK.Graphics.Color4(0, 0, 0, 255));
                        GL.Material(MaterialFace.Front, MaterialParameter.Emission, new OpenTK.Graphics.Color4(0, 0, 0, 0));
                        GL.Material(MaterialFace.Front, MaterialParameter.Specular, new float[] { 0.0f, 0.0f, 0.0f, 1.0f });
                        GL.Material(
                            MaterialFace.Front,
                            MaterialParameter.Emission,
                            new OpenTK.Graphics.Color4(col.R, col.G, col.B, col.A));
                        GL.Begin(BeginMode.Lines);
                        GL.Vertex3(model.xMin, model.yMin, model.zMin);
                        GL.Vertex3(model.xMax, model.yMin, model.zMin);

                        GL.Vertex3(model.xMin, model.yMin, model.zMin);
                        GL.Vertex3(model.xMin, model.yMax, model.zMin);

                        GL.Vertex3(model.xMin, model.yMin, model.zMin);
                        GL.Vertex3(model.xMin, model.yMin, model.zMax);

                        GL.Vertex3(model.xMax, model.yMax, model.zMax);
                        GL.Vertex3(model.xMin, model.yMax, model.zMax);

                        GL.Vertex3(model.xMax, model.yMax, model.zMax);
                        GL.Vertex3(model.xMax, model.yMin, model.zMax);

                        GL.Vertex3(model.xMax, model.yMax, model.zMax);
                        GL.Vertex3(model.xMax, model.yMax, model.zMin);

                        GL.Vertex3(model.xMin, model.yMax, model.zMax);
                        GL.Vertex3(model.xMin, model.yMax, model.zMin);

                        GL.Vertex3(model.xMin, model.yMax, model.zMax);
                        GL.Vertex3(model.xMin, model.yMin, model.zMax);

                        GL.Vertex3(model.xMax, model.yMax, model.zMin);
                        GL.Vertex3(model.xMax, model.yMin, model.zMin);

                        GL.Vertex3(model.xMax, model.yMax, model.zMin);
                        GL.Vertex3(model.xMin, model.yMax, model.zMin);

                        GL.Vertex3(model.xMax, model.yMin, model.zMax);
                        GL.Vertex3(model.xMin, model.yMin, model.zMax);

                        GL.Vertex3(model.xMax, model.yMin, model.zMax);
                        GL.Vertex3(model.xMax, model.yMin, model.zMin);

                        GL.End();
                        model.AnimationAfter();                                                                                                                 
                        GL.PopMatrix();
                    }
                } // End loop of models.


                /* if (drawRay)
                 {
                     col = Main.threeDSettings.printerBase.BackColor;
                     GL.Material(MaterialFace.FrontAndBack, MaterialParameter.AmbientAndDiffuse, new OpenTK.Graphics.Color4(0, 0, 0, 255));
                     GL.Material(MaterialFace.Front, MaterialParameter.Emission, new OpenTK.Graphics.Color4(0, 0, 0, 0));
                     GL.Material(MaterialFace.Front, MaterialParameter.Specular, new float[] { 0.0f, 0.0f, 0.0f, 1.0f });
                     GL.Material(
                         MaterialFace.Front,
                         MaterialParameter.Emission,
                         new OpenTK.Graphics.Color4(255,0,0,255));
                     GL.Begin(BeginMode.Lines);
                     GL.Vertex3(rayStart);
                     GL.Vertex3(rayEnd);
                     GL.End();
                 }*/
                if (Main.threeDSettings.showPrintbed.Checked)
                {
                    GL.Disable(EnableCap.CullFace);
                    GL.Enable(EnableCap.Blend);	// Turn Blending On
                    GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
                    //GL.Disable(EnableCap.Lighting);
                    // Draw bottom
                    col = Main.threeDSettings.printerBase.BackColor;
                    float[] transblack = new float[] { 0, 0, 0, 0 };
                    GL.Material(MaterialFace.FrontAndBack, MaterialParameter.AmbientAndDiffuse, new OpenTK.Graphics.Color4(col.R, col.G, col.B, 130));
                    GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, transblack);
                    GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Emission, transblack);
                    GL.PushMatrix();
                    GL.Translate(0, 0, -0.04);
                    if (ps.printerType < 2 || ps.printerType == 3)
                    {
                        GL.Begin(BeginMode.Quads);
                        GL.Normal3(0, 0, 1);

                        if (ps.printerType == 1)
                        {
                            if (dy1 > 0)
                            {
                                GL.Vertex3(ps.BedLeft, ps.BedFront, 0);
                                GL.Vertex3(ps.BedLeft + ps.PrintAreaWidth, ps.BedFront, 0);
                                GL.Vertex3(ps.BedLeft + ps.PrintAreaWidth, ps.BedFront + dy1, 0);
                                GL.Vertex3(ps.BedLeft, ps.BedFront + dy1, 0);
                            }
                            if (dy2 < ps.PrintAreaDepth)
                            {
                                GL.Vertex3(ps.BedLeft, ps.BedFront + dy2, 0);
                                GL.Vertex3(ps.BedLeft + ps.PrintAreaWidth, ps.BedFront + dy2, 0);
                                GL.Vertex3(ps.BedLeft + ps.PrintAreaWidth, ps.BedFront + ps.PrintAreaDepth, 0);
                                GL.Vertex3(ps.BedLeft, ps.BedFront + ps.PrintAreaDepth, 0);
                            }
                            if (dx1 > 0)
                            {
                                GL.Vertex3(ps.BedLeft, ps.BedFront + dy1, 0);
                                GL.Vertex3(ps.BedLeft + dx1, ps.BedFront + dy1, 0);
                                GL.Vertex3(ps.BedLeft + dx1, ps.BedFront + dy2, 0);
                                GL.Vertex3(ps.BedLeft, ps.BedFront + dy2, 0);
                            }
                            if (dx2 < ps.PrintAreaWidth)
                            {
                                GL.Vertex3(ps.BedLeft + dx2, ps.BedFront + dy1, 0);
                                GL.Vertex3(ps.BedLeft + ps.PrintAreaWidth, ps.BedFront + dy1, 0);
                                GL.Vertex3(ps.BedLeft + ps.PrintAreaWidth, ps.BedFront + dy2, 0);
                                GL.Vertex3(ps.BedLeft + dx2, ps.BedFront + dy2, 0);
                            }
                        }
                        else
                        {
                            GL.Vertex3(ps.BedLeft, ps.BedFront, 0);
                            GL.Vertex3(ps.BedLeft + ps.PrintAreaWidth, ps.BedFront, 0);
                            GL.Vertex3(ps.BedLeft + ps.PrintAreaWidth, ps.BedFront + ps.PrintAreaDepth, 0);
                            GL.Vertex3(ps.BedLeft + 0, ps.BedFront + ps.PrintAreaDepth, 0);
                        }

                        GL.End();
                    }
                    else if (ps.printerType == 2)
                    {
                        int ncirc = 32;
                        float delta = (float)(Math.PI * 2 / ncirc);
                        float alpha = 0;
                        GL.Begin(BeginMode.Quads);
                        GL.Normal3(0, 0, 1);
                        for (int i = 0; i < ncirc / 4; i++)
                        {
                            float alpha2 = (float)(alpha + delta);
                            float x1 = (float)(ps.rostockRadius * Math.Sin(alpha));
                            float y1 = (float)(ps.rostockRadius * Math.Cos(alpha));
                            float x2 = (float)(ps.rostockRadius * Math.Sin(alpha2));
                            float y2 = (float)(ps.rostockRadius * Math.Cos(alpha2));
                            GL.Vertex3(x1, y1, 0);
                            GL.Vertex3(x2, y2, 0);
                            GL.Vertex3(-x2, y2, 0);
                            GL.Vertex3(-x1, y1, 0);
                            GL.Vertex3(x1, -y1, 0);
                            GL.Vertex3(x2, -y2, 0);
                            GL.Vertex3(-x2, -y2, 0);
                            GL.Vertex3(-x1, -y1, 0);
                            alpha = alpha2;
                        }

                        GL.End();
                    }

                    GL.PopMatrix();
                    GL.Disable(EnableCap.Blend);
                } // End show the print bed. 

                //// Draw a gradient background. 
                ////http://stackoverflow.com/questions/468297/in-opengl-how-do-i-make-a-simple-background-quad

                //GL.MatrixMode(MatrixMode.Projection); // glMatrixMode(GL_PROJECTION);
                //GL.PushMatrix(); // glPushMatrix();
                //GL.LoadIdentity(); // glLoadIdentity();
                //int w = gl.Width;
                //int h = gl.Height;
                //GL.Rects(0,(short) w,(short) h, 0);

                //GL.PopMatrix(); //glPopMatrix() // Pops the matrix that we used to draw the quad
                //// GL.MatrixMode(MatrixMode.Projection); //glMatrixMode(GL_PROJECTION);
                ////glPopMatrix(); // Pops our orthographic projection matrix, which restores the old one
                //GL.MatrixMode(MatrixMode.Modelview); //glMatrixMode

                //glLoadIdentity();
                //glMatrixMode(GL_MODELVIEW);
                //glLoadIdentity();
              
               

                //GL.Disable(EnableCap.CullFace); // CAn see it from both sies. 
                //GL.Enable(EnableCap.Blend);	// Turn Blending On
               


                //GL.Material(MaterialFace.FrontAndBack, MaterialParameter.AmbientAndDiffuse, new OpenTK.Graphics.Color4(0, 0, 0, 255));
                //GL.Material(MaterialFace.Front, MaterialParameter.Emission, new OpenTK.Graphics.Color4(1, 0, 0, 0));
                //GL.Material(MaterialFace.Front, MaterialParameter.Specular, new float[] { 0.0f, 0.0f, 0.0f, 1.0f });
                //GL.Material(
                //    MaterialFace.Front,
                //    MaterialParameter.Emission,
                //    new OpenTK.Graphics.Color4(col.R, col.G, col.B, col.A));

                ////GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
                //GL.Begin(BeginMode.Quads);// glBegin(GL_QUADS);
                //////red color
              
                //int tempSize = 300;
                //Vector3 towardView = view.userPosition - view.viewCenter;
                //Matrix4 rotateM = Matrix4.CreateFromAxisAngle(towardView, MathHelper.PiOver2);
                //Vector4 towardView4 = new Vector4(towardView, 1);
                //Vector4 dir1 = Vector4.Multiply( towardView4 * rotateM;

               // GL.MatrixMode(MatrixMode.

                    
                 //GL.Rotate(model.Rotation.z, Vector3.UnitZ);
                 //GL.Rotate(model.Rotation.y, Vector3.UnitY);
                 //GL.Rotate(model.Rotation.x, Vector3.UnitX);

                
                                //GL.Vertex3(ps.BedLeft, ps.BedFront, 0);
                                //GL.Vertex3(ps.BedLeft + ps.PrintAreaWidth, ps.BedFront, 0);
                                //GL.Vertex3(ps.BedLeft + ps.PrintAreaWidth, ps.BedFront + dy1, 0);
                //                //GL.Vertex3(ps.BedLeft, ps.BedFront + dy1, 0);
                            
                //GL.Color3(1.0, 0.0, 0.0); //glColor3f(1.0, 0.0, 0.0);
                //GL.Vertex3(-tempSize, tempSize,-20); //glVertex2f(-1.0, 1.0);
                //GL.Vertex3(-tempSize, -tempSize,-20); //glVertex2f(-1.0, -1.0);
                //////blue color
                //GL.Color3(1.0, 1.0, 0.0); //glColor3f(0.0, 0.0, 1.0);
                //GL.Vertex3(tempSize, -tempSize,-20); //glVertex2f(1.0, -1.0);
                //GL.Vertex3(tempSize, tempSize,-20); //glVertex2f(1.0, 1.0);
                //GL.End(); //glEnd();

                // END Attempt 1. 


                gl.SwapBuffers();  // Show the scene on the screen. 
                fpsTimer.Stop();  // Stop the stopwatch timer. 

                //// double time = fpsTimer.Elapsed.Milliseconds / 1000.0;
                //// PrinterConnection.logInfo("OpenGL update time:" + time.ToString());

                double framerate = 1.0 / fpsTimer.Elapsed.TotalSeconds;
                Main.main.fpsLabel.Text = framerate.ToString("0") + " FPS";
                if ((framerate < 30) && (Main.globalSettings.DisableQualityReduction == false))
                {
                    slowCounter++;
                    if (slowCounter >= 10)
                    {
                        slowCounter = 0;
                        foreach (ThreeDModel model in view.models)
                        {
                            model.ReduceQuality();
                        }
                    }
                }
                else if (slowCounter > 0)
                {
                    slowCounter--;
                }
            }
            catch { }
        }

        private bool SimpleTest()
        {
            gl.MakeCurrent();
            
            GL.Viewport(ClientRectangle.X, ClientRectangle.Y, ClientRectangle.Width, ClientRectangle.Height);

            Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView((float)Math.PI / 4, Width / (float)Height, 1.0f, 64.0f);
            GL.MatrixMode(MatrixMode.Projection);
            GL.PushMatrix();
            GL.LoadMatrix(ref projection);

            //--------------------------------
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            Matrix4 modelview = Matrix4.LookAt(Vector3.Zero, Vector3.UnitZ, Vector3.UnitY);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.PushMatrix();
            GL.LoadMatrix(ref modelview);

            GL.Begin(BeginMode.Triangles);

            GL.Color3(1.0f, 1.0f, 0.0f); GL.Vertex3(-1.0f, -1.0f, 4.0f);
            GL.Color3(1.0f, 0.0f, 0.0f); GL.Vertex3(1.0f, -1.0f, 4.0f);
            GL.Color3(0.2f, 0.9f, 1.0f); GL.Vertex3(0.0f, 1.0f, 4.0f);

            GL.End();
            GL.PopMatrix();
            GL.PopMatrix();

            //gl.SwapBuffers();

            return false;

           // throw new NotImplementedException();
        }

        private void DrawBackGroundGradient()
        {
            // http://stackoverflow.com/questions/468297/in-opengl-how-do-i-make-a-simple-background-quad

            GL.MatrixMode(MatrixMode.Projection); //glMatrixMode(GL_PROJECTION);
            GL.PushMatrix(); //glPushMatrix();
            GL.LoadIdentity(); //glLoadIdentity();
            int w = gl.Width; //glutGet(GLUT_WINDOW_WIDTH);
            int h = gl.Height; //glutGet(GLUT_WINDOW_HEIGHT);
            Matrix4 temp = Matrix4.CreatePerspectiveFieldOfView(0,w,0,h);
            GL.LoadMatrix(ref temp);
            GL.MatrixMode(MatrixMode.Modelview); //glMatrixMode(GL_MODELVIEW);
            GL.PushMatrix(); //glPushMatrix();
            GL.LoadIdentity(); //glLoadIdentity();
            // Draw your quad here in screen coordinates
             GL.Begin(BeginMode.Quads);
            
             GL.Vertex2(-10, 10); //glVertex2f(-1.0, 1.0);
             GL.Vertex2(-10, 10); //glVertex2f(-1.0, -1.0);

             GL.PopMatrix(); //glPopMatrix() // Pops the matrix that we used to draw the quad
             GL.MatrixMode(MatrixMode.Projection);//glMatrixMode(GL_PROJECTION);
             GL.PopMatrix(); // glPopMatrix(); // Pops our orthographic projection matrix, which restores the old one

            
            // glMatrixMode(GL_MODELVIEW); // Puts us back into GL_MODELVIEW since this is probably what 


            //throw new NotImplementedException();
        }

        static bool configureSettings = true;
        private void ThreeDControl_Load(object sender, EventArgs e)
        {
            if (configureSettings)
            {
                try
                {
                    Main.connection.log("OpenGL version:" + GL.GetString(StringName.Version), false, 3);
                    Main.connection.log("OpenGL extensions:" + GL.GetString(StringName.Extensions), false, 3);
                    Main.connection.log("OpenGL renderer:" + GL.GetString(StringName.Renderer), false, 3);
                    string sv = GL.GetString(StringName.Version).Trim();
                    int p = sv.IndexOf(" ");
                    if (p > 0) sv = sv.Substring(0, p);
                    p = sv.IndexOf('.');
                    if (p > 0)
                    {
                        p = sv.IndexOf('.', p + 1);
                        if (p > 0)
                            sv = sv.Substring(0, p);
                    }
                    try
                    {
                        float val = 0;
                        float.TryParse(sv, NumberStyles.Float, GCode.format, out val);
                        Main.threeDSettings.openGLVersion = val;
                    }
                    catch
                    {
                        Main.threeDSettings.openGLVersion = 1.1f;
                    }
                    string extensions = GL.GetString(StringName.Extensions);
                    Main.threeDSettings.useVBOs = false;
                    foreach (string s in extensions.Split(' '))
                    {
                        if (s.Equals("GL_ARB_vertex_buffer_object")/* && Main.threeDSettings.openGLVersion>1.49*/)
                        {
                            Main.threeDSettings.useVBOs = true;
                        }
                    }
                    if (Main.threeDSettings.useVBOs)
                        Main.connection.log("Using fast VBOs for rendering is possible", false, 3);
                    else
                        Main.connection.log("Fast VBOs for rendering not supported. Using slower default method.", false, 3);
                    //  Main.threeDSettings.useVBOs = false;
                }
                catch { }
                configureSettings = false;
            }
            loaded = true;
            SetupViewport();
        }
        private Matrix4 GluPickMatrix(float x, float y, float width, float height, int[] viewport)
        {
            Matrix4 result = Matrix4.Identity;
            if ((width <= 0.0f) || (height <= 0.0f))
            {
                return result;
            }

            float translateX = (viewport[2] - (2.0f * (x - viewport[0]))) / width;
            float translateY = (viewport[3] - (2.0f * (y - viewport[1]))) / height;
            result = Matrix4.Mult(Matrix4.CreateTranslation(translateX, translateY, 0.0f), result);
            float scaleX = viewport[2] / width;
            float scaleY = viewport[3] / height;
            result = Matrix4.Mult(Matrix4.Scale(scaleX, scaleY, 1.0f), result);
            return result;
        }
        public uint lastDepth = 0;
        public Geom3DLine pickLine = null; // Last pick up line ray
        public Geom3DLine viewLine = null; // Direction of view
        public Geom3DVector pickPoint = new Geom3DVector(0, 0, 0); // Koordinates of last pick

        public void UpdatePickLine(int x, int y)
        {
            if (view == null) return;
            // Intersection on bottom plane

            int window_y = (Height - y) - Height / 2;
            double norm_y = (double)window_y / (double)(Height / 2);
            int window_x = x - Width / 2;
            double norm_x = (double)window_x / (double)(Width / 2);
            float fpy = (float)(view.nearHeight * 0.5 * norm_y) * (perspectiveModeisParrallel==true ? 4f : 1f);
            float fpx = (float)(view.nearHeight * 0.5 * view.aspectRatio * norm_x) * (perspectiveModeisParrallel == true ? 4f : 1f);


            Vector4 frontPointN = (perspectiveModeisParrallel == true ? new Vector4(fpx, fpy, 0, 1) : new Vector4(0, 0, 0, 1));
            Vector4 dirN = (perspectiveModeisParrallel == true ? new Vector4(0, 0, -view.nearDist, 0) : new Vector4(fpx, fpy, -view.nearDist, 0));
            Matrix4 rotx = Matrix4.CreateFromAxisAngle(new Vector3(1, 0, 0), (float)(view.rotX * Math.PI / 180.0));
            Matrix4 rotz = Matrix4.CreateFromAxisAngle(new Vector3(0, 0, 1), (float)(view.rotZ * Math.PI / 180.0));
            Matrix4 trans = Matrix4.CreateTranslation(-ps.BedLeft - ps.PrintAreaWidth * 0.5f, -ps.BedFront - ps.PrintAreaDepth * 0.5f, -0.5f * ps.PrintAreaHeight);
            Matrix4 ntrans = Matrix4.LookAt(view.userPosition.X, view.userPosition.Y, view.userPosition.Z, view.viewCenter.X, view.viewCenter.Y, view.viewCenter.Z, 0, 0, 1.0f);
            
            ntrans = Matrix4.Mult(rotx, ntrans);
            ntrans = Matrix4.Mult(rotz, ntrans);
            ntrans = Matrix4.Mult(trans, ntrans);
            ntrans = Matrix4.Invert(ntrans);
            Vector4 frontPoint = (perspectiveModeisParrallel == true ? Vector4.Transform(frontPointN, ntrans) : ntrans.Row3);
            //Vector4 frontPoint = (perspectiveModeisParrallel==true ? frontPointN : ntrans.Row3);
            Vector4 dirVec = Vector4.Transform(dirN, ntrans);
            pickLine = new Geom3DLine(new Geom3DVector(frontPoint.X / frontPoint.W, frontPoint.Y / frontPoint.W, frontPoint.Z / frontPoint.W),
                new Geom3DVector(dirVec.X, dirVec.Y, dirVec.Z), true);
            pickLine.dir.normalize();
            /*Geom3DPlane plane = new Geom3DPlane(new Geom3DVector(0, 0, 0), new Geom3DVector(0, 0, 1));
            Geom3DVector cross = new Geom3DVector(0, 0, 0);
            plane.intersectLine(pickLine, cross);
            */
        }
        private ThreeDModel Picktest(int x, int y)
        {
            if (view == null) return null;
            // int x = Mouse.X;
            // int y = Mouse.Y;
            // Console.WriteLine("X:" + x + " Y:" + y);
            gl.MakeCurrent();
            uint[] selectBuffer = new uint[128];
            GL.SelectBuffer(128, selectBuffer);
            GL.RenderMode(RenderingMode.Select);
            SetupViewport();

            GL.MatrixMode(MatrixMode.Projection);
            GL.PushMatrix();
            GL.LoadIdentity();

            int[] viewport = new int[4];
            GL.GetInteger(GetPName.Viewport, viewport);

            Matrix4 m = GluPickMatrix(x, viewport[3] - y, 1, 1, viewport);
            GL.MultMatrix(ref m);


            //GluPerspective(45, 32 / 24, 0.1f, 100.0f);
            //Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView((float)Math.PI / 4, 1, 0.1f, 100.0f);
            GL.MultMatrix(ref view.persp);

            GL.MatrixMode(MatrixMode.Modelview);
            GL.ClearColor(Main.threeDSettings.background.BackColor);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            view.lookAt = Matrix4.LookAt(view.userPosition.X, view.userPosition.Y, view.userPosition.Z, view.viewCenter.X, view.viewCenter.Y, view.viewCenter.Z, 0, 0, 1.0f);

            // Intersection on bottom plane

            int window_y = (viewport[3] - y) - viewport[3] / 2;
            double norm_y = (double)window_y / (double)(viewport[3] / 2);
            int window_x = x - viewport[2] / 2;
            double norm_x = (double)window_x / (double)(viewport[2] / 2);
            float fpy = (float)(view.nearHeight * 0.5 * norm_y) * (perspectiveModeisParrallel==true ? 4f : 1f);
            float fpx = (float)(view.nearHeight * 0.5 * view.aspectRatio * norm_x) * (perspectiveModeisParrallel==true ? 4f : 1f);


            Vector4 frontPointN = (perspectiveModeisParrallel==true ? new Vector4(fpx, fpy, 0, 1) : new Vector4(0, 0, 0, 1));
            Vector4 dirN = (perspectiveModeisParrallel==true ? new Vector4(0, 0, -view.nearDist, 0) : new Vector4(fpx, fpy, -view.nearDist, 0));
            Matrix4 rotx = Matrix4.CreateFromAxisAngle(new Vector3(1, 0, 0), (float)(view.rotX * Math.PI / 180.0));
            Matrix4 rotz = Matrix4.CreateFromAxisAngle(new Vector3(0, 0, 1), (float)(view.rotZ * Math.PI / 180.0));
            Matrix4 trans = Matrix4.CreateTranslation(-ps.BedLeft - ps.PrintAreaWidth * 0.5f, -ps.BedFront - ps.PrintAreaDepth * 0.5f, -0.5f * ps.PrintAreaHeight);
            Matrix4 ntrans = view.lookAt;
            ntrans = Matrix4.Mult(rotx, ntrans);
            ntrans = Matrix4.Mult(rotz, ntrans);
            ntrans = Matrix4.Mult(trans, ntrans);
            ntrans = Matrix4.Invert(ntrans);
            Vector4 frontPoint = (perspectiveModeisParrallel==true ? Vector4.Transform(frontPointN, ntrans) : ntrans.Row3);
            Vector4 dirVec = Vector4.Transform(dirN, ntrans);
            pickLine = new Geom3DLine(new Geom3DVector(frontPoint.X / frontPoint.W, frontPoint.Y / frontPoint.W, frontPoint.Z / frontPoint.W),
                new Geom3DVector(dirVec.X, dirVec.Y, dirVec.Z), true);
            dirN = new Vector4(0, 0, -view.nearDist, 0);
            dirVec = Vector4.Transform(dirN, ntrans);
            viewLine = new Geom3DLine(new Geom3DVector(frontPoint.X / frontPoint.W, frontPoint.Y / frontPoint.W, frontPoint.Z / frontPoint.W),
                new Geom3DVector(dirVec.X, dirVec.Y, dirVec.Z), true);
            viewLine.dir.normalize();
            pickLine.dir.normalize();
            /* Geom3DPlane plane = new Geom3DPlane(new Geom3DVector(0, 0, 0), new Geom3DVector(0, 0, 1));
             Geom3DVector cross = new Geom3DVector(0, 0, 0);
             plane.intersectLine(pickLine, cross);
             Main.conn.log("Linie: " + pickLine, false, 3);
             Main.conn.log("Schnittpunkt: " + cross, false, 3);
             */
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadMatrix(ref view.lookAt);
            GL.Rotate(view.rotX, 1, 0, 0);
            GL.Rotate(view.rotZ, 0, 0, 1);
            GL.Translate(-ps.BedLeft - ps.PrintAreaWidth * 0.5f, -ps.BedFront - ps.PrintAreaDepth * 0.5f, -0.5f * ps.PrintAreaHeight);



            GL.InitNames();
            int pos = 0;
            foreach (ThreeDModel model in view.models)
            {
                GL.PushName(pos++);
                GL.PushMatrix();
                model.AnimationBefore();
                GL.Translate(model.Position.x, model.Position.y, model.Position.z);
                GL.Rotate(model.Rotation.z, Vector3.UnitZ);
                GL.Rotate(model.Rotation.y, Vector3.UnitY);
                GL.Rotate(model.Rotation.x, Vector3.UnitX);
                GL.Scale(model.Scale.x, model.Scale.y, model.Scale.z);
                model.Paint();
                model.AnimationAfter();
                GL.PopMatrix();
                GL.PopName();
            }
            GL.MatrixMode(MatrixMode.Projection);
            GL.PopMatrix();
            GL.MatrixMode(MatrixMode.Modelview);

            int hits = GL.RenderMode(RenderingMode.Render);
            ThreeDModel selected = null;
            if (hits > 0)
            {
                selected = view.models.ElementAt((int)selectBuffer[3]);
                lastDepth = selectBuffer[1];
                for (int i = 1; i < hits; i++)
                {
                    if (selectBuffer[4 * i + 1] < lastDepth)
                    {
                        lastDepth = selectBuffer[i * 4 + 1];
                        selected = view.models.ElementAt((int)selectBuffer[i * 4 + 3]);
                    }
                }
                double dfac = (double)lastDepth / uint.MaxValue;
                dfac = -(view.farDist * view.nearDist) / (dfac * (view.farDist - view.nearDist) - view.farDist);
                Geom3DVector crossPlanePoint = new Geom3DVector(viewLine.dir).scale((float)dfac).add(viewLine.point);
                Geom3DPlane objplane = new Geom3DPlane(crossPlanePoint, viewLine.dir);
                objplane.intersectLine(pickLine, pickPoint);
                //Main.conn.log("Objekttreffer: " + pickPoint, false, 3);

            }
            //PrinterConnection.logInfo("Hits: " + hits);
            return selected;
        }
        private void gl_Resize(object sender, EventArgs e)
        {
            SetupViewport();
            gl.Invalidate();
        }
        Geom3DPlane movePlane = new Geom3DPlane(new Geom3DVector(0, 0, 0), new Geom3DVector(0, 0, 1)); // Plane where object movement occurs
        Geom3DVector moveStart, moveLast, movePos;
        private void gl_MouseDown(object sender, MouseEventArgs e)
        {
            lastX = xDown = e.X;
            lastY = yDown = e.Y;
            startRotX = view.rotX;
            startRotZ = view.rotZ;
            startViewCenter = view.viewCenter;
            startUserPosition = view.userPosition;
            movePlane = new Geom3DPlane(new Geom3DVector(0, 0, 0), new Geom3DVector(0, 0, 1));
            moveStart = moveLast = new Geom3DVector(0, 0, 0);
            UpdatePickLine(e.X, e.Y);
            movePlane.intersectLine(pickLine, moveStart);
            if (e.Button == MouseButtons.Right)
            {
                ThreeDModel sel = Picktest(e.X, e.Y);
                if (sel != null)
                {
                    movePlane = new Geom3DPlane(pickPoint, new Geom3DVector(0, 0, 1));
                    moveStart = moveLast = new Geom3DVector(pickPoint);
                }
                if (sel != null && view.eventObjectMoved != null)
                    view.eventObjectSelected(sel);
                //computeRay();
            }
        }

        private void gl_MouseMove(object sender, MouseEventArgs e)
        {
            double window_y = (gl.Height - e.Y) - gl.Height / 2;
            normY = window_y * 2.0 / (double)(gl.Height);
            double window_x = e.X - gl.Width / 2;
            normX = window_x * 2.0 / (double)(gl.Width);
            if (e.Button == MouseButtons.None)
            {
                speedX = speedY = 0;
                return;
            }
            xPos = e.X;
            yPos = e.Y;
            UpdatePickLine(e.X, e.Y);
            movePos = new Geom3DVector(0, 0, 0);
            movePlane.intersectLine(pickLine, movePos);
            float d = Math.Min(gl.Width, gl.Height) / 3;
            speedX = Math.Max(-1, Math.Min(1, (xPos - xDown) / d));
            speedY = Math.Max(-1, Math.Min(1, (yPos - yDown) / d));
        }

        private void gl_MouseUp(object sender, MouseEventArgs e)
        {
            speedX = speedY = 0;
        }
        private void gl_MouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta != 0)
            {
                view.zoom *= 1f - e.Delta / 2000f;
                if (view.zoom < 0.01) view.zoom = 0.01f;
                if (view.zoom > 5.9) view.zoom = 5.9f;
                //userPosition.Y += e.Delta;
                gl.Invalidate();
            }
        }
        void Application_Idle(object sender, EventArgs e)
        {
            if (!loaded || (speedX == 0 && speedY == 0)) return;
            // no guard needed -- we hooked into the event in Load handler

            sw.Stop(); // we've measured everything since last Idle run
            double milliseconds = sw.Elapsed.TotalMilliseconds;
            sw.Reset(); // reset stopwatch
            sw.Start(); // restart stopwatch
            Keys k = Control.ModifierKeys;
            int emode = mode;
            if (k == Keys.Shift || Control.MouseButtons == MouseButtons.Middle) emode = 2;
            if (k == Keys.Control) emode = 0;
            if (k == Keys.Alt || Control.MouseButtons == MouseButtons.Right) emode = 4;
            if (emode == 0)
            {
                float d = Math.Min(gl.Width, gl.Height) / 3;
                speedX = (xPos - xDown) / d;
                speedY = (yPos - yDown) / d;
                view.rotZ = startRotZ + speedX * 50;
                view.rotX = startRotX + speedY * 50;
                //rotZ += (float)milliseconds * speedX *Math.Abs(speedX)/ 15.0f;
                //rotX += (float)milliseconds * speedY*Math.Abs(speedY) / 15.0f;
                gl.Invalidate();
            }
            else if (emode == 1)
            {
                speedX = (xPos - xDown) / gl.Width;
                speedY = (yPos - yDown) / gl.Height;
                view.userPosition.X = startUserPosition.X + speedX * 200 * view.zoom;
                view.userPosition.Z = startUserPosition.Z - speedY * 200 * view.zoom;
                if (view.rotX == 90 && view.rotZ == 0 && perspectiveModeisParrallel==true)
                {
                    view.viewCenter.X = startViewCenter.X + speedX * 200 * view.zoom;
                    view.viewCenter.Z = startViewCenter.Z - speedY * 200 * view.zoom;
                }

                //userPosition.X += (float)milliseconds * speedX * Math.Abs(speedX) / 10.0f;
                //userPosition.Z -= (float)milliseconds * speedY *Math.Abs(speedY)/ 10.0f;
                gl.Invalidate();
            }
            else if (emode == 2)
            {
                speedX = (xPos - xDown) / gl.Width;
                speedY = (yPos - yDown) / gl.Height;
                view.viewCenter.X = startViewCenter.X - speedX * 200 * view.zoom;
                view.viewCenter.Z = startViewCenter.Z + speedY * 200 * view.zoom;
                if (view.rotX == 90 && view.rotZ == 0 && perspectiveModeisParrallel==true)
                {
                    view.userPosition.X = startUserPosition.X - speedX * 200 * view.zoom;
                    view.userPosition.Z = startUserPosition.Z + speedY * 200 * view.zoom;
                }
                //viewCenter.X -= (float)milliseconds * speedX * Math.Abs(speedX) / 10.0f;
                //viewCenter.Z += (float)milliseconds * speedY * Math.Abs(speedY)/ 10.0f;
                gl.Invalidate();
            }
            else if (emode == 3)
            {
                //userPosition.Y += (float)milliseconds * speedY * Math.Abs(speedY) / 10.0f;
                view.zoom *= (1 - speedY);
                speedY = 0;
                if (view.zoom < 0.01) view.zoom = 0.01f;
                if (view.zoom > 5.9) view.zoom = 5.9f;
                yDown = yPos;
                gl.Invalidate();
            }
            else if (emode == 4)
            {
                Geom3DVector diff = movePos.sub(moveLast);
                moveLast = movePos;
                speedX = (xPos - lastX) * 200 * view.zoom / gl.Width;
                speedY = (yPos - lastY) * 200 * view.zoom / gl.Height;
                if (view.eventObjectMoved != null)
                    view.eventObjectMoved(diff.x, diff.y);
                //               view.eventObjectMoved(speedX, -speedY);
                //eventObjectMoved((float)milliseconds * speedX * Math.Abs(speedX) / 10.0f,
                //   -(float)milliseconds * speedY * Math.Abs(speedY) / 10.0f);
                lastX = xPos;
                lastY = yPos;
                gl.Invalidate();
            }
        }

        public void SetMode(int _mode)
        {
            mode = _mode;

            // Getting rid of these tools, so we don't need to worry if they are check or not. 
            //toolRotate.Checked = mode == 0;
            //toolMove.Checked = mode == 1;
            //toolMoveViewpoint.Checked = mode == 2;
            //toolZoom.Checked = mode == 3;
            //toolMoveObject.Checked = mode == 4;
        }
        private void toolRotate_Click(object sender, EventArgs e)
        {
            SetMode(0);
        }

        private void toolMove_Click(object sender, EventArgs e)
        {
            SetMode(1);
        }

        public void ResetView()
        {
            view.rotX = 20;
            view.rotZ = 0;
            view.zoom = 1.0f;
            //view.viewCenter = new Vector3(0f * ps.PrintAreaWidth, ps.PrintAreaDepth * 0.25f, 0.0f * ps.PrintAreaHeight);
            view.userPosition = new Vector3(0f * ps.PrintAreaWidth, -1.6f * (float)Math.Sqrt(ps.PrintAreaDepth * ps.PrintAreaDepth + ps.PrintAreaWidth * ps.PrintAreaWidth + ps.PrintAreaHeight * ps.PrintAreaHeight), 0.0f * ps.PrintAreaHeight);
            view.viewCenter = new Vector3(0f * ps.PrintAreaWidth, ps.PrintAreaDepth * 0f, 0.0f * ps.PrintAreaHeight);
            //userPosition = new Vector3(0f * ps.PrintAreaWidth, -2f * ps.PrintAreaDepth, 0.0f * ps.PrintAreaHeight);
            gl.Invalidate();
        }

        private void toolMoveViewpoint_Click(object sender, EventArgs e)
        {
            SetMode(2);
        }

        private void toolZoom_Click(object sender, EventArgs e)
        {
            SetMode(3);
        }

        private void toolMoveObject_Click(object sender, EventArgs e)
        {
            SetMode(4);
        }

        private void ThreeDControl_KeyPress(object sender, System.Windows.Forms.KeyPressEventArgs e)
        {
            /*if (e.KeyChar == Keys.Left)
            {
                view.rotZ -= 5;
                gl.Invalidate();
                e.Handled = true;
            }
            if (e.KeyCode == Keys.Right)
            {
                view.rotZ += 5;
                gl.Invalidate();
                e.Handled = true;
            }
            if (e.KeyCode == Keys.Up)
            {
                view.rotX -= 5;
                gl.Invalidate();
                e.Handled = true;
            }
            if (e.KeyCode == Keys.Down)
            {
                view.rotX += 5;
                gl.Invalidate();
                e.Handled = true;
            }*/
            if (e.KeyChar == '-')
            {
                view.zoom *= 1.05f;
                if (view.zoom > 10) view.zoom = 10;
                gl.Invalidate();
                e.Handled = true;
            }
            if (e.KeyChar == '+')
            {
                view.zoom *= 0.95f;
                if (view.zoom < 0.01) view.zoom = 0.01f;
                gl.Invalidate();
                e.Handled = true;
            }
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            Application_Idle(sender, e);
            timeCall++;

            foreach (ThreeDModel m in view.models)
            {
                if (m.Changed || m.hasAnimations)
                {
                   // if ((Main.threeDSettings.drawMethod == 0 && (timeCall % 9) != 0))
                    if ((ThreeDSettings.currentDrawMethod == ThreeDSettings.drawMethod.DrawElements) && ((timeCall % 9) != 0))
                    {
                        return;
                    }
                    //if (m.hasAnimations && Main.threeDSettings.drawMethod != 0)
                    if (m.hasAnimations && (ThreeDSettings.currentDrawMethod != ThreeDSettings.drawMethod.DrawElements))
                    {
                        gl.Invalidate();
                    }
                    else if ((timeCall % 3) == 0)
                    {
                        gl.Invalidate();
                    }

                    return;
                }
            }
        }

        private void toolStripClear_Click(object sender, EventArgs e)
        {
            if (view.editor)
            {
                Main.main.fileAddOrRemove.ButtonRemoveSTL_Click();
                //Main.main.stlComposer1.buttonRemoveSTL_Click(null, null);
            }
            foreach (ThreeDModel m in view.models)
            {
                m.Clear();
            }
            gl.Invalidate();
        }

        private void ThreeDControl_MouseEnter(object sender, EventArgs e)
        {
            // Focus();
        }

        /// <summary>
        /// Resets the position of the camera and the look at fields so that you look at the object from the top. 
        /// </summary>
        public void GoToTopView()
        {
            view.rotX = 90;
            view.rotZ = 0;
            view.zoom = 1.0f;
            view.viewCenter = new Vector3(0f * ps.PrintAreaWidth, ps.PrintAreaDepth * 0.25f, 0.0f * ps.PrintAreaHeight);
            view.userPosition = new Vector3(0f * ps.PrintAreaWidth, -1.6f * (float)Math.Sqrt(ps.PrintAreaDepth * ps.PrintAreaDepth + ps.PrintAreaWidth * ps.PrintAreaWidth + ps.PrintAreaHeight * ps.PrintAreaHeight), 0.0f * ps.PrintAreaHeight);
            view.viewCenter = new Vector3(0f * ps.PrintAreaWidth, ps.PrintAreaDepth * 0f, 0.0f * ps.PrintAreaHeight);
            gl.Invalidate();
        }

        /// <summary>
        /// Resets the position of the camera and the look at fields so that you look at the object from the Front. 
        /// </summary>
        public void GoToFrontView()
        {
            view.rotX = 0;
            view.rotZ = 0;
            view.zoom = 1.0f;
            view.viewCenter = new Vector3(0f * ps.PrintAreaWidth, ps.PrintAreaDepth * 0.25f, 0.0f * ps.PrintAreaHeight);
            view.userPosition = new Vector3(0f * ps.PrintAreaWidth, -1.6f * (float)Math.Sqrt(ps.PrintAreaDepth * ps.PrintAreaDepth + ps.PrintAreaWidth * ps.PrintAreaWidth + ps.PrintAreaHeight * ps.PrintAreaHeight), 0.0f * ps.PrintAreaHeight);
            view.viewCenter = new Vector3(0f * ps.PrintAreaWidth, ps.PrintAreaDepth * 0f, 0.0f * ps.PrintAreaHeight);
            gl.Invalidate();
        }

        /// <summary>
        /// Resets the position of the camera and the look at fields so that you look at the object from the Side. 
        /// </summary>
        public void GoToSideView()
        {
            view.rotX = 0;
            view.rotZ = 90;
            view.zoom = 1.0f;
            view.viewCenter = new Vector3(0f * ps.PrintAreaWidth, ps.PrintAreaDepth * 0.25f, 0.0f * ps.PrintAreaHeight);
            view.userPosition = new Vector3(0f * ps.PrintAreaWidth, -1.6f * (float)Math.Sqrt(ps.PrintAreaDepth * ps.PrintAreaDepth + ps.PrintAreaWidth * ps.PrintAreaWidth + ps.PrintAreaHeight * ps.PrintAreaHeight), 0.0f * ps.PrintAreaHeight);
            view.viewCenter = new Vector3(0f * ps.PrintAreaWidth, ps.PrintAreaDepth * 0f, 0.0f * ps.PrintAreaHeight);
            gl.Invalidate();
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            view.rotX = 90;
            view.rotZ = 0;
            view.zoom = 1.0f;
            view.viewCenter = new Vector3(0f * ps.PrintAreaWidth, ps.PrintAreaDepth * 0.25f, 0.0f * ps.PrintAreaHeight);
            //view.userPosition = new Vector3(0f * ps.PrintAreaWidth, -1.7f *(float)Math.Sqrt(ps.PrintAreaDepth * ps.PrintAreaDepth + ps.PrintAreaWidth * ps.PrintAreaWidth), 0.0f * ps.PrintAreaHeight);
            view.userPosition = new Vector3(0f * ps.PrintAreaWidth, -1.6f * (float)Math.Sqrt(ps.PrintAreaDepth * ps.PrintAreaDepth + ps.PrintAreaWidth * ps.PrintAreaWidth + ps.PrintAreaHeight * ps.PrintAreaHeight), 0.0f * ps.PrintAreaHeight);
            view.viewCenter = new Vector3(0f * ps.PrintAreaWidth, ps.PrintAreaDepth * 0f, 0.0f * ps.PrintAreaHeight);
            //userPosition = new Vector3(0f * ps.PrintAreaWidth, -2f * ps.PrintAreaDepth, 0.0f * ps.PrintAreaHeight);
            gl.Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Right:
                case Keys.Left:
                case Keys.Up:
                case Keys.Down:
                    return true;
                case Keys.Shift | Keys.Right:
                case Keys.Shift | Keys.Left:
                case Keys.Shift | Keys.Up:
                case Keys.Shift | Keys.Down:
                    return true;
            }
            return base.IsInputKey(keyData);
        }
        public void ThreeDControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left)
            {
                view.rotZ -= 5;
                gl.Invalidate();
                e.Handled = true;
            }
            if (e.KeyCode == Keys.Right)
            {
                view.rotZ += 5;
                gl.Invalidate();
                e.Handled = true;
            }
            if (e.KeyCode == Keys.Up)
            {
                view.rotX -= 5;
                gl.Invalidate();
                e.Handled = true;
            }
            if (e.KeyCode == Keys.Down)
            {
                view.rotX += 5;
                gl.Invalidate();
                e.Handled = true;
            }
            if (e.KeyValue == '-')
            {
                view.zoom *= 1.05f;
                if (view.zoom > 10) view.zoom = 10;
                gl.Invalidate();
                e.Handled = true;
            }
            if (e.KeyValue == '+')
            {
                view.zoom *= 0.95f;
                if (view.zoom < 0.01) view.zoom = 0.01f;
                gl.Invalidate();
                e.Handled = true;
            }

            // If the event hasn't been handled, then send it on to the listSTLObjects key down to see if we should delete something. 
            // Basically we are making a way to delele objects. 
            if (Main.main.current3Dview == Main.ThreeDViewOptions.STLeditor && e.Handled == false)
            {  
                Main.main.listSTLObjects_KeyDown(sender, e);
             }

            if (e.Handled == false)
                Main.main.fileAddOrRemove.SaveAFile_KeyDown(sender, e);
        }

        public void ChangeProspectiveMode()
        {
            perspectiveModeisParrallel = !perspectiveModeisParrallel;
            gl.Invalidate();
        }

        private void toolParallelProjection_Click(object sender, EventArgs e)
        {
            perspectiveModeisParrallel = !perspectiveModeisParrallel;
            gl.Invalidate();
        }


        /// <summary>
        /// Not working right now. I was trying to make a function that would center the view directly on the object.   
        /// </summary>
        internal void CenterViewOnObjects()
        {
             
            float xCenter=0f * ps.PrintAreaWidth;
            float yCenter =ps.PrintAreaDepth * 0f;
            float zCenter = 0.0f * ps.PrintAreaHeight;
            // view.viewCenter = new Vector3(, , );
           
            foreach (STL tempSTL in Main.main.listSTLObjects.Items)
            {
                xCenter += (tempSTL.xMax + tempSTL.xMin);
                yCenter += (tempSTL.yMax + tempSTL.yMin);
                zCenter += (tempSTL.zMax + tempSTL.zMin);
            }

            view.viewCenter = new Vector3(xCenter, yCenter, zCenter);
            gl.Invalidate();
         }
    }
}
