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
using UnityEngine.Rendering;
using System.Collections;

using Common.Unity.Drawing;

namespace Proland
{
	/// <summary>
	/// Extend the base class OceanNode to provide the data need 
	/// to create the waves using fourier transform which can then be applied
	/// to the projected grid handled by the OceanNode.
	/// All the fourier transforms are performed on the GPU
	/// </summary>
	public class OceanFFT : OceanNode
	{

		//CONST DONT CHANGE
		private const float WAVE_CM = 0.23f;	// Eq 59
        private const float WAVE_KM = 370.0f;	// Eq 59
        private const float AMP = 1.0f;

        public override float MaxSlopeVariance { get { return m_maxSlopeVariance; } }

		[SerializeField]
        private Material m_initSpectrumMat;

		[SerializeField]
        private Material m_initDisplacementMat;

        [SerializeField]
        private Shader m_fourierSdr;

        [SerializeField]
        private ComputeShader m_varianceShader;

		[SerializeField]
        private int m_ansio = 2;

		/// <summary>
        /// A higher wind speed gives greater swell to the waves.
		/// </summary>
		[SerializeField]
        private float m_windSpeed = 5.0f; 

		/// <summary>
        /// A lower number means the waves last longer and will build up larger waves.
		/// </summary>
		[SerializeField]
        private float m_omega = 0.84f; 

		/// <summary>
        /// Size in meters (i.e. in spatial domain) of each grid.
		/// </summary>
		[SerializeField]
        private Vector4 m_gridSizes = new Vector4(5488, 392, 28, 2);

		/// <summary>
        /// strenght of sideways displacement for each grid.
		/// </summary>
		[SerializeField]
        private Vector4 m_choppyness = new Vector4(2.3f, 2.1f, 1.3f, 0.9f);

		/// <summary>
        /// This is the fourier transform size, must pow2 number. Recommend no higher or lower than 64, 128 or 256.
		/// </summary>
		[SerializeField]
        private int m_fourierGridSize = 256;

        private int m_varianceSize = 16;
        private float m_fsize;
        private float m_maxSlopeVariance;
        private int m_idx = 0;
        private Vector4 m_offset;
        private Vector4 m_inverseGridSizes;

        private RenderTexture m_spectrum01, m_spectrum23;
        private RenderTexture m_WTable;
        private RenderTexture[] m_fourierBuffer0, m_fourierBuffer1, m_fourierBuffer2;
        private RenderTexture[] m_fourierBuffer3, m_fourierBuffer4;
        private RenderTexture m_map0, m_map1, m_map2, m_map3, m_map4;
        private RenderTexture m_variance;

        private FourierGPU m_fourier;

		protected override void Start () 
		{
			base.Start();

			if(m_fourierGridSize > 256)
			{
				Debug.Log("fourier grid size must not be greater than 256, changing to 256");
				m_fourierGridSize = 256;
			}
			
			if(!Mathf.IsPowerOfTwo(m_fourierGridSize))
			{
				Debug.Log("fourier grid size must be pow2 number, changing to nearest pow2 number");
				m_fourierGridSize = Mathf.NextPowerOfTwo(m_fourierGridSize);
			}

			m_fsize = (float)m_fourierGridSize;
			m_offset = new Vector4(1.0f + 0.5f / m_fsize, 1.0f + 0.5f / m_fsize, 0, 0);

			float factor = 2.0f * Mathf.PI * m_fsize;
			m_inverseGridSizes = new Vector4(factor/m_gridSizes.x, factor/m_gridSizes.y, factor/m_gridSizes.z, factor/m_gridSizes.w);

			m_fourier = new FourierGPU(m_fourierGridSize, m_fourierSdr);

			//Create the data needed to make the waves each frame
			CreateRenderTextures();
			GenerateWavesSpectrum();
			CreateWTable();

			m_initSpectrumMat.SetTexture("_Spectrum01", m_spectrum01);
			m_initSpectrumMat.SetTexture("_Spectrum23", m_spectrum23);
			m_initSpectrumMat.SetTexture("_WTable", m_WTable);
			m_initSpectrumMat.SetVector("_Offset", m_offset);
			m_initSpectrumMat.SetVector("_InverseGridSizes", m_inverseGridSizes);
			
			m_initDisplacementMat.SetVector("_InverseGridSizes", m_inverseGridSizes);
		}

