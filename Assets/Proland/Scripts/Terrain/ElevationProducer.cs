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

using Common.Unity.Drawing;

namespace Proland
{

    /// <summary>
	/// Creates the elevations data for the terrain from perlin noise
	/// The noise amps set in the noise Amps array are the amp of the noise for that level of the terrain quad.
	/// If the amp is a negative number the upsample shader will apply the noise every where,
	/// if it is a positive number the noise will only be applied to steep areas and if the amp
	/// is 0 then the elevations will be upsampled but have no new noise applied
    /// </summary>
	public class ElevationProducer : TileProducer
	{

        /// <summary>
        /// The tiles border.
        /// </summary>
        public override int Border { get { return 2; } }

        [SerializeField]
		private ResidualProducer m_residualProducer;

		/// <summary>
        /// The Program to perform the upsampling and add procedure on GPU.
		/// </summary>
		[SerializeField]
        private Material m_upsampleMat;

        /// <summary>
        /// The seed for the noise.
        /// </summary>
		[SerializeField]
        private int m_seed = 0;

     	//The amplitude of the noise to be added for each level (one amplitude per level).
		//example of planet amps
		[SerializeField]
        private float[] m_noiseAmp = new float[] { -3250.0f, -1590.0f, -1125.0f, -795.0f, -561.0f, -397.0f, -140.0f, -100.0f, 15.0f, 8.0f, 5.0f, 2.5f, 1.5f, 1.0f, 0.5f, 0.25f, 0.1f, 0.05f };

        private ElevationUniforms m_uniforms;

        private PerlinNoise m_noise;

        private RenderTexture[] m_noiseTextures;

        private RenderTexture m_residualTex;

        private ComputeBuffer m_residualBuffer;

		protected override void Start () 
		{
			base.Start();

			int tileSize = Cache.GetStorage(0).TileSize;

			if((tileSize - Border*2 - 1) % (World.GridResolution-1) != 0)
				throw new InvalidParameterException("Tile size - border*2 - 1 must be divisible by grid mesh resolution - 1");

			if(m_residualProducer != null && m_residualProducer.GetTileSize(0) != tileSize)
				throw new InvalidParameterException("Residual tile size must match elevation tile size");
			
			GPUTileStorage storage = Cache.GetStorage(0) as GPUTileStorage;
			
			if(storage == null)
				throw new InvalidStorageException("Storage must be a GPUTileStorage");
			
			if(storage.FilterMode != FilterMode.Point)
				throw new InvalidParameterException("GPUTileStorage filter must be point. There will be seams in the terrain otherwise");

			if(m_residualProducer != null)
			{
				m_residualTex = new RenderTexture(tileSize, tileSize, 0, RenderTextureFormat.RFloat);
				m_residualTex.wrapMode = TextureWrapMode.Clamp;
				m_residualTex.filterMode = FilterMode.Point;
				m_residualTex.enableRandomWrite = true;
				m_residualTex.Create();

				m_residualBuffer = new ComputeBuffer(tileSize*tileSize, sizeof(float));
			}

			m_uniforms = new ElevationUniforms();
			m_noise = new PerlinNoise(m_seed);

			CreateDemNoise();
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();

			for(int i = 0; i < 6; i++)
				m_noiseTextures[i].Release();

			if(m_residualTex != null) m_residualTex.Release();
			if(m_residualBuffer != null) m_residualBuffer.Release();
		}

