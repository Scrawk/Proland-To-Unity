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

#pragma warning disable 162

using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

using Common.Unity.Drawing;

namespace Proland
{
    /// <summary>
    /// Precomputes the tables for the given atmosphere parameters.
	/// To run this just create a new scene and add this script to a game object and then attach the compute shaders.
	/// Once the scene is run the tables will be saved to the file path.
	/// If you change some of the settings, like the table dimensions then you will need to open up the Sky script
	/// and make sure the setting for the tables match the settings in that script.
    /// 
    /// The tables created are almost the same as the original Proland code.
    /// The inscatter is slightly darker and there is a small pixel offset issue at the horizon. 
    /// </summary>
    public class CreateAtmosphereTables : EditorWindow
    {
        //Dont change these
        private const int NUM_THREADS = 8;
        private const int READ = 0;
        private const int WRITE = 1;

        //Will save the tables as 8 bit png files so they can be
        //viewed in photoshop. Used for debugging.
        private const bool WRITE_DEBUG_TEX = false;

        //You can change these
        //The radius of the planet (Rg), radius of the atmosphere (Rt)
        private float Rg = 6360.0f;
        private float Rt = 6420.0f;
        private float RL = 6421.0f;

        //Dimensions of the tables
        private const int TRANSMITTANCE_W = 256;
        private const int TRANSMITTANCE_H = 64;

        private const int IRRADIANCE_W = 64;
        private const int IRRADIANCE_H = 16;

        private const int INSCATTER_R = 32;
        private const int INSCATTER_MU = 128;
        private const int INSCATTER_MU_S = 32;
        private const int INSCATTER_NU = 8;

        //Physical settings, Mie and Rayliegh values
        private float AVERAGE_GROUND_REFLECTANCE = 0.1f;
        private Vector4 BETA_R = new Vector4(5.8e-3f, 1.35e-2f, 3.31e-2f, 0.0f);
        private Vector4 BETA_MSca = new Vector4(4e-3f, 4e-3f, 4e-3f, 0.0f);
        private Vector4 BETA_MEx = new Vector4(4.44e-3f, 4.44e-3f, 4.44e-3f, 0.0f);

        //Asymmetry factor for the mie phase function
        //A higher number meands more light is scattered in the forward direction
        private float MIE_G = 0.8f;

        //Half heights for the atmosphere air density (HR) and particle density (HM)
        //This is the height in km that half the particles are found below
        private float HR = 8.0f;
        private float HM = 1.2f;

		RenderTexture m_transmittanceT;
		RenderTexture m_deltaET, m_deltaSRT, m_deltaSMT, m_deltaJT;
		RenderTexture[] m_irradianceT, m_inscatterT;

		//This is where the tables will be saved to
		public string m_filePath = "/Proland/Textures/Atmo";

		public ComputeShader m_copyInscatter1, m_copyInscatterN, m_copyIrradiance;
		public ComputeShader m_inscatter1, m_inscatterN, m_inscatterS;
		public ComputeShader m_irradiance1, m_irradianceN, m_transmittance;
		public ComputeShader m_readData;

        private int m_step, m_order;
        private bool m_finished = false;

        private GUIStyle m_boxStyle, m_wrapStyle;

        [MenuItem("Window/Proland/Create Atmosphere Tables")]
        private static void Init()
        {
            CreateAtmosphereTables window = GetWindow<CreateAtmosphereTables>(false, "Create Atmosphere Tables");
            window.Show();
        }

        private void OnGUI()
        {

            if (m_boxStyle == null)
            {
                m_boxStyle = new GUIStyle(GUI.skin.box);
                m_boxStyle.normal.textColor = GUI.skin.label.normal.textColor;
                m_boxStyle.fontStyle = FontStyle.Bold;
                m_boxStyle.alignment = TextAnchor.UpperLeft;
            }

            if (m_wrapStyle == null)
            {
                m_wrapStyle = new GUIStyle(GUI.skin.label);
                m_wrapStyle.fontStyle = FontStyle.Normal;
                m_wrapStyle.wordWrap = true;
            }

            GUILayout.BeginVertical("Create Atmosphere Tablesp", m_boxStyle);
            GUILayout.Space(20);
            EditorGUILayout.LabelField("Create the look up tables needed to render the atmosphere.", m_wrapStyle);
            GUILayout.EndVertical();

            Rg = EditorGUILayout.FloatField("Radius at ground", Rg);
            Rt = EditorGUILayout.FloatField("Radius at top", Rt);
            RL = Rt + 1.0f;

            AVERAGE_GROUND_REFLECTANCE = EditorGUILayout.FloatField("Average Ground Reflectance", AVERAGE_GROUND_REFLECTANCE);
            MIE_G = EditorGUILayout.FloatField("Mie asymmetry factor", MIE_G);
            HR = EditorGUILayout.FloatField("Half height Rayliegh", HR);
            HM = EditorGUILayout.FloatField("Half height Mie", HM);

            BETA_R = EditorGUILayout.Vector3Field("Rayliegh factor", BETA_R);
            BETA_MSca = EditorGUILayout.Vector3Field("Mie scatter factor", BETA_MSca);
            BETA_MEx = EditorGUILayout.Vector3Field("Mie extinction factor", BETA_MEx);

            if (GUILayout.Button("Create"))
            {
                Run();
                EditorUtility.FocusProjectWindow();
            }

        }

