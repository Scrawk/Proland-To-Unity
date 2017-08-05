using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using Common.Geometry.Shapes;
using Common.Unity.Mathematics;
using Common.Unity.Drawing;

namespace Proland
{
	/// <summary>
	/// A manger to organise what order update functions are called, the running of tasks and the drawing of the terrain.
	/// Provides a location for common settings and allows the nodes to access each other.
	/// Also sets uniforms that are considered global.
	/// </summary>
	public class World : MonoBehaviour 
	{

		public enum DEFORM { PLANE, SPHERE };

        public int GridResolution { get { return m_gridResolution; } }

        public bool IsDeformed { get { return (m_deformType == DEFORM.SPHERE); } }

        public ComputeShader WriteData { get { return m_writeData; } }

        public ComputeShader ReadData { get { return m_readData; } }

        public float Radius { get { return m_radius; } }

        public SkyNode SkyNode { get; private set; }

        public SunNode SunNode { get; private set; }

        public OceanNode OceanNode { get; private set; }

        public Controller Controller { get; private set; }

        /// <summary>
        /// A utility shader to write data into a render texture.
        /// </summary>
		[SerializeField]
		private ComputeShader m_writeData;

        /// <summary>
        /// A utility shader to read data from a render texture.
        /// </summary>
		[SerializeField]
        private ComputeShader m_readData;

        /// <summary>
        /// The mesh resolution for a terrain tile.
        /// </summary>
		[SerializeField]
        private int m_gridResolution = 25;

        /// <summary>
        /// The exposure for the HDR.
        /// </summary>
		[SerializeField]
        private float m_HDRExposure = 0.2f;

		/// <summary>
        /// If the world is a flat plane or a sphere.
		/// </summary>
		[SerializeField]
        private DEFORM m_deformType = DEFORM.PLANE;

        /// <summary>
        /// Radius of the planent.
        /// </summary>
		[SerializeField]
        private float m_radius = 6360000.0f;

        private TerrainNode[] m_terrainNodes;

        private List<TileSampler> m_samplers;

        private Mesh m_quadMesh;

        private MaterialPropertyBlock m_propertyBlock;

        private Vector3 m_origin;

        private void Awake() 
		{

			if(IsDeformed)
				m_origin = Vector3.zero;
			else
				m_origin = new Vector3(0.0f, 0.0f, Radius);

			Controller = GetComponentInChildren<Controller>();

			//if planet view is being use set the radius
			if( Controller.View is PlanetView)
                (Controller.View as PlanetView).Radius = Radius;

			//Get the nodes that are children of the manager
			OceanNode = GetComponentInChildren<OceanNode>();
			SkyNode = GetComponentInChildren<SkyNode>();
			SunNode = GetComponentInChildren<SunNode>();
			m_terrainNodes = GetComponentsInChildren<TerrainNode>();

			m_samplers = new List<TileSampler>(GetComponentsInChildren<TileSampler>());
			m_samplers.Sort(new TileSamplerComparer());

			m_propertyBlock = new MaterialPropertyBlock();
			//make the mesh used to draw the terrain quads
			m_quadMesh = MeshFactory.MakePlane(m_gridResolution,m_gridResolution);
			m_quadMesh.bounds = new Bounds(Vector3.zero, new Vector3(1e8f, 1e8f, 1e8f));
		}

		public void SetUniforms(Material mat)
		{
			//Sets uniforms that this or other gameobjects may need
			if(mat == null) return;

			mat.SetMatrix("_Globals_WorldToCamera", MathConverter.ToMatrix4x4(Controller.View.WorldToCamera));
			mat.SetMatrix("_Globals_CameraToWorld", MathConverter.ToMatrix4x4(Controller.View.CameraToWorld));
			mat.SetMatrix("_Globals_CameraToScreen", MathConverter.ToMatrix4x4(Controller.View.CameraToScreen));
			mat.SetMatrix("_Globals_ScreenToCamera", MathConverter.ToMatrix4x4(Controller.View.ScreenToCamera));
			mat.SetVector("_Globals_WorldCameraPos", MathConverter.ToVector3(Controller.View.WorldCameraPos));
			mat.SetVector("_Globals_Origin", m_origin);
			mat.SetFloat("_Exposure", m_HDRExposure);

		}

        private void Update() 
		{

			//Update the sky, sun and controller. These node are presumed to always be present
			Controller.UpdateController();
			SunNode.UpdateNode();
			SkyNode.UpdateNode();

			//Uppdate ocean if used
			if(OceanNode != null)
				OceanNode.UpdateNode();

			//Update all the terrain nodes used and active
			foreach(TerrainNode node in m_terrainNodes)
			{
				if(node.gameObject.activeInHierarchy) 
					node.UpdateNode();
			}

			//Update all the samplers used and active
			foreach(TileSampler sampler in m_samplers)
			{
				if(sampler.gameObject.activeInHierarchy)
					sampler.UpdateSampler();
			}

			//Draw the terrain quads of each terrain node if active
			foreach(TerrainNode node in m_terrainNodes)
			{
				if(node.gameObject.activeInHierarchy)
					DrawTerrain(node);
			}

		}