		/// <summary>
		/// Initializes the data to the shader that needs to 
		/// have the fourier transform applied to it this frame.
		/// </summary>
		/// <param name="t">time in seconds</param>
		protected virtual void InitWaveSpectrum(float t)
		{
			// init heights (0) and slopes (1,2)
			RenderTexture[] buffers012 = new RenderTexture[] { m_fourierBuffer0[1], m_fourierBuffer1[1], m_fourierBuffer2[1] };
			m_initSpectrumMat.SetFloat("_T", t);
			RTUtility.MultiTargetBlit(buffers012, m_initSpectrumMat);
			
			// Init displacement (3,4)
			RenderTexture[] buffers34 = new RenderTexture[] { m_fourierBuffer3[1], m_fourierBuffer4[1] };
			m_initDisplacementMat.SetTexture("_Buffer1", m_fourierBuffer1[1]);
			m_initDisplacementMat.SetTexture("_Buffer2", m_fourierBuffer2[1]);
			RTUtility.MultiTargetBlit(buffers34, m_initDisplacementMat);
		}

		public override void UpdateNode()
		{

			float t = Time.time;

			InitWaveSpectrum(t);

			//Perform fourier transform and record what is the current index
			m_idx = m_fourier.PeformFFT(m_fourierBuffer0, m_fourierBuffer1, m_fourierBuffer2);
			m_fourier.PeformFFT(m_fourierBuffer3, m_fourierBuffer4);
			
			//Copy the contents of the completed fourier transform to the map textures.
			//You could just use the buffer textures (m_fourierBuffer0,1,2,etc) to read from for the ocean shader 
			//but they need to have mipmaps and unity updates the mipmaps
			//every time the texture is renderer into. This impacts performance during fourier transform stage as mipmaps would be updated every pass
			//and there is no way to disable and then enable mipmaps on render textures in Unity at time of writting.
			
			Graphics.Blit(m_fourierBuffer0[m_idx], m_map0);
			Graphics.Blit(m_fourierBuffer1[m_idx], m_map1);
			Graphics.Blit(m_fourierBuffer2[m_idx], m_map2);
			Graphics.Blit(m_fourierBuffer3[m_idx], m_map3);
			Graphics.Blit(m_fourierBuffer4[m_idx], m_map4);

			m_oceanMaterial.SetVector("_Ocean_MapSize", new Vector2(m_fsize, m_fsize));
			m_oceanMaterial.SetVector("_Ocean_Choppyness", m_choppyness);
			m_oceanMaterial.SetVector("_Ocean_GridSizes", m_gridSizes);
			m_oceanMaterial.SetFloat("_Ocean_HeightOffset", m_oceanLevel);
			m_oceanMaterial.SetTexture("_Ocean_Variance", m_variance);
			m_oceanMaterial.SetTexture("_Ocean_Map0", m_map0);
			m_oceanMaterial.SetTexture("_Ocean_Map1", m_map1);
			m_oceanMaterial.SetTexture("_Ocean_Map2", m_map2);
			m_oceanMaterial.SetTexture("_Ocean_Map3", m_map3);
			m_oceanMaterial.SetTexture("_Ocean_Map4", m_map4);

			//Make sure base class get updated as well
			base.UpdateNode();

		}

