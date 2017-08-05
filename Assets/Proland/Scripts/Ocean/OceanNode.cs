/*
 * Proland: a procedural landscape rendering library.
 * Copyright (c) 2008-2011 INRIA
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 *
 * Proland is distributed under a dual-license scheme.
 * You can obtain a specific license from Inria: proland-licensing@inria.fr.
 *
 * Authors: Eric Bruneton, Antoine Begault, Guillaume Piolat.
 * 
 */

using UnityEngine;
using System.Collections;

using Common.Mathematics.LinearAlgebra;
using Common.Unity.Mathematics;

namespace Proland
{
	
	/// <summary>
	/// An AbstractTask to draw a flat or spherical ocean.
	/// This class provides the functions and data to draw a flat projected grid but nothing else.
	/// </summary>
	public abstract class OceanNode : Node 
	{
        /// <summary>
        /// Concrete classes must provide a function that returns the
        /// variance of the waves need for the BRDF rendering of waves
        /// </summary>
        public abstract float MaxSlopeVariance { get; }

        /// <summary>
        /// If the ocean should be draw. To minimize depth fighting the ocean is not draw when the camera is far away. 
        /// Instead the terrain shader should render the ocean areas directly on the terrain
        /// </summary>
        public bool DrawOcean { get; private set; }

		[SerializeField]
		protected Material m_oceanMaterial;

		[SerializeField]
		protected Color m_oceanUpwellingColor = new Color(0.039f, 0.156f, 0.47f);

		/// <summary>
        /// Sea level in meters.
		/// </summary>
		[SerializeField]
		protected float m_oceanLevel = 5.0f;

		/// <summary>
        /// The maximum altitude at which the ocean must be displayed.
		/// </summary>
		[SerializeField]
		protected float m_zmin = 20000.0f;

		/// <summary>
        /// Size of each grid in the projected grid. (number of pixels on screen)
		/// </summary>
		[SerializeField]
		protected int m_resolution = 4;
		
		private Mesh[] m_screenGrids;
        private Matrix4x4d m_oldLtoo;
        private Vector3d m_offset;
		
		protected override void Start () 
		{
			base.Start();

			World.SkyNode.InitUniforms(m_oceanMaterial);

			m_oldLtoo = Matrix4x4d.Identity;
			m_offset = Vector3d.Zero;

			//Create the projected grid. The resolution is the size in pixels
			//of each square in the grid. If the squares are small the size of
			//the mesh will exceed the max verts for a mesh in Unity. In this case 
			//split the mesh up into smaller meshes.

			m_resolution = Mathf.Max(1, m_resolution);
			//The number of squares in the grid on the x and y axis
			int NX = Screen.width / m_resolution;
			int NY = Screen.height / m_resolution;
			int numGrids = 1;

			const int MAX_VERTS = 65000;
			//The number of meshes need to make a grid of this resolution
			if(NX*NY > MAX_VERTS) {
				numGrids += (NX*NY) / MAX_VERTS;
			}

			m_screenGrids = new Mesh[numGrids];
			//Make the meshes. The end product will be a grid of verts that cover 
			//the screen on the x and y axis with the z depth at 0. This grid is then
			//projected as the ocean by the shader
			for(int i = 0; i < numGrids; i++)
			{
				NY = Screen.height / numGrids / m_resolution;
		
				m_screenGrids[i] = MakePlane(NX, NY, (float)i / (float)numGrids, 1.0f / (float)numGrids);
				m_screenGrids[i].bounds = new Bounds(Vector3.zero, new Vector3(1e8f, 1e8f, 1e8f));
			}
			
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
		}