		private void DrawTerrain(TerrainNode node)
		{
			//Get all the samplers attached to the terrain node. The samples contain the data need to draw the quad
			TileSampler[] allSamplers = node.transform.GetComponentsInChildren<TileSampler>();
			List<TileSampler> samplers = new List<TileSampler>();

			//Only use sample if enabled
			foreach(TileSampler sampler in allSamplers)
			{
				if(sampler.enabled && sampler.StoreLeaf)
					samplers.Add(sampler);
			}

			if(samplers.Count == 0) return;

			//Find all the quads in the terrain node that need to be drawn
			FindDrawableQuads(node.Root, samplers);
			//The draw them
			DrawQuad(node, node.Root, samplers);

		}

		/// <summary>
		/// Find all the quads in a terrain that need to be drawn. If a quad is a leaf and is visible it should
		/// be drawn. If that quads tile is not ready the first ready parent is drawn
		/// NOTE - because of the current set up all task are run on the frame they are generated so 
		/// the leaf quads will always have tiles that are ready to be drawn
		/// </summary>
        private void FindDrawableQuads(TerrainQuad quad, List<TileSampler> samplers)
		{
			quad.Drawable = false;
			
			if (!quad.IsVisible) 
            {
				quad.Drawable = true;
				return;
			}
			
			if (quad.IsLeaf) 
			{
				for ( int i = 0; i < samplers.Count; ++i)
				{
					TileProducer p = samplers[i].Producer;
					int l = quad.Level;
					int tx = quad.Tx;
					int ty = quad.Ty;

					if (p.HasTile(l, tx, ty) && p.FindTile(l, tx, ty, false, true) == null)
						return;
				}
			} 
			else 
			{
				int nDrawable = 0;
				for (int i = 0; i < 4; ++i) 
				{
					FindDrawableQuads(quad.GetChild(i), samplers);
					if (quad.GetChild(i).Drawable)
						++nDrawable;
				}

				if (nDrawable < 4) 
				{
					for (int i = 0; i < samplers.Count; ++i) 
					{
						TileProducer p = samplers[i].Producer;
						int l = quad.Level;
						int tx = quad.Tx;
						int ty = quad.Ty;
						
						if (p.HasTile(l, tx, ty) && p.FindTile(l, tx, ty, false, true) == null)
							return;
					}
				}
			}
			
			quad.Drawable = true;
		}

        private void DrawQuad(TerrainNode node, TerrainQuad quad, List<TileSampler> samplers)
		{
			if (!quad.IsVisible) return;

			if (!quad.Drawable) return;

			if (quad.IsLeaf) 
			{
				m_propertyBlock.Clear();

                //Set the unifroms needed to draw the texture for this sampler
				for (int i = 0; i < samplers.Count; ++i)
					samplers[i].SetTile(m_propertyBlock, quad.Level, quad.Tx, quad.Ty);

				//Set the uniforms unique to each quad
				node.SetPerQuadUniforms(quad, m_propertyBlock);

                //Draw the mesh
				Graphics.DrawMesh(m_quadMesh, Matrix4x4.identity, node.Material, 0, Camera.main, 0, m_propertyBlock);
			} 
			else 
			{
				//draw quads in a order based on distance to camera
				int[] order = new int[4];
				double ox = node.LocalCameraPos.x;
				double oy = node.LocalCameraPos.y;
				
				double cx = quad.Ox + quad.Length / 2.0;
				double cy = quad.Oy + quad.Length / 2.0;

				if (oy < cy) 
				{
					if (ox < cx) {
						order[0] = 0;
						order[1] = 1;
						order[2] = 2;
						order[3] = 3;
					} else {
						order[0] = 1;
						order[1] = 0;
						order[2] = 3;
						order[3] = 2;
					}
				} 
				else 
				{
					if (ox < cx) {
						order[0] = 2;
						order[1] = 0;
						order[2] = 3;
						order[3] = 1;
					} else {
						order[0] = 3;
						order[1] = 1;
						order[2] = 2;
						order[3] = 0;
					}
				}
				
				int done = 0;
				for (int i = 0; i < 4; ++i) 
				{
					if (quad.GetChild(order[i]).Visible == FRUSTUM_VISIBILTY.INVISIBLE) 
                    {
						done |= (1 << order[i]);
					} 
					else if (quad.GetChild(order[i]).Drawable) 
                    {
						DrawQuad(node, quad.GetChild(order[i]), samplers);
						done |= (1 << order[i]);
					}
				}

				if (done < 15) 
				{
					//If a leaf quad needs to be drawn but its tiles are not ready then this 
					//will draw the next parent tile instead that is ready.
					//Because of the current set up all tiles always have there tasks run on the frame they are generated
					//so this section of code is never reached

					m_propertyBlock.Clear();

                    //Set the unifroms needed to draw the texture for this sampler
					for (int i = 0; i < samplers.Count; ++i)
						samplers[i].SetTile(m_propertyBlock, quad.Level, quad.Tx, quad.Ty);
					
					//Set the uniforms unique to each quad
					node.SetPerQuadUniforms(quad, m_propertyBlock);

                    //Draw the mesh.
					Graphics.DrawMesh(m_quadMesh, Matrix4x4.identity, node.Material, 0, Camera.main, 0, m_propertyBlock);
				}
			}
		}
	}
}











