        private void Run()
        {

            m_irradianceT = new RenderTexture[2];
			m_inscatterT = new RenderTexture[2];

			m_transmittanceT = new RenderTexture(TRANSMITTANCE_W, TRANSMITTANCE_H, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
			m_transmittanceT.enableRandomWrite = true;
			m_transmittanceT.Create();

			m_irradianceT[0] = new RenderTexture(IRRADIANCE_W, IRRADIANCE_H, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
			m_irradianceT[0].enableRandomWrite = true;
			m_irradianceT[0].Create();

			m_irradianceT[1] = new RenderTexture(IRRADIANCE_W, IRRADIANCE_H, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
			m_irradianceT[1].enableRandomWrite = true;
			m_irradianceT[1].Create();

			m_inscatterT[0] = new RenderTexture(INSCATTER_MU_S * INSCATTER_NU, INSCATTER_MU, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            m_inscatterT[0].dimension = TextureDimension.Tex3D;
			m_inscatterT[0].enableRandomWrite = true;
			m_inscatterT[0].volumeDepth = INSCATTER_R;
			m_inscatterT[0].Create();

			m_inscatterT[1] = new RenderTexture(INSCATTER_MU_S * INSCATTER_NU, INSCATTER_MU, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
			m_inscatterT[1].dimension = TextureDimension.Tex3D;
            m_inscatterT[1].enableRandomWrite = true;
			m_inscatterT[1].volumeDepth = INSCATTER_R;
			m_inscatterT[1].Create();

			m_deltaET = new RenderTexture(IRRADIANCE_W, IRRADIANCE_H, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
			m_deltaET.enableRandomWrite = true;
			m_deltaET.Create();
			                   
			m_deltaSRT = new RenderTexture(INSCATTER_MU_S * INSCATTER_NU, INSCATTER_MU, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
			m_deltaSRT.dimension = TextureDimension.Tex3D;
            m_deltaSRT.enableRandomWrite = true;
			m_deltaSRT.volumeDepth = INSCATTER_R;
			m_deltaSRT.Create();

			m_deltaSMT = new RenderTexture(INSCATTER_MU_S * INSCATTER_NU, INSCATTER_MU, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
			m_deltaSMT.dimension = TextureDimension.Tex3D;
            m_deltaSMT.enableRandomWrite = true;
			m_deltaSMT.volumeDepth = INSCATTER_R;
			m_deltaSMT.Create();

			m_deltaJT = new RenderTexture(INSCATTER_MU_S * INSCATTER_NU, INSCATTER_MU, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
			m_deltaJT.dimension = TextureDimension.Tex3D;
            m_deltaJT.enableRandomWrite = true;
			m_deltaJT.volumeDepth = INSCATTER_R;
			m_deltaJT.Create();

            try
            {

                SetParameters(m_copyInscatter1);
                SetParameters(m_copyInscatterN);
                SetParameters(m_copyIrradiance);
                SetParameters(m_inscatter1);
                SetParameters(m_inscatterN);
                SetParameters(m_inscatterS);
                SetParameters(m_irradiance1);
                SetParameters(m_irradianceN);
                SetParameters(m_transmittance);

                m_step = 0;
                m_order = 2;

                ClearColor(m_irradianceT);

                while (!m_finished)
                {
                    Preprocess();
                }

                AssetDatabase.Refresh();
            }
            catch(Exception e)
            {
                Debug.Log("Create atmoshere tables failed.");
                Debug.Log(e);
            }
            finally
            {
                Destroy();
            }

		}

		void SetParameters(ComputeShader mat)
		{
			mat.SetFloat("Rg", Rg);
			mat.SetFloat("Rt", Rt);
			mat.SetFloat("RL", RL);
			mat.SetInt("TRANSMITTANCE_W", TRANSMITTANCE_W);
			mat.SetInt("TRANSMITTANCE_H", TRANSMITTANCE_H);
			mat.SetInt("SKY_W", IRRADIANCE_W);
			mat.SetInt("SKY_H", IRRADIANCE_H);
			mat.SetInt("RES_R", INSCATTER_R);
			mat.SetInt("RES_MU", INSCATTER_MU);
			mat.SetInt("RES_MU_S", INSCATTER_MU_S);
			mat.SetInt("RES_NU", INSCATTER_NU);
			mat.SetFloat("AVERAGE_GROUND_REFLECTANCE", AVERAGE_GROUND_REFLECTANCE);
			mat.SetFloat("HR", HR);
			mat.SetFloat("HM", HM);
			mat.SetVector("betaR", BETA_R);
			mat.SetVector("betaMSca", BETA_MSca);
			mat.SetVector("betaMEx", BETA_MEx);
			mat.SetFloat("mieG", Mathf.Clamp(MIE_G, 0.0f, 0.99f));
		}

		void Preprocess()
		{
			if (m_step == 0) 
			{
				// computes transmittance texture T (line 1 in algorithm 4.1)
				m_transmittance.SetTexture(0, "transmittanceWrite", m_transmittanceT);
				m_transmittance.Dispatch(0, TRANSMITTANCE_W/NUM_THREADS, TRANSMITTANCE_H/NUM_THREADS, 1);
			} 
			else if (m_step == 1) 
			{
				// computes irradiance texture deltaE (line 2 in algorithm 4.1)
				m_irradiance1.SetTexture(0, "transmittanceRead", m_transmittanceT);
				m_irradiance1.SetTexture(0, "deltaEWrite", m_deltaET);
				m_irradiance1.Dispatch(0, IRRADIANCE_W/NUM_THREADS, IRRADIANCE_H/NUM_THREADS, 1);

				if(WRITE_DEBUG_TEX)
					SaveAs8bit(IRRADIANCE_W, IRRADIANCE_H, 4, "/deltaE_debug", m_deltaET);
			} 
			else if (m_step == 2) 
			{
				// computes single scattering texture deltaS (line 3 in algorithm 4.1)
				// Rayleigh and Mie separated in deltaSR + deltaSM
				m_inscatter1.SetTexture(0, "transmittanceRead", m_transmittanceT);
				m_inscatter1.SetTexture(0, "deltaSRWrite", m_deltaSRT);
				m_inscatter1.SetTexture(0, "deltaSMWrite", m_deltaSMT);

				//The inscatter calc's can be quite demanding for some cards so process 
				//the calc's in layers instead of the whole 3D data set.
				for(int i = 0; i < INSCATTER_R; i++) {
					m_inscatter1.SetInt("layer", i);
					m_inscatter1.Dispatch(0, (INSCATTER_MU_S*INSCATTER_NU)/NUM_THREADS, INSCATTER_MU/NUM_THREADS, 1);
				}

				if(WRITE_DEBUG_TEX)
					SaveAs8bit(INSCATTER_MU_S*INSCATTER_NU, INSCATTER_MU*INSCATTER_R, 4, "/deltaSR_debug", m_deltaSRT);

				if(WRITE_DEBUG_TEX)
					SaveAs8bit(INSCATTER_MU_S*INSCATTER_NU, INSCATTER_MU*INSCATTER_R, 4, "/deltaSM_debug", m_deltaSMT);
			} 
			else if (m_step == 3) 
			{
				// copies deltaE into irradiance texture E (line 4 in algorithm 4.1)
				m_copyIrradiance.SetFloat("k", 0.0f);
				m_copyIrradiance.SetTexture(0, "deltaERead", m_deltaET);
				m_copyIrradiance.SetTexture(0, "irradianceRead", m_irradianceT[READ]);
				m_copyIrradiance.SetTexture(0, "irradianceWrite", m_irradianceT[WRITE]);
				m_copyIrradiance.Dispatch(0, IRRADIANCE_W/NUM_THREADS, IRRADIANCE_H/NUM_THREADS, 1);

				Swap(m_irradianceT);
			} 
			else if (m_step == 4) 
			{
				// copies deltaS into inscatter texture S (line 5 in algorithm 4.1)
				m_copyInscatter1.SetTexture(0, "deltaSRRead", m_deltaSRT);
				m_copyInscatter1.SetTexture(0, "deltaSMRead", m_deltaSMT);
				m_copyInscatter1.SetTexture(0, "inscatterWrite", m_inscatterT[WRITE]);

				//The inscatter calc's can be quite demanding for some cards so process 
				//the calc's in layers instead of the whole 3D data set.
				for(int i = 0; i < INSCATTER_R; i++) {
					m_copyInscatter1.SetInt("layer", i);
					m_copyInscatter1.Dispatch(0, (INSCATTER_MU_S*INSCATTER_NU)/NUM_THREADS, INSCATTER_MU/NUM_THREADS, 1);
				}

				Swap(m_inscatterT);
			} 
			else if (m_step == 5) 
			{
				// computes deltaJ (line 7 in algorithm 4.1)
				m_inscatterS.SetInt("first", (m_order == 2) ? 1 : 0);
				m_inscatterS.SetTexture(0, "transmittanceRead", m_transmittanceT);
				m_inscatterS.SetTexture(0, "deltaERead", m_deltaET);
				m_inscatterS.SetTexture(0, "deltaSRRead", m_deltaSRT);
				m_inscatterS.SetTexture(0, "deltaSMRead", m_deltaSMT);
				m_inscatterS.SetTexture(0, "deltaJWrite", m_deltaJT);

                //The inscatter calc's can be quite demanding for some cards so process 
                //the calc's in layers instead of the whole 3D data set.
                for (int i = 0; i < INSCATTER_R; i++) {
					m_inscatterS.SetInt("layer", i);
					m_inscatterS.Dispatch(0, (INSCATTER_MU_S*INSCATTER_NU)/NUM_THREADS, INSCATTER_MU/NUM_THREADS, 1);
				}
			} 
			else if (m_step == 6) 
			{
				// computes deltaE (line 8 in algorithm 4.1)
				m_irradianceN.SetInt("first", (m_order == 2) ? 1 : 0);
				m_irradianceN.SetTexture(0, "deltaSRRead", m_deltaSRT);
				m_irradianceN.SetTexture(0, "deltaSMRead", m_deltaSMT);
				m_irradianceN.SetTexture(0, "deltaEWrite", m_deltaET);
				m_irradianceN.Dispatch(0, IRRADIANCE_W/NUM_THREADS, IRRADIANCE_H/NUM_THREADS, 1);
			} 
			else if (m_step == 7) 
			{
				// computes deltaS (line 9 in algorithm 4.1)
				m_inscatterN.SetTexture(0, "transmittanceRead", m_transmittanceT);
				m_inscatterN.SetTexture(0, "deltaJRead", m_deltaJT);
				m_inscatterN.SetTexture(0, "deltaSRWrite", m_deltaSRT);

				//The inscatter calc's can be quite demanding for some cards so process 
				//the calc's in layers instead of the whole 3D data set.
				for(int i = 0; i < INSCATTER_R; i++) {
					m_inscatterN.SetInt("layer", i);
					m_inscatterN.Dispatch(0, (INSCATTER_MU_S*INSCATTER_NU)/NUM_THREADS, INSCATTER_MU/NUM_THREADS, 1);
				}
			} 
			else if (m_step == 8) 
			{
				// adds deltaE into irradiance texture E (line 10 in algorithm 4.1)
				m_copyIrradiance.SetFloat("k", 1.0f);
				m_copyIrradiance.SetTexture(0, "deltaERead", m_deltaET);
				m_copyIrradiance.SetTexture(0, "irradianceRead", m_irradianceT[READ]);
				m_copyIrradiance.SetTexture(0, "irradianceWrite", m_irradianceT[WRITE]);
				m_copyIrradiance.Dispatch(0, IRRADIANCE_W/NUM_THREADS, IRRADIANCE_H/NUM_THREADS, 1);
				
				Swap(m_irradianceT);
			} 
			else if (m_step == 9) 
			{

				// adds deltaS into inscatter texture S (line 11 in algorithm 4.1)
				m_copyInscatterN.SetTexture(0, "deltaSRead", m_deltaSRT);
				m_copyInscatterN.SetTexture(0, "inscatterRead", m_inscatterT[READ]);
				m_copyInscatterN.SetTexture(0, "inscatterWrite", m_inscatterT[WRITE]);

				//The inscatter calc's can be quite demanding for some cards so process 
				//the calc's in layers instead of the whole 3D data set.
				for(int i = 0; i < INSCATTER_R; i++) {
					m_copyInscatterN.SetInt("layer", i);
					m_copyInscatterN.Dispatch(0, (INSCATTER_MU_S*INSCATTER_NU)/NUM_THREADS, INSCATTER_MU/NUM_THREADS, 1);
				}

				Swap(m_inscatterT);

				if (m_order < 4) {
					m_step = 4;
					m_order += 1;
				}
			} 
			else if (m_step == 10) 
			{
				SaveAsRaw(TRANSMITTANCE_W * TRANSMITTANCE_H, 3, "/transmittance", m_transmittanceT);

				SaveAsRaw(IRRADIANCE_W * IRRADIANCE_H, 3, "/irradiance", m_irradianceT[READ]);
	
				SaveAsRaw((INSCATTER_MU_S*INSCATTER_NU) * INSCATTER_MU * INSCATTER_R, 4, "/inscatter", m_inscatterT[READ]);

				if(WRITE_DEBUG_TEX)
				{
					SaveAs8bit(TRANSMITTANCE_W, TRANSMITTANCE_H, 4, "/transmittance_debug", m_transmittanceT);

					SaveAs8bit(IRRADIANCE_W, IRRADIANCE_H, 4, "/irradiance_debug", m_irradianceT[READ]);

					SaveAs8bit(INSCATTER_MU_S*INSCATTER_NU, INSCATTER_MU*INSCATTER_R, 4, "/inscater_debug", m_inscatterT[READ]);

                    SaveAs8bit(INSCATTER_MU_S * INSCATTER_NU, INSCATTER_MU * INSCATTER_R, 4, "/deltaJ_debug", m_deltaJT);
                }
			} 
			else if (m_step == 11) 
			{
				m_finished = true;
				Debug.Log("Preprocess done. Files saved to - " + m_filePath);
			}

			m_step += 1;
		}

		private void Destroy()
		{
            RenderTexture.active = null;

			DestroyImmediate(m_transmittanceT);
            DestroyImmediate(m_irradianceT[0]);
            DestroyImmediate(m_irradianceT[1]);
            DestroyImmediate(m_inscatterT[0]);
            DestroyImmediate(m_inscatterT[1]);
            DestroyImmediate(m_deltaET);
            DestroyImmediate(m_deltaSRT);
            DestroyImmediate(m_deltaSMT);
            DestroyImmediate(m_deltaJT);
		}

        private void Swap(RenderTexture[] texs)
        {
            RenderTexture temp = texs[0];
            texs[0] = texs[1];
            texs[1] = temp;
        }

        private void ClearColor(RenderTexture[] texs)
        {
            Graphics.SetRenderTarget(texs[0]);
            GL.Clear(false, true, Color.clear);

            Graphics.SetRenderTarget(texs[1]);
            GL.Clear(false, true, Color.clear);
        }

        /// <summary>
        /// Save the actual data in table as a raw file to be loaded and used during run time.
        /// </summary>
        private void SaveAsRaw(int size, int channels, string fileName, RenderTexture rtex)
		{
			ComputeBuffer buffer = new ComputeBuffer(size, sizeof(float)*channels);
			
			CBUtility.ReadFromRenderTexture(rtex, channels, buffer, m_readData);
			
			float[] data = new float[size * channels];
			
			buffer.GetData(data);

			byte[] byteArray = new byte[size * 4 * channels];
			System.Buffer.BlockCopy(data, 0, byteArray, 0, byteArray.Length);
			System.IO.File.WriteAllBytes(Application.dataPath + m_filePath + fileName + ".raw", byteArray);
			
			buffer.Release();
		}

        /// <summary>
        /// Saves a 8 bit version of the table.
        /// Only used to get a visible image for debugging.
        /// </summary>
        private void SaveAs8bit(int width, int height, int channels, string fileName, RenderTexture rtex, float scale = 1.0f)
		{
			ComputeBuffer buffer = new ComputeBuffer(width*height, sizeof(float)*channels);
			
			CBUtility.ReadFromRenderTexture(rtex, channels, buffer, m_readData);
			
			float[] data = new float[width*height* channels];
			
			buffer.GetData(data);

			Texture2D tex = new Texture2D(width, height);

			for(int x = 0; x < width; x++)
			{
				for(int y = 0; y < height; y++)
				{
					Color col = new Color(0,0,0,1);

					col.r = data[(x + y * width) * channels + 0];

					if(channels > 1)
						col.g = data[(x + y * width) * channels + 1];

					if(channels > 2)
						col.b = data[(x + y * width) * channels + 2];

					tex.SetPixel(x, y, col * scale);
				}
			}

			tex.Apply();

			byte[] bytes = tex.EncodeToPNG();

			System.IO.File.WriteAllBytes(Application.dataPath + m_filePath + fileName + ".png", bytes);

			buffer.Release();

		}

	}

}