        private Mesh MakePlane(int w, int h, float offset, float scale) 
		{
			
			Vector3[] vertices = new Vector3[w*h];
			Vector2[] texcoords = new Vector2[w*h];
			Vector3[] normals = new Vector3[w*h];
			int[] indices = new int[w*h*6];
			
			for(int x = 0; x < w; x++)
			{
				for(int y = 0; y < h; y++)
				{
					Vector2 uv = new Vector3((float)x / (float)(w-1), (float)y / (float)(h-1));

					uv.y *= scale;
					uv.y += offset;

					Vector2 p = new Vector2();
					p.x = (uv.x-0.5f)*2.0f;
					p.y = (uv.y-0.5f)*2.0f;

					Vector3 pos = new Vector3(p.x, p.y, 0.0f);
					Vector3 norm = new Vector3(0.0f, 0.0f, 1.0f);

					texcoords[x+y*w] = uv;
					vertices[x+y*w] = pos;
					normals[x+y*w] = norm;
				}
			}
		
			int num = 0;
			for(int x = 0; x < w-1; x++)
			{
				for(int y = 0; y < h-1; y++)
				{
					indices[num++] = x + y * w;
					indices[num++] = x + (y+1) * w;
					indices[num++] = (x+1) + y * w;
					
					indices[num++] = x + (y+1) * w;
					indices[num++] = (x+1) + (y+1) * w;
					indices[num++] = (x+1) + y * w;
				}
			}
			
			Mesh mesh = new Mesh();
			
			mesh.vertices = vertices;
			mesh.uv = texcoords;
			mesh.triangles = indices;
			mesh.normals = normals;
			
			return mesh;
		}

