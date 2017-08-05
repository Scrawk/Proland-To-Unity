using UnityEngine;
using UnityEngine.Rendering;

using Common.Unity.Drawing;

namespace Proland
{
	/// <summary>
	/// Loads the tables required for the atmospheric scattering and sets any uniforms for shaders
	/// that need them. If you create new tables using the PreprocessAtmo.cs script and changed some of  
	/// the settings (like the tables dimensions) you need to make sure the settings match here.
	/// You can adjust some of these settings (mieG, betaR) to change the look of the scattering but
	/// as precomputed tables are used there is a limit to how much the scattering will change.
	/// For large changes you will need to create new table with the settings you want.
	/// NOTE - all scenes must contain a skyNode.
	/// </summary>
	public class SkyNode : Node
	{

		/// <summary>
        /// The radius of the planet (Rg), radius of the atmosphere (Rt).
		/// </summary>
		private const float Rg = 6360000.0f;
        private const float Rt = 6420000.0f;
        private const float RL = 6421000.0f;

		/// <summary>
        /// Dimensions of the tables.
		/// </summary>
        private const int TRANSMITTANCE_W = 256;
        private const int TRANSMITTANCE_H = 64;
        private const int SKY_W = 64;
        private const int SKY_H = 16;
        private const int RES_R = 32;
        private const int RES_MU = 128;
        private const int RES_MU_S = 32;
        private const int RES_NU = 8;

        private const float AVERAGE_GROUND_REFLECTANCE = 0.1f;

	    /// <summary>
        /// Half heights for the atmosphere air density (HR) and particle density (HM)
        /// This is the height in km that half the particles are found below.
	    /// </summary>
        private const float HR = 8.0f;
        private const float HM = 1.2f;

		/// <summary>
        /// scatter coefficient for mie.
		/// </summary>
        private readonly Vector3 BETA_MSca = new Vector3(4e-3f, 4e-3f, 4e-3f);

		[SerializeField]
        private Material m_skyMaterial;

		[SerializeField]
        private Material m_skyMapMaterial;

		/// <summary>
        /// scatter coefficient for rayliegh
		/// </summary>
		[SerializeField]
        private Vector3 m_betaR = new Vector3(5.8e-3f, 1.35e-2f, 3.31e-2f);

        /// <summary>
        /// Asymmetry factor for the mie phase function
        /// A higher number meands more light is scattered in the forward direction
        /// </summary>
		[SerializeField]
        private float m_mieG = 0.85f;

        private string m_filePath = "/Proland/Textures/Atmo";

        private Mesh m_mesh;

        private RenderTexture m_transmittance, m_inscatter, m_irradiance, m_skyMap;

		protected override void Start() 
		{
			base.Start ();

			m_mesh = MeshFactory.MakePlane(2, 2, MeshFactory.PLANE.XY, false);
			m_mesh.bounds = new Bounds(Vector3.zero, new Vector3(1e8f, 1e8f, 1e8f));

			//The sky map is used to create a reflection of the sky for objects that need it (like the ocean)
			m_skyMap = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGBHalf);
			m_skyMap.filterMode = FilterMode.Trilinear;
			m_skyMap.wrapMode = TextureWrapMode.Clamp;
			m_skyMap.anisoLevel = 9;
			m_skyMap.useMipMap = true;
			//m_skyMap.mipMapBias = -0.5f;
			m_skyMap.Create();

			//Transmittance is responsible for the change in the sun color as it moves
			//The raw file is a 2D array of 32 bit floats with a range of 0 to 1
			string path = Application.dataPath + m_filePath + "/transmittance.raw";

			m_transmittance = new RenderTexture(TRANSMITTANCE_W, TRANSMITTANCE_H, 0, RenderTextureFormat.ARGBHalf);
			m_transmittance.wrapMode = TextureWrapMode.Clamp;
			m_transmittance.filterMode = FilterMode.Bilinear;
			m_transmittance.enableRandomWrite = true;
			m_transmittance.Create();
			
			ComputeBuffer buffer = new ComputeBuffer(TRANSMITTANCE_W*TRANSMITTANCE_H, sizeof(float)*3);
			CBUtility.WriteIntoRenderTexture(m_transmittance, 3, path, buffer, World.WriteData);
			buffer.Release();

			//Iirradiance is responsible for the change in the sky color as the sun moves
			//The raw file is a 2D array of 32 bit floats with a range of 0 to 1
			path = Application.dataPath + m_filePath + "/irradiance.raw";

			m_irradiance = new RenderTexture(SKY_W, SKY_H, 0, RenderTextureFormat.ARGBHalf);
			m_irradiance.wrapMode = TextureWrapMode.Clamp;
			m_irradiance.filterMode = FilterMode.Bilinear;
			m_irradiance.enableRandomWrite = true;
			m_irradiance.Create();
			