		protected override void OnDestroy()
		{
			base.OnDestroy();

			m_map0.Release();
			m_map1.Release();
			m_map2.Release();
			m_map3.Release();
			m_map4.Release();

			m_spectrum01.Release();
			m_spectrum23.Release();
			
			m_WTable.Release();
			m_variance.Release();
			
			for(int i = 0; i < 2; i++)
			{
				m_fourierBuffer0[i].Release();
				m_fourierBuffer1[i].Release();
				m_fourierBuffer2[i].Release();
				m_fourierBuffer3[i].Release();
				m_fourierBuffer4[i].Release();
			}
		}
		
		protected virtual void CreateRenderTextures()
		{

			RenderTextureFormat mapFormat = RenderTextureFormat.ARGBFloat;
			RenderTextureFormat format = RenderTextureFormat.ARGBFloat;
			
			//These texture hold the actual data use in the ocean renderer
			CreateMap(ref m_map0, mapFormat, m_ansio); 
			CreateMap(ref m_map1, mapFormat, m_ansio); 
			CreateMap(ref m_map2, mapFormat, m_ansio); 
			CreateMap(ref m_map3, mapFormat, m_ansio); 
			CreateMap(ref m_map4, mapFormat, m_ansio); 

			//These textures are used to perform the fourier transform
			CreateBuffer(ref m_fourierBuffer0, format);//heights
			CreateBuffer(ref m_fourierBuffer1, format);// slopes X
			CreateBuffer(ref m_fourierBuffer2, format);// slopes Y
			CreateBuffer(ref m_fourierBuffer3, format);// displacement X
			CreateBuffer(ref m_fourierBuffer4, format);// displacement Y
			
			//These textures hold the specturm the fourier transform is performed on
			m_spectrum01 = new RenderTexture(m_fourierGridSize, m_fourierGridSize, 0, format);
			m_spectrum01.filterMode = FilterMode.Point;
			m_spectrum01.wrapMode = TextureWrapMode.Repeat;
			m_spectrum01.enableRandomWrite = true;
			m_spectrum01.Create();
			
			m_spectrum23 = new RenderTexture(m_fourierGridSize, m_fourierGridSize, 0, format);
			m_spectrum23.filterMode = FilterMode.Point;
			m_spectrum23.wrapMode = TextureWrapMode.Repeat;	
			m_spectrum23.enableRandomWrite = true;
			m_spectrum23.Create();
			
			m_WTable = new RenderTexture(m_fourierGridSize, m_fourierGridSize, 0, format);
			m_WTable.filterMode = FilterMode.Point;
			m_WTable.wrapMode = TextureWrapMode.Clamp;
			m_WTable.enableRandomWrite = true;
			m_WTable.Create();
			
			m_variance = new RenderTexture(m_varianceSize, m_varianceSize, 0, RenderTextureFormat.RHalf);
			m_variance.volumeDepth = m_varianceSize;
			m_variance.wrapMode = TextureWrapMode.Clamp;
			m_variance.filterMode = FilterMode.Bilinear;
            m_variance.dimension = TextureDimension.Tex3D;
			m_variance.enableRandomWrite = true;
			m_variance.useMipMap = true;
			m_variance.Create();
			
		}

		protected void CreateBuffer(ref RenderTexture[] tex, RenderTextureFormat format)
		{
			tex = new RenderTexture[2];
			
			for(int i = 0; i < 2; i++)
			{
				tex[i] = new RenderTexture(m_fourierGridSize, m_fourierGridSize, 0, format);
				tex[i].filterMode = FilterMode.Point;
				tex[i].wrapMode = TextureWrapMode.Clamp;
				tex[i].Create();
			}
		}

		protected void CreateMap(ref RenderTexture map, RenderTextureFormat format, int ansio)
		{
			map = new RenderTexture(m_fourierGridSize, m_fourierGridSize, 0, format);
			map.filterMode = FilterMode.Trilinear;
			map.wrapMode = TextureWrapMode.Repeat;
			map.anisoLevel = ansio;
			map.useMipMap = true;
			map.Create();
		}

        private float sqr(float x) { return x * x; }