		/// <summary>
		/// This function creates the elevations data and is called by the CreateTileTask when the task is run by the schedular
		/// The functions needs the tiles parent data to have already been created. If it has not the program will abort.
		/// </summary>
		public override void DoCreateTile(int level, int tx, int ty, List<Slot> slot)
		{

			GPUSlot gpuSlot = slot[0] as GPUSlot;

			int tileWidth = gpuSlot.Owner.TileSize;
            int b = Border * 2 + 1;
			int tileSize = tileWidth - b;

			GPUSlot parentGpuSlot = null;
			Tile parentTile = null;

			if (level > 0) 
			{	
				parentTile = FindTile(level - 1, tx / 2, ty / 2, false, true);

				if(parentTile != null)
					parentGpuSlot = parentTile.GetSlot(0) as GPUSlot;
				else 
					throw new MissingTileException("Find parent tile failed");
			}

			float rootQuadSize = (float)TerrainNode.Root.Length;

			Vector4 tileWSD = new Vector4();
			tileWSD.x = tileWidth;
			tileWSD.y = rootQuadSize / (1 << level) / tileSize;
			tileWSD.z = (tileWidth - b) / (World.GridResolution - 1.0f);
			tileWSD.w = 0.0f;

			m_upsampleMat.SetVector(m_uniforms.tileWSD, tileWSD);

			if (level > 0) 
			{
				RenderTexture tex = parentGpuSlot.Texture;

				m_upsampleMat.SetTexture(m_uniforms.coarseLevelSampler, tex);

				float dx = (tx % 2) * (tileSize / 2);
				float dy = (ty % 2) * (tileSize / 2);

				Vector4 coarseLevelOSL = new Vector4(dx / tex.width, dy / tex.height, 1.0f / tex.width, 0.0f);

				m_upsampleMat.SetVector(m_uniforms.coarseLevelOSL, coarseLevelOSL);
			} 
			else
            {
				m_upsampleMat.SetVector(m_uniforms.coarseLevelOSL,  new Vector4(-1.0f, -1.0f, -1.0f, -1.0f));
			}

			if (m_residualProducer != null && m_residualProducer.HasTile(level, tx, ty)) 
			{
				Tile residualTile = m_residualProducer.FindTile(level, tx, ty, false, true);
				CPUSlot<float> residualSlot = null;
				
				if(residualTile != null)
					residualSlot = residualTile.GetSlot(0) as CPUSlot<float>;
				else 
					throw new MissingTileException("Find residual tile failed");

				//Must clear residual tex before use or terrain will have artifacts at the seams. Not sure why.
				RTUtility.ClearColor(m_residualTex, Color.clear);

				m_residualBuffer.SetData(residualSlot.Data);
				CBUtility.WriteIntoRenderTexture(m_residualTex, 1, m_residualBuffer, World.WriteData);

				m_upsampleMat.SetTexture(m_uniforms.residualSampler, m_residualTex);
				m_upsampleMat.SetVector(m_uniforms.residualOSH, new Vector4(0.25f / tileWidth, 0.25f / tileWidth, 2.0f / tileWidth, 1.0f));
				
			} 
			else 
			{
				m_upsampleMat.SetTexture(m_uniforms.residualSampler, null);
				m_upsampleMat.SetVector(m_uniforms.residualOSH, new Vector4(0.0f, 0.0f, 1.0f, 0.0f));
			}

			float rs = level < m_noiseAmp.Length ? m_noiseAmp[level] : 0.0f;

			int noiseL = 0;
			int face = TerrainNode.Face;

			if( rs != 0.0f)
			{
				if (face == 1) 
				{
					int offset = 1 << level;
					int bottomB = m_noise.Noise2D(tx + 0.5f, ty + offset) > 0.0f ? 1 : 0;
					int rightB = (tx == offset - 1 ? m_noise.Noise2D(ty + offset + 0.5f, offset) : m_noise.Noise2D(tx + 1.0f, ty + offset + 0.5f)) > 0.0f ? 2 : 0;
					int topB = (ty == offset - 1 ? m_noise.Noise2D((3.0f * offset - 1.0f - tx) + 0.5f, offset) : m_noise.Noise2D(tx + 0.5f, ty + offset + 1.0f)) > 0.0f ? 4 : 0;
					int leftB = (tx == 0 ? m_noise.Noise2D((4.0f * offset - 1.0f - ty) + 0.5f, offset) : m_noise.Noise2D(tx, ty + offset + 0.5f)) > 0.0f ? 8 : 0;
					noiseL = bottomB + rightB + topB + leftB;
				} 
				else if(face == 6) 
				{
					int offset = 1 << level;
					int bottomB = (ty == 0 ? m_noise.Noise2D((3.0f * offset - 1.0f - tx) + 0.5f, 0) : m_noise.Noise2D(tx + 0.5f, ty - offset)) > 0.0f ? 1 : 0;
					int rightB = (tx == offset - 1.0f ? m_noise.Noise2D((2.0f * offset - 1.0f - ty) + 0.5f, 0) : m_noise.Noise2D(tx + 1.0f, ty - offset + 0.5f)) > 0.0f ? 2 : 0;
					int topB = m_noise.Noise2D(tx + 0.5f, ty - offset + 1.0f) > 0.0f ? 4 : 0;
					int leftB = (tx == 0 ? m_noise.Noise2D(3.0f * offset + ty + 0.5f, 0) : m_noise.Noise2D(tx, ty - offset + 0.5f)) > 0.0f ? 8 : 0;
					noiseL = bottomB + rightB + topB + leftB;
				} 
				else 
				{
					int offset = (1 << level) * (face - 2);
					int bottomB = m_noise.Noise2D(tx + offset + 0.5f, ty) > 0.0f ? 1 : 0;
					int rightB = m_noise.Noise2D((tx + offset + 1) % (4 << level), ty + 0.5f) > 0.0f ? 2 : 0;
					int topB = m_noise.Noise2D(tx + offset + 0.5f, ty + 1.0f) > 0.0f ? 4 : 0;
					int leftB = m_noise.Noise2D(tx + offset, ty + 0.5f) > 0.0f ? 8 : 0;
					noiseL = bottomB + rightB + topB + leftB;
				}
			}

			int[] noiseRs = new int[]{ 0, 0, 1, 0, 2, 0, 1, 0, 3, 3, 1, 3, 2, 2, 1, 0 };
			int noiseR = noiseRs[noiseL];

			int[] noiseLs = new int[]{ 0, 1, 1, 2, 1, 3, 2, 4, 1, 2, 3, 4, 2, 4, 4, 5 };
			noiseL = noiseLs[noiseL];

			m_upsampleMat.SetTexture(m_uniforms.noiseSampler, m_noiseTextures[noiseL]);
			m_upsampleMat.SetVector(m_uniforms.noiseUVLH, new Vector4(noiseR, (noiseR + 1) % 4, 0, rs));

			Graphics.Blit(null, gpuSlot.Texture, m_upsampleMat);

			base.DoCreateTile(level, tx, ty, slot);

		}