			buffer = new ComputeBuffer(SKY_W*SKY_H, sizeof(float)*3);
			CBUtility.WriteIntoRenderTexture(m_irradiance, 3, path, buffer, World.WriteData);
			buffer.Release();
			
			//Inscatter is responsible for the change in the sky color as the sun moves
			//The raw file is a 4D array of 32 bit floats with a range of 0 to 1.589844
			//As there is not such thing as a 4D texture the data is packed into a 3D texture 
			//and the shader manually performs the sample for the 4th dimension
			path = Application.dataPath + m_filePath + "/inscatter.raw";

			m_inscatter = new RenderTexture(RES_MU_S * RES_NU, RES_MU, 0, RenderTextureFormat.ARGBHalf);
			m_inscatter.volumeDepth = RES_R;
			m_inscatter.wrapMode = TextureWrapMode.Clamp;
			m_inscatter.filterMode = FilterMode.Bilinear;
			m_inscatter.dimension = TextureDimension.Tex3D;
            m_inscatter.enableRandomWrite = true;
			m_inscatter.Create();
			
			buffer = new ComputeBuffer(RES_MU_S*RES_NU*RES_MU*RES_R, sizeof(float)*4);
			CBUtility.WriteIntoRenderTexture(m_inscatter, 4, path, buffer, World.WriteData);
			buffer.Release();

			InitUniforms(m_skyMaterial);
			InitUniforms(m_skyMapMaterial);

		}

		protected override void OnDestroy()
		{
			base.OnDestroy();

			m_transmittance.Release();
			m_irradiance.Release();
			m_inscatter.Release();
			m_skyMap.Release();
		}
		
		public void UpdateNode() 
		{
			SetUniforms(m_skyMaterial);
			SetUniforms(m_skyMapMaterial);

			World.SetUniforms(m_skyMaterial);
			m_skyMaterial.SetMatrix("_Sun_WorldToLocal", World.SunNode.WorldToLocalRotation);

			Graphics.DrawMesh(m_mesh, Matrix4x4.identity, m_skyMaterial, 0, Camera.main);

			//Update the sky map if...
			//The sun has moved
			//Or if this is first frame
			//And if this is not a deformed terrain (ie a planet). Planet sky map not supported
			if((!World.IsDeformed && World.SunNode.HasMoved) || Time.frameCount == 1)
				Graphics.Blit(null, m_skyMap, m_skyMapMaterial);
		}

		public void SetUniforms(Material mat)
		{	
			//Sets uniforms that this or other gameobjects may need
			if(mat == null) return;

			mat.SetVector("betaR", m_betaR / 1000.0f);
			mat.SetFloat("mieG", Mathf.Clamp(m_mieG, 0.0f, 0.99f));
			mat.SetTexture("_Sky_Transmittance", m_transmittance);
			mat.SetTexture("_Sky_Inscatter", m_inscatter);
			mat.SetTexture("_Sky_Irradiance", m_irradiance);
			mat.SetTexture("_Sky_Map", m_skyMap);

			World.SunNode.SetUniforms(mat);
		}

		public void InitUniforms(Material mat)
		{
			//Init uniforms that this or other gameobjects may need
			if(mat == null) return;

			mat.SetFloat("scale",Rg / World.Radius);
			mat.SetFloat("Rg", Rg);
			mat.SetFloat("Rt", Rt);
			mat.SetFloat("RL", RL);
			mat.SetFloat("TRANSMITTANCE_W", TRANSMITTANCE_W);
			mat.SetFloat("TRANSMITTANCE_H", TRANSMITTANCE_H);
			mat.SetFloat("SKY_W", SKY_W);
			mat.SetFloat("SKY_H", SKY_H);
			mat.SetFloat("RES_R", RES_R);
			mat.SetFloat("RES_MU", RES_MU);
			mat.SetFloat("RES_MU_S", RES_MU_S);
			mat.SetFloat("RES_NU", RES_NU);
			mat.SetFloat("AVERAGE_GROUND_REFLECTANCE", AVERAGE_GROUND_REFLECTANCE);
			mat.SetFloat("HR", HR * 1000.0f);
			mat.SetFloat("HM", HM * 1000.0f);
			mat.SetVector("betaMSca", BETA_MSca / 1000.0f);
			mat.SetVector("betaMEx", (BETA_MSca / 1000.0f) / 0.9f);
	
		}

		void OnGUI(){
			//GUI.DrawTexture(new Rect(0,0,512, 512), m_skyMap);
		}

	}
}