        private float omega(float k) { return Mathf.Sqrt(9.81f * k * (1.0f + sqr(k / WAVE_KM))); } // Eq 24

        /// <summary>
        /// Recreates a statistcally representative model of a wave spectrum in the frequency domain.
        /// </summary>
        private float Spectrum(float kx, float ky, bool omnispectrum)
		{
			float U10 = m_windSpeed;
			
			// phase speed
			float k = Mathf.Sqrt(kx * kx + ky * ky);
			float c = omega(k) / k;
			
			// spectral peak
			float kp = 9.81f * sqr(m_omega / U10); // after Eq 3
			float cp = omega(kp) / kp;
			
			// friction velocity
			float z0 = 3.7e-5f * sqr(U10) / 9.81f * Mathf.Pow(U10 / cp, 0.9f); // Eq 66
			float u_star = 0.41f * U10 / Mathf.Log(10.0f / z0); // Eq 60
			
			float Lpm = Mathf.Exp(- 5.0f / 4.0f * sqr(kp / k)); // after Eq 3
			float gamma = (m_omega < 1.0f) ? 1.7f : 1.7f + 6.0f * Mathf.Log(m_omega); // after Eq 3 // log10 or log?
			float sigma = 0.08f * (1.0f + 4.0f / Mathf.Pow(m_omega, 3.0f)); // after Eq 3
			float Gamma = Mathf.Exp(-1.0f / (2.0f * sqr(sigma)) * sqr(Mathf.Sqrt(k / kp) - 1.0f));
			float Jp = Mathf.Pow(gamma, Gamma); // Eq 3
			float Fp = Lpm * Jp * Mathf.Exp(-m_omega / Mathf.Sqrt(10.0f) * (Mathf.Sqrt(k / kp) - 1.0f)); // Eq 32
			float alphap = 0.006f * Mathf.Sqrt(m_omega); // Eq 34
			float Bl = 0.5f * alphap * cp / c * Fp; // Eq 31

			float alpham = 0.01f * (u_star < WAVE_CM ? 1.0f + Mathf.Log(u_star / WAVE_CM) : 1.0f + 3.0f * Mathf.Log(u_star / WAVE_CM)); // Eq 44
			float Fm = Mathf.Exp(-0.25f * sqr(k / WAVE_KM - 1.0f)); // Eq 41
			float Bh = 0.5f * alpham * WAVE_CM / c * Fm * Lpm; // Eq 40 (fixed)

			Bh *= Lpm; // bug fix???

			if(omnispectrum) 
				return AMP * (Bl + Bh) / (k * sqr(k)); // Eq 30
			
			float a0 = Mathf.Log(2.0f) / 4.0f; 
			float ap = 4.0f; 
			float am = 0.13f * u_star / WAVE_CM; // Eq 59
			float Delta = (float)System.Math.Tanh(a0 + ap * Mathf.Pow(c / cp, 2.5f) + am * Mathf.Pow(WAVE_CM / c, 2.5f)); // Eq 57
			
			float phi = Mathf.Atan2(ky, kx);
			
			if (kx < 0.0f) return 0.0f;
			
			Bl *= 2.0f;
			Bh *= 2.0f;
			
			// remove waves perpendicular to wind dir
			float tweak = Mathf.Sqrt(Mathf.Max(kx/Mathf.Sqrt(kx*kx+ky*ky),0.0f));
			
			return AMP * (Bl + Bh) * (1.0f + Delta * Mathf.Cos(2.0f * phi)) / (2.0f * Mathf.PI * sqr(sqr(k))) * tweak; // Eq 67
		}

        private Vector2 GetSpectrumSample(float i, float j, float lengthScale, float kMin)
		{
			float dk = 2.0f * Mathf.PI / lengthScale;
			float kx = i * dk;
			float ky = j * dk;
			Vector2 result = new Vector2(0.0f,0.0f);
			
			float rnd = Random.value;
			
			if(Mathf.Abs(kx) >= kMin || Mathf.Abs(ky) >= kMin)
			{
				float S = Spectrum(kx, ky, false);
				float h = Mathf.Sqrt(S / 2.0f) * dk;
				
				float phi = rnd * 2.0f * Mathf.PI;
				result.x = h * Mathf.Cos(phi);
				result.y = h * Mathf.Sin(phi);
			}
			
			return result;
		}