        private float Rand()
        {
			return Random.value * 2.0f - 1.0f;
		}

		/// <summary>
		/// Creates a series of textures that contain random noise.
		/// These texture tile together using the Wang Tiling method.
		/// Used by the UpSample shader to create fractal noise for the terrain elevations.
		/// </summary>
		private void CreateDemNoise()
		{
			int tileWidth = Cache.GetStorage(0).TileSize;
			m_noiseTextures = new RenderTexture[6];
	
			int[] layers = new int[]{0, 1, 3, 5, 7, 15};
			int rand = 1234567;

			for (int nl = 0; nl < 6; ++nl) 
			{
				float[] noiseArray = new float[tileWidth * tileWidth];
				int l = layers[nl];

				ComputeBuffer buffer = new ComputeBuffer(tileWidth*tileWidth, sizeof(float));

				// corners
				for (int j = 0; j < tileWidth; ++j) {
					for (int i = 0; i < tileWidth; ++i) {
						noiseArray[i+j*tileWidth] = 0.0f;
					}
				}

				// bottom border
				Random.InitState((l & 1) == 0 ? 7654321 : 5647381);
				for (int h = 5; h <= tileWidth / 2; ++h) {
					float N = Rand();
					noiseArray[h+2*tileWidth] = N;
					noiseArray[(tileWidth-1-h)+2*tileWidth] = N;
				}

				for (int v = 3; v < 5; ++v) {
					for (int h = 5; h < tileWidth - 5; ++h) {
						float N = Rand();
						noiseArray[h+v*tileWidth] = N;
						noiseArray[(tileWidth-1-h)+(4-v)*tileWidth] = N;
					}
				}

				// right border
				Random.InitState((l & 2) == 0 ? 7654321 : 5647381);
				for (int v = 5; v <= tileWidth / 2; ++v) {
					float N = Rand();
					noiseArray[(tileWidth-3)+v*tileWidth] = N;
					noiseArray[(tileWidth-3)+(tileWidth-1-v)*tileWidth] = N;
				}

				for (int h = tileWidth - 4; h >= tileWidth - 5; --h) {
					for (int v = 5; v < tileWidth - 5; ++v) {
						float N = Rand();
						noiseArray[h+v*tileWidth] = N;
						noiseArray[(2*tileWidth-6-h)+(tileWidth-1-v)*tileWidth] = N;
					}
				}

				// top border
				Random.InitState((l & 4) == 0 ? 7654321 : 5647381);
				for (int h = 5; h <= tileWidth / 2; ++h) {
					float N = Rand();
					noiseArray[h+(tileWidth-3)*tileWidth] = N;
					noiseArray[(tileWidth-1-h)+(tileWidth-3)*tileWidth] = N;
				}

				for (int v = tileWidth - 2; v < tileWidth; ++v) {
					for (int h = 5; h < tileWidth - 5; ++h) {
						float N = Rand();
						noiseArray[h+v*tileWidth] = N;
						noiseArray[(tileWidth-1-h)+(2*tileWidth-6-v)*tileWidth] = N;
					}
				}

				// left border
				Random.InitState((l & 8) == 0 ? 7654321 : 5647381);
				for (int v = 5; v <= tileWidth / 2; ++v) {
					float N = Rand();
					noiseArray[2+v*tileWidth] = N;
					noiseArray[2+(tileWidth-1-v)*tileWidth] = N;
				}

				for (int h = 1; h >= 0; --h) {
					for (int v = 5; v < tileWidth - 5; ++v) {
						float N = Rand();
						noiseArray[h+v*tileWidth] = N;
						noiseArray[(4-h)+(tileWidth-1-v)*tileWidth] = N;
					}
				}

				// center
				Random.InitState(rand);
				for (int v = 5; v < tileWidth - 5; ++v) {
					for (int h = 5; h < tileWidth - 5; ++h) {
						float N = Rand();
						noiseArray[h+v*tileWidth] = N;
					}
				}

				//randomize for next texture
				rand = (rand * 1103515245 + 12345) & 0x7FFFFFFF;

				m_noiseTextures[nl] = new RenderTexture(tileWidth, tileWidth, 0, RenderTextureFormat.RHalf);
				m_noiseTextures[nl].wrapMode = TextureWrapMode.Repeat;
				m_noiseTextures[nl].filterMode = FilterMode.Point;
				m_noiseTextures[nl].enableRandomWrite = true;
				m_noiseTextures[nl].Create();
				//write data into render texture
				buffer.SetData(noiseArray);
				CBUtility.WriteIntoRenderTexture(m_noiseTextures[nl], 1, buffer, World.WriteData);
				buffer.Release();
			}

		}

	}

    public class ElevationUniforms
    {
        public int tileWSD, coarseLevelSampler, coarseLevelOSL;
        public int noiseUVLH, noiseSampler;
        public int residualOSH, residualSampler;

        public ElevationUniforms()
        {
            tileWSD = Shader.PropertyToID("_TileWSD");
            coarseLevelSampler = Shader.PropertyToID("_CoarseLevelSampler");
            coarseLevelOSL = Shader.PropertyToID("_CoarseLevelOSL");
            noiseUVLH = Shader.PropertyToID("_NoiseUVLH");
            noiseSampler = Shader.PropertyToID("_NoiseSampler");
            residualOSH = Shader.PropertyToID("_ResidualOSH");
            residualSampler = Shader.PropertyToID("_ResidualSampler");
        }
    }

}




























