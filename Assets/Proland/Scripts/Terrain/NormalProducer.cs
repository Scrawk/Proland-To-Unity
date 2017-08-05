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
using System.Collections.Generic;

using Common.Mathematics.LinearAlgebra;
using Common.Unity.Mathematics;

namespace Proland
{

	public class NormalProducer : TileProducer
	{

        public override int Border { get { return 2; } }

        [SerializeField]
		private ElevationProducer m_elevationProducer;

		[SerializeField]
        private Material m_normalsMat;

        private NormalUniforms m_uniforms;

		protected override void Start () 
		{
			base.Start();

            m_uniforms = new NormalUniforms();

			int tileSize = Cache.GetStorage(0).TileSize;
			int elevationTileSize = m_elevationProducer.Cache.GetStorage(0).TileSize;

			if(tileSize != elevationTileSize)
				throw new InvalidParameterException("Tile size must equal elevation tile size");

			if(Border != m_elevationProducer.Border)
				throw new InvalidParameterException("Border size must be equal to elevation border size");

			GPUTileStorage storage = Cache.GetStorage(0) as GPUTileStorage;
			
			if(storage == null)
				throw new InvalidStorageException("Storage must be a GPUTileStorage");

		}

		public override bool HasTile(int level, int tx, int ty)
        {
			return m_elevationProducer.HasTile(level, tx, ty);
		}

		public override void DoCreateTile(int level, int tx, int ty, List<Slot> slot)
		{
			GPUSlot gpuSlot = slot[0] as GPUSlot;

			Tile elevationTile = m_elevationProducer.FindTile(level, tx, ty, false, true);
			GPUSlot elevationGpuSlot = null;

			if(elevationTile != null)
				elevationGpuSlot = elevationTile.GetSlot(0) as GPUSlot;
			else
				throw new MissingTileException("Find elevation tile failed");

			int tileWidth = gpuSlot.Owner.TileSize;

			m_normalsMat.SetVector(m_uniforms.tileSD, new Vector2(tileWidth, (tileWidth-1.0f) / (World.GridResolution-1.0f)) );

			RenderTexture elevationTex = elevationGpuSlot.Texture;

			m_normalsMat.SetTexture(m_uniforms.elevationSampler, elevationTex);

			Vector4 elevationOSL = new Vector4(0.25f/elevationTex.width, 0.25f/elevationTex.height, 1.0f/elevationTex.width, 0.0f);

			m_normalsMat.SetVector(m_uniforms.elevationOSL, elevationOSL);

			if(World.IsDeformed) 
			{
				double D = TerrainNode.Root.Length;
				double R = D / 2.0;
                double len = 1 << level;

                double x0 = tx / len * D - R;
				double x1 = (tx + 1) / len * D - R;
				double y0 = ty / len * D - R;
				double y1 = (ty + 1) /len * D - R;

				Vector3d p0 = new Vector3d(x0, y0, R);
				Vector3d p1 = new Vector3d(x1, y0, R);
				Vector3d p2 = new Vector3d(x0, y1, R);
				Vector3d p3 = new Vector3d(x1, y1, R);
				Vector3d pc = new Vector3d((x0 + x1) * 0.5, (y0 + y1) * 0.5, R);

                double l0 = p0.Magnitude;
                double l1 = p1.Magnitude;
                double l2 = p2.Magnitude;
                double l3 = p3.Magnitude;
                Vector3d v0 = p0.Normalized;
                Vector3d v1 = p1.Normalized;
                Vector3d v2 = p2.Normalized;
                Vector3d v3 = p3.Normalized;

				Vector3d vc = (v0 + v1 + v2 + v3) * 0.25;
				
				Matrix4x4d deformedCorners = new Matrix4x4d(
					v0.x * R - vc.x * R, v1.x * R - vc.x * R, v2.x * R - vc.x * R, v3.x * R - vc.x * R,
					v0.y * R - vc.y * R, v1.y * R - vc.y * R, v2.y * R - vc.y * R, v3.y * R - vc.y * R,
					v0.z * R - vc.z * R, v1.z * R - vc.z * R, v2.z * R - vc.z * R, v3.z * R - vc.z * R,
					1.0, 1.0, 1.0, 1.0);
				
				Matrix4x4d deformedVerticals = new Matrix4x4d(
					v0.x, v1.x, v2.x, v3.x,
					v0.y, v1.y, v2.y, v3.y,
					v0.z, v1.z, v2.z, v3.z,
					0.0, 0.0, 0.0, 0.0);
				
				Vector3d uz = pc.Normalized;
				Vector3d ux = (new Vector3d(0,1,0)).Cross(uz).Normalized;
				Vector3d uy = uz.Cross(ux);

				Matrix4x4d worldToTangentFrame = new Matrix4x4d(
					ux.x, ux.y, ux.z, 0.0,
					uy.x, uy.y, uy.z, 0.0,
					uz.x, uz.y, uz.z, 0.0,
					0.0, 0.0, 0.0, 0.0);

				m_normalsMat.SetMatrix(m_uniforms.patchCorners, MathConverter.ToMatrix4x4(deformedCorners));
				m_normalsMat.SetMatrix(m_uniforms.patchVerticals, MathConverter.ToMatrix4x4(deformedVerticals));
				m_normalsMat.SetVector(m_uniforms.patchCornerNorms, new Vector4((float)l0, (float)l1, (float)l2, (float)l3));
				m_normalsMat.SetVector(m_uniforms.deform, new Vector4((float)x0, (float)y0, (float)(D / len), (float)R));
				m_normalsMat.SetMatrix(m_uniforms.worldToTangentFrame, MathConverter.ToMatrix4x4(worldToTangentFrame));
			} 
			else 
			{
				double D = TerrainNode.Root.Length;
				double R = D / 2.0;
                double len = 1 << level;

                double x0 = tx / len * D - R;
				double y0 = ty / len * D - R;

				m_normalsMat.SetMatrix(m_uniforms.worldToTangentFrame, Matrix4x4.identity);
				m_normalsMat.SetVector(m_uniforms.deform, new Vector4((float)x0, (float)y0, (float)(D / len), 0.0f));

			}

			Graphics.Blit(null, gpuSlot.Texture, m_normalsMat);

			base.DoCreateTile(level, tx, ty, slot);
		}
		
	}

    public class NormalUniforms
    {
        public int tileSD, elevationSampler, elevationOSL;
        public int patchCorners, patchVerticals, patchCornerNorms;
        public int deform, worldToTangentFrame;

        public NormalUniforms()
        {
            tileSD = Shader.PropertyToID("_TileSD");
            elevationSampler = Shader.PropertyToID("_ElevationSampler");
            elevationOSL = Shader.PropertyToID("_ElevationOSL");
            patchCorners = Shader.PropertyToID("_PatchCorners");
            patchVerticals = Shader.PropertyToID("_PatchVerticals");
            patchCornerNorms = Shader.PropertyToID("_PatchCornerNorms");
            deform = Shader.PropertyToID("_Deform");
            worldToTangentFrame = Shader.PropertyToID("_WorldToTangentFrame");
        }
    }
}








