        private float GetSlopeVariance(float kx, float ky, Vector2 spectrumSample)
		{
			float kSquare = kx * kx + ky * ky;
			float real = spectrumSample.x;
			float img = spectrumSample.y;
			float hSquare = real * real + img * img;
			return kSquare * hSquare * 2.0f;
		}

        private void GenerateWavesSpectrum()
		{
			
			// Slope variance due to all waves, by integrating over the full spectrum.
			// Used by the BRDF rendering model
			float theoreticSlopeVariance = 0.0f;
			float k = 5e-3f;
			while (k < 1e3f) 
			{
				float nextK = k * 1.001f;
				theoreticSlopeVariance += k * k * Spectrum(k, 0, true) * (nextK - k);
				k = nextK;
			}
			
			float[] spectrum01 = new float[m_fourierGridSize*m_fourierGridSize*4];
			float[] spectrum23 = new float[m_fourierGridSize*m_fourierGridSize*4];
			
			int idx;
			float i;
			float j;
			float totalSlopeVariance = 0.0f;
			Vector2 sample12XY;
			Vector2 sample12ZW;
			Vector2 sample34XY;
			Vector2 sample34ZW;

            Random.InitState(0);


            for (int x = 0; x < m_fourierGridSize; x++) 
			{
				for (int y = 0; y < m_fourierGridSize; y++) 
				{
					idx = x+y*m_fourierGridSize;
					i = (x >= m_fourierGridSize / 2) ? (float)(x - m_fourierGridSize) : (float)x;
					j = (y >= m_fourierGridSize / 2) ? (float)(y - m_fourierGridSize) : (float)y;
					
					sample12XY = GetSpectrumSample(i, j, m_gridSizes.x, Mathf.PI / m_gridSizes.x);
					sample12ZW = GetSpectrumSample(i, j, m_gridSizes.y, Mathf.PI * m_fsize / m_gridSizes.x);
					sample34XY = GetSpectrumSample(i, j, m_gridSizes.z, Mathf.PI * m_fsize / m_gridSizes.y);
					sample34ZW = GetSpectrumSample(i, j, m_gridSizes.w, Mathf.PI * m_fsize / m_gridSizes.z);
					
					spectrum01[idx*4+0] = sample12XY.x;
					spectrum01[idx*4+1] = sample12XY.y;
					spectrum01[idx*4+2] = sample12ZW.x;
					spectrum01[idx*4+3] = sample12ZW.y;
					
					spectrum23[idx*4+0] = sample34XY.x;
					spectrum23[idx*4+1] = sample34XY.y;
					spectrum23[idx*4+2] = sample34ZW.x;
					spectrum23[idx*4+3] = sample34ZW.y;
					
					i *= 2.0f * Mathf.PI;
					j *= 2.0f * Mathf.PI;
					
					totalSlopeVariance += GetSlopeVariance(i / m_gridSizes.x, j / m_gridSizes.x, sample12XY);
					totalSlopeVariance += GetSlopeVariance(i / m_gridSizes.y, j / m_gridSizes.y, sample12ZW);
					totalSlopeVariance += GetSlopeVariance(i / m_gridSizes.z, j / m_gridSizes.z, sample34XY);
					totalSlopeVariance += GetSlopeVariance(i / m_gridSizes.w, j / m_gridSizes.w, sample34ZW);
				}
			}
			
			//Write floating point data into render texture
			ComputeBuffer buffer = new ComputeBuffer(m_fourierGridSize*m_fourierGridSize, sizeof(float)*4);
			
			buffer.SetData(spectrum01);
			CBUtility.WriteIntoRenderTexture(m_spectrum01, 4, buffer, World.WriteData);
			
			buffer.SetData(spectrum23);
			CBUtility.WriteIntoRenderTexture(m_spectrum23, 4, buffer, World.WriteData);
			
			buffer.Release();
			
			m_varianceShader.SetFloat("_SlopeVarianceDelta", 0.5f * (theoreticSlopeVariance - totalSlopeVariance));
			m_varianceShader.SetFloat("_VarianceSize", (float)m_varianceSize);
			m_varianceShader.SetFloat("_Size", m_fsize);
			m_varianceShader.SetVector("_GridSizes", m_gridSizes);
			m_varianceShader.SetTexture(0, "_Spectrum01", m_spectrum01);
			m_varianceShader.SetTexture(0, "_Spectrum23", m_spectrum23);
			m_varianceShader.SetTexture(0, "des", m_variance);
			
			m_varianceShader.Dispatch(0,m_varianceSize/4,m_varianceSize/4,m_varianceSize/4);

			//Find the maximum value for slope variance

			buffer = new ComputeBuffer(m_varianceSize*m_varianceSize*m_varianceSize, sizeof(float));
			CBUtility.ReadFromRenderTexture(m_variance, 1, buffer, World.ReadData);

			float[] varianceData = new float[m_varianceSize*m_varianceSize*m_varianceSize];
			buffer.GetData(varianceData);

			m_maxSlopeVariance = 0.0f;
			for(int v = 0; v < m_varianceSize*m_varianceSize*m_varianceSize; v++) {
				m_maxSlopeVariance = Mathf.Max(m_maxSlopeVariance, varianceData[v]);
			}

			buffer.Release();
			
		}

