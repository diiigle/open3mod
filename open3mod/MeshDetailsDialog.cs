﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Assimp;

namespace open3mod
{
    public partial class MeshDetailsDialog : Form
    {
        private Mesh _mesh;
        private MainWindow _host;

        public MeshDetailsDialog()
        {
            InitializeComponent();
        }


        public void SetMesh(MainWindow host, Mesh mesh, string meshName) 
        {
            Debug.Assert(mesh != null);
            Debug.Assert(host != null);
            Debug.Assert(meshName != null);

            _mesh = mesh;
            _host = host;

            labelVertexCount.Text = mesh.VertexCount + " Vertices";
            labelFaceCount.Text = mesh.FaceCount + " Faces";
            Text = meshName + " - Details";

            checkedListBoxPerFace.CheckOnClick = false;
            checkedListBoxPerFace.SetItemCheckState(0,
                mesh.PrimitiveType.HasFlag(PrimitiveType.Triangle)
                ? CheckState.Checked
                : CheckState.Unchecked);

            checkedListBoxPerFace.SetItemCheckState(1,
                mesh.PrimitiveType.HasFlag(PrimitiveType.Line)
                ? CheckState.Checked
                : CheckState.Unchecked);

            checkedListBoxPerFace.SetItemCheckState(2,
                mesh.PrimitiveType.HasFlag(PrimitiveType.Point)
                ? CheckState.Checked
                : CheckState.Unchecked);

            checkedListBoxPerVertex.CheckOnClick = false;
            checkedListBoxPerVertex.SetItemCheckState(0, CheckState.Checked);
            checkedListBoxPerVertex.SetItemCheckState(1, mesh.HasNormals
                ? CheckState.Checked
                : CheckState.Unchecked);
            checkedListBoxPerVertex.SetItemCheckState(2, mesh.HasTangentBasis
                ? CheckState.Checked
                : CheckState.Unchecked);

            Debug.Assert(mesh.TextureCoordinateChannels.Length >= 4);
            for (var i = 0; i < 4; ++i)
            {
                checkedListBoxPerVertex.SetItemCheckState(3 + i, mesh.HasTextureCoords(i)
                    ? CheckState.Checked
                    : CheckState.Unchecked);
            }

            Debug.Assert(mesh.VertexColorChannels.Length >= 4);
            for (var i = 0; i < 4; ++i)
            {
                checkedListBoxPerVertex.SetItemCheckState(7 + i, mesh.HasVertexColors(i)
                    ? CheckState.Checked
                    : CheckState.Unchecked);
            }

            checkedListBoxPerVertex.SetItemCheckState(11, mesh.HasBones
                ? CheckState.Checked
                : CheckState.Unchecked);
        }


        private void OnJumpToMaterial(object sender, LinkLabelLinkClickedEventArgs e)
        {
            linkLabel1.LinkVisited = false;
            Debug.Assert(_mesh != null);
            Debug.Assert(_host != null);
            
            // this need not be the currently selected tab
            foreach(var tab in _host.UiState.TabsWithActiveScenes())
            {
                var scene = tab.ActiveScene;
                Debug.Assert(scene != null);

                for(var i = 0; i < scene.Raw.MeshCount; ++i)
                {
                    var m = scene.Raw.Meshes[i];
                    if (m == _mesh)
                    {
                        var mat = scene.Raw.Materials[m.MaterialIndex];
                        var ui = _host.UiForTab(tab);
                        Debug.Assert(ui != null);

                        var inspector = ui.GetInspector();
                        inspector.Materials.SelectEntry(mat);
                        inspector.Materials.EnsureVisible(mat);
                        inspector.OpenMaterialsTab();
                    }
                }
            }
        }
    }
}