		public virtual void UpdateNode() 
		{
			//Calculates the required data for the projected grid

			// compute ltoo = localToOcean transform, where ocean frame = tangent space at
			// camera projection on sphere radius in local space
			Matrix4x4d ctol = View.CameraToWorld;
			Vector3d cl = ctol * Vector3d.Zero; // camera in local space

			float radius = World.IsDeformed ? World.Radius : 0.0f;

			if ((radius == 0.0 && cl.z > m_zmin) ||
			    (radius > 0.0 && cl.Magnitude > radius + m_zmin) ||
			    (radius < 0.0 && (new Vector2d(cl.y, cl.z)).Magnitude < -radius - m_zmin))
			{
				m_oldLtoo = Matrix4x4d.Identity;
				m_offset = Vector3d.Zero;
				DrawOcean = false;
				return;
			}

			DrawOcean = true;
			Vector3d ux, uy, uz, oo;

			if(radius == 0.0f)
			{
				//terrain ocean
				ux = Vector3d.UnitX;
				uy = Vector3d.UnitY;
				uz = Vector3d.UnitZ;
				oo = new Vector3d(cl.x, cl.y, 0.0);
			}
			else
			{
				// planet ocean
				uz = cl.Normalized; // unit z vector of ocean frame, in local space
				if (m_oldLtoo != Matrix4x4d.Identity) {
					ux = (new Vector3d(m_oldLtoo[1,0], m_oldLtoo[1,1], m_oldLtoo[1,2])).Cross(uz).Normalized;
				} 
				else {
					ux = Vector3d.UnitZ.Cross(uz).Normalized;
				}
				uy = uz.Cross(ux); // unit y vector
				oo = uz * radius; // origin of ocean frame, in local space
			}
			
			Matrix4x4d ltoo = new Matrix4x4d(
				ux.x, ux.y, ux.z, -Vector3d.Dot(ux, oo),
				uy.x, uy.y, uy.z, -Vector3d.Dot(uy, oo),
				uz.x, uz.y, uz.z, -Vector3d.Dot(uz, oo),
				0.0,  0.0,  0.0,  1.0);
			
			// compute ctoo = cameraToOcean transform
			Matrix4x4d ctoo = ltoo * ctol;

			if (m_oldLtoo != Matrix4x4d.Identity) 
			{
				Vector3d delta = ltoo * (m_oldLtoo.Inverse * Vector3d.Zero);
				m_offset += delta;
			}

			m_oldLtoo = ltoo;
			
			Matrix4x4d stoc = View.ScreenToCamera;
			Vector3d oc = ctoo * Vector3d.Zero;
			
			double h = oc.z;

			Vector4d stoc_w = (stoc * Vector4d.UnitW).xyz0;
			Vector4d stoc_x = (stoc * Vector4d.UnitX).xyz0;
			Vector4d stoc_y = (stoc * Vector4d.UnitY).xyz0;
			
			Vector3d A0 = (ctoo * stoc_w).xyz;
			Vector3d dA = (ctoo * stoc_x).xyz;
			Vector3d B =  (ctoo * stoc_y).xyz;

			Vector3d horizon1, horizon2;
			Vector3d offset = new Vector3d(-m_offset.x, -m_offset.y, oc.z);

			if (radius == 0.0) 
			{
				//Terrain ocean
				horizon1 = new Vector3d(-(h * 1e-6 + A0.z) / B.z, -dA.z / B.z, 0.0);
				horizon2 = Vector3d.Zero;
			} 
			else 
			{
				//Planet ocean
				double h1 = h * (h + 2.0 * radius);
				double h2 = (h + radius) * (h + radius);
				double alpha = Vector3d.Dot(B, B) * h1 - B.z * B.z * h2;
				double beta0 = (Vector3d.Dot(A0, B) * h1 - B.z * A0.z * h2) / alpha;
				double beta1 = (Vector3d.Dot(dA, B) * h1 - B.z * dA.z * h2) / alpha;
				double gamma0 = (Vector3d.Dot(A0, A0) * h1 - A0.z * A0.z * h2) / alpha;
				double gamma1 = (Vector3d.Dot(A0, dA) * h1 - A0.z * dA.z * h2) / alpha;
				double gamma2 = (Vector3d.Dot(dA, dA) * h1 - dA.z * dA.z * h2) / alpha;

				horizon1 = new Vector3d(-beta0, -beta1, 0.0);
				horizon2 = new Vector3d(beta0 * beta0 - gamma0, 2.0 * (beta0 * beta1 - gamma1), beta1 * beta1 - gamma2);
			}

            Vector3 dir = World.SunNode.Direction;

            Vector3d sunDir = new Vector3d(dir.x, dir.y, dir.z);
			Vector3d oceanSunDir = ltoo.ToMatrix3x3d() * sunDir;

			m_oceanMaterial.SetVector("_Ocean_SunDir", MathConverter.ToVector3(oceanSunDir));
			m_oceanMaterial.SetVector("_Ocean_Horizon1", MathConverter.ToVector3(horizon1));
			m_oceanMaterial.SetVector("_Ocean_Horizon2", MathConverter.ToVector3(horizon2));
			m_oceanMaterial.SetMatrix("_Ocean_CameraToOcean", MathConverter.ToMatrix4x4(ctoo));
			m_oceanMaterial.SetMatrix("_Ocean_OceanToCamera", MathConverter.ToMatrix4x4(ctoo.Inverse));
			m_oceanMaterial.SetVector("_Ocean_CameraPos", MathConverter.ToVector3(offset));
			m_oceanMaterial.SetVector("_Ocean_Color", m_oceanUpwellingColor * 0.1f);
			m_oceanMaterial.SetVector("_Ocean_ScreenGridSize", new Vector2((float)m_resolution / (float)Screen.width, (float)m_resolution / (float)Screen.height));
			m_oceanMaterial.SetFloat("_Ocean_Radius", radius);

			World.SkyNode.SetUniforms(m_oceanMaterial);
			World.SunNode.SetUniforms(m_oceanMaterial);
			World.SetUniforms(m_oceanMaterial);

			//Draw each mesh that makes up the projected grid
			foreach( Mesh mesh in m_screenGrids)
				Graphics.DrawMesh(mesh, Matrix4x4.identity, m_oceanMaterial, 0, Camera.main);
			
		}

		public void SetUniforms(Material mat)
		{
			//Sets uniforms that this or other gameobjects may need
			if(mat == null) return;

			mat.SetFloat("_Ocean_Sigma", MaxSlopeVariance);
			mat.SetVector("_Ocean_Color", m_oceanUpwellingColor * 0.1f);
			mat.SetFloat("_Ocean_DrawBRDF", (DrawOcean) ? 0.0f : 1.0f);
			mat.SetFloat("_Ocean_Level", m_oceanLevel);
		}
		
	}
	
}


















