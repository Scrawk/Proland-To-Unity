
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

namespace Proland
{
	
	public class OrthoProducer : TileProducer
	{

        public override int Border { get { return 2; } }

        [SerializeField]
        private OrthoCPUProducer m_orthoCPUProducer;

		/// <summary>
        /// The Program to perform the upsampling and add procedure on GPU.
		/// </summary>
		[SerializeField]
        private Material m_upsampleMat;

		[SerializeField]
        private Color m_rootNoiseColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

		[SerializeField]
        private Color m_noiseColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);

     	/// <summary>
        /// Maximum quadtree level, or -1 to allow any level
     	/// </summary>
		[SerializeField]
        private int m_maxLevel = -1;

		[SerializeField]
        private bool m_hsv = true;

		[SerializeField]
        private int m_seed = 0;

		[SerializeField]
        private float[] m_noiseAmp = new float[] { 0, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 };

        private OrthoUniforms m_uniforms;

        private PerlinNoise m_noise;

        private Texture2D[] m_noiseTextures;

		private Texture2D m_residueTex;

		protected override void Start () 
		{
			base.Start();

			int tileSize = Cache.GetStorage(0).TileSize;

			if(m_orthoCPUProducer != null && m_orthoCPUProducer.GetTileSize(0) != tileSize)
				throw new InvalidParameterException("ortho CPU tile size must match ortho tile size");

			GPUTileStorage storage = Cache.GetStorage(0) as GPUTileStorage;
			
			if(storage == null)
				throw new InvalidStorageException("Storage must be a GPUTileStorage");

			m_uniforms = new OrthoUniforms();

			m_noise = new PerlinNoise(m_seed);

			m_residueTex = new Texture2D(tileSize, tileSize, TextureFormat.ARGB32, false);
			m_residueTex.wrapMode = TextureWrapMode.Clamp;
			m_residueTex.filterMode = FilterMode.Point;

			CreateOrthoNoise();
		}
		
		public override bool HasTile(int level, int tx, int ty) 
        {
			return (m_maxLevel == -1 || level <= m_maxLevel);
		}

		public override void DoCreateTile(int level, int tx, int ty, List<Slot> slot)
		{

			GPUSlot gpuSlot = slot[0] as GPUSlot;
			
			int tileWidth = gpuSlot.Owner.TileSize;
			int tileSize = tileWidth - 4;

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

			m_upsampleMat.SetFloat(m_uniforms.tileWidth, tileWidth);

			if (level > 0) 
			{
				RenderTexture tex = parentGpuSlot.Texture;

				m_upsampleMat.SetTexture(m_uniforms.coarseLevelSampler, tex);

				float dx = (tx % 2) * (tileSize / 2);
				float dy = (ty % 2) * (tileSize / 2);
				
				Vector4 coarseLevelOSL = new Vector4((dx+0.5f) / tex.width, (dy+0.5f) / tex.height, 1.0f / tex.width, 0.0f);
				
				m_upsampleMat.SetVector(m_uniforms.coarseLevelOSL, coarseLevelOSL);
			} 
			else
            {
				m_upsampleMat.SetVector(m_uniforms.coarseLevelOSL,  new Vector4(-1.0f, -1.0f, -1.0f, -1.0f));
			}

			if (m_orthoCPUProducer != null && m_orthoCPUProducer.HasTile(level, tx, ty)) 
			{
				Tile orthoCPUTile = m_orthoCPUProducer.FindTile(level, tx, ty, false, true);
				CPUSlot<byte> orthoCPUSlot = null;
				
				if(orthoCPUTile != null)
					orthoCPUSlot = orthoCPUTile.GetSlot(0) as CPUSlot<byte>;
				else 
					throw new MissingTileException("Find orthoCPU tile failed");

				int c = m_orthoCPUProducer.Channels;
				Color32 col = new Color32();
				byte[] data = orthoCPUSlot.Data;

				for(int x = 0; x < tileWidth; x++)
				{
					for(int y = 0; y < tileWidth; y++)
					{
						col.r = data[(x+y*tileWidth)*c];

						if(c > 1) col.g = data[(x+y*tileWidth)*c+1];
						if(c > 2) col.b = data[(x+y*tileWidth)*c+2];
						if(c > 3) col.a = data[(x+y*tileWidth)*c+3];

						m_residueTex.SetPixel(x, y, col);
					}
				}

				m_residueTex.Apply();

				m_upsampleMat.SetTexture(m_uniforms.residualSampler, m_residueTex);
				m_upsampleMat.SetVector(m_uniforms.residualOSH, new Vector4( 0.5f/tileWidth, 0.5f/tileWidth, 1.0f/tileWidth, 0.0f));
			} 
			else
            {
				m_upsampleMat.SetTexture(m_uniforms.residualSampler, null);
				m_upsampleMat.SetVector(m_uniforms.residualOSH, new Vector4( -1,-1,-1,-1));
			}

			float rs = level < m_noiseAmp.Length ? m_noiseAmp[level] : 0.0f;
			
			int noiseL = 0;
			int face = TerrainNode.Face;

			if(rs != 0.0f)
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
				else if (face == 6) 
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
			m_upsampleMat.SetVector(m_uniforms.noiseUVLH, new Vector4(noiseR, (noiseR + 1) % 4, 0.0f, m_hsv ? 1.0f : 0.0f));


			if(m_hsv)
			{
				Vector4 col = m_noiseColor * rs / 255.0f;
				col.w *= 2.0f;
				m_upsampleMat.SetVector(m_uniforms.noiseColor, col);
			} 
			else 
			{
				Vector4 col = m_noiseColor * rs * 2.0f / 255.0f;
				col.w *= 2.0f;
				m_upsampleMat.SetVector(m_uniforms.noiseColor, col);
			}

			m_upsampleMat.SetVector(m_uniforms.noiseRootColor, m_rootNoiseColor);

			Graphics.Blit(null, gpuSlot.Texture, m_upsampleMat);

			base.DoCreateTile(level, tx, ty, slot);

		}