		private void CreateWTable()
		{
			//Some values need for the InitWaveSpectrum function can be precomputed
			Vector2 uv, st;
			float k1, k2, k3, k4, w1, w2, w3, w4;
			
			float[] table = new float[m_fourierGridSize*m_fourierGridSize*4];
			
			for (int x = 0; x < m_fourierGridSize; x++) 
			{
				for (int y = 0; y < m_fourierGridSize; y++) 
				{
					uv = new Vector2(x,y) / m_fsize;
					
					st.x = uv.x > 0.5f ? uv.x - 1.0f : uv.x;
					st.y = uv.y > 0.5f ? uv.y - 1.0f : uv.y;
					
					k1 = (st * m_inverseGridSizes.x).magnitude;
					k2 = (st * m_inverseGridSizes.y).magnitude;
					k3 = (st * m_inverseGridSizes.z).magnitude;
					k4 = (st * m_inverseGridSizes.w).magnitude;
					
					w1 = Mathf.Sqrt(9.81f * k1 * (1.0f + k1 * k1 / (WAVE_KM*WAVE_KM)));
					w2 = Mathf.Sqrt(9.81f * k2 * (1.0f + k2 * k2 / (WAVE_KM*WAVE_KM)));
					w3 = Mathf.Sqrt(9.81f * k3 * (1.0f + k3 * k3 / (WAVE_KM*WAVE_KM)));
					w4 = Mathf.Sqrt(9.81f * k4 * (1.0f + k4 * k4 / (WAVE_KM*WAVE_KM)));
					
					table[(x+y*m_fourierGridSize)*4+0] = w1;
					table[(x+y*m_fourierGridSize)*4+1] = w2;
					table[(x+y*m_fourierGridSize)*4+2] = w3;
					table[(x+y*m_fourierGridSize)*4+3] = w4;
					
				}
			}
			
			//Write floating point data into render texture
			ComputeBuffer buffer = new ComputeBuffer(m_fourierGridSize*m_fourierGridSize, sizeof(float)*4);
			
			buffer.SetData(table);
			CBUtility.WriteIntoRenderTexture(m_WTable, 4, buffer, World.WriteData);
			
			buffer.Release();
			
		}
	}

}




