		private void CreateOrthoNoise()
		{
			int tileWidth = Cache.GetStorage(0).TileSize;
			m_noiseTextures = new Texture2D[6];
			Color col = new Color();
			
			int[] layers = new int[]{0, 1, 3, 5, 7, 15};
			int rand = 1234567;
			
			for (int nl = 0; nl < 6; ++nl) 
			{
				int l = layers[nl];

				m_noiseTextures[nl] = new Texture2D(tileWidth, tileWidth, TextureFormat.ARGB32, false, true);

				// corners
				for (int j = 0; j < tileWidth; ++j) {
					for (int i = 0; i < tileWidth; ++i) {
						m_noiseTextures[nl].SetPixel(i,j, new Color(0.5f,0.5f,0.5f,0.5f));
					}
				}
				
				// bottom border
				Random.InitState((l & 1) == 0 ? 7654321 : 5647381);
				//Random.seed = 5647381;
				for (int v = 2; v < 4; ++v) {
					for (int h = 4; h < tileWidth - 4; ++h) {
						for (int c = 0; c < 4; ++c) {
							col[c] = Random.value;
						}

						m_noiseTextures[nl].SetPixel(h,v,col);
						m_noiseTextures[nl].SetPixel(tileWidth-1-h,3-v,col);
					}
				}
	
				// right border
				Random.InitState((l & 2) == 0 ? 7654321 : 5647381);
				//Random.seed = 5647381;
				for (int h = tileWidth - 3; h >= tileWidth - 4; --h) {
					for (int v = 4; v < tileWidth - 4; ++v) {
						for (int c = 0; c < 4; ++c) {
							col[c] = Random.value;
						}

						m_noiseTextures[nl].SetPixel(h,v,col);
						m_noiseTextures[nl].SetPixel(2*tileWidth-5-h,tileWidth-1-v,col);
					}
				}
				
				// top border
				Random.InitState((l & 4) == 0 ? 7654321 : 5647381);
				//Random.seed = 5647381;
				for (int v = tileWidth - 2; v < tileWidth; ++v) {
					for (int h = 4; h < tileWidth - 4; ++h) {
						for (int c = 0; c < 4; ++c) {
							col[c] = Random.value;
						}
						
						m_noiseTextures[nl].SetPixel(h,v,col);
						m_noiseTextures[nl].SetPixel(tileWidth-1-h,2*tileWidth-5-v,col);
					}
				}
				
				// left border
				Random.InitState((l & 8) == 0 ? 7654321 : 5647381);
				//Random.seed = 5647381;
				for (int h = 1; h >= 0; --h) {
					for (int v = 4; v < tileWidth - 4; ++v) {
						for (int c = 0; c < 4; ++c) {
							col[c] = Random.value;
						}
						
						m_noiseTextures[nl].SetPixel(h,v,col);
						m_noiseTextures[nl].SetPixel(3-h,tileWidth-1-v,col);
					}
				}
				
				// center
				Random.InitState(rand);
				for (int v = 4; v < tileWidth - 4; ++v) {
					for (int h = 4; h < tileWidth - 4; ++h) {
						for (int c = 0; c < 4; ++c) {
							col[c] = Random.value;
						}

						m_noiseTextures[nl].SetPixel(h,v,col);
					}
				}
				
				//randomize for next texture
				rand = (rand * 1103515245 + 12345) & 0x7FFFFFFF;

				m_noiseTextures[nl].Apply();

			}
			
		}

	}

    public class OrthoUniforms
    {
        public int tileWidth, coarseLevelSampler, coarseLevelOSL;
        public int noiseSampler, noiseUVLH, noiseColor;
        public int noiseRootColor;
        public int residualOSH, residualSampler;

        public OrthoUniforms()
        {
            tileWidth = Shader.PropertyToID("_TileWidth");
            coarseLevelSampler = Shader.PropertyToID("_CoarseLevelSampler");
            coarseLevelOSL = Shader.PropertyToID("_CoarseLevelOSL");
            noiseSampler = Shader.PropertyToID("_NoiseSampler");
            noiseUVLH = Shader.PropertyToID("_NoiseUVLH");
            noiseColor = Shader.PropertyToID("_NoiseColor");
            noiseRootColor = Shader.PropertyToID("_NoiseRootColor");
            residualOSH = Shader.PropertyToID("_ResidualOSH");
            residualSampler = Shader.PropertyToID("_ResidualSampler");
        }
    }

}



















