
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
 */

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

using Common.Mathematics.LinearAlgebra;
using Common.Geometry.Shapes;

namespace Proland
{
	/// <summary>
	/// Provides a framework to draw and update view-dependent, quadtree based terrains.
	/// This framework provides classes to represent the terrain quadtree, classes to
	/// associate data produced by a TileProducer to the quads of this
	/// quadtree, as well as classes to update and draw such terrains (which can be
	/// deformed to get spherical).
	///
	/// A view dependent, quadtree based terrain. This class provides access to the
	/// terrain quadtree, defines the terrain deformation (can be used to get planet
	/// sized terrains), and defines how the terrain quadtree must be subdivided based
	/// on the viewer position. This class does not give any direct or indirect access
	/// to the terrain data (elevations, normals, texture, etc). The terrain data must
	/// be managed by TileProducer, and stored in TileStorage. 
	/// The link between with the terrain quadtree is provided by the TileSampler class.
	/// </summary>
	public class TerrainNode : Node
	{

		private readonly static int HORIZON_SIZE = 256;

        /// <summary>
        /// True to perform horizon occlusion culling tests.
        /// </summary>
        public bool HorizonCulling { get; private set; }

        /// <summary>
        /// True to subdivide invisible quads based on distance, like visible ones.
        /// </summary>
        public bool SplitInvisibleQuads { get; private set; }

        /// <summary>
        /// The deformation of this terrain. In the terrain local space the
        /// terrain sea level surface is flat. In the terrain deformed space
        /// the sea level surface can be spherical (or flat if the
        /// identity deformation is used).
        /// </summary>
        public Deformation Deform { get; private set; }

        /// <summary>
        /// The root of the terrain quadtree. This quadtree is subdivided based on the
        /// current viewer position by the update method.
        /// </summary>
        public TerrainQuad Root { get; private set; }

        /// <summary>
        /// The current viewer position in the deformed terrain space.
        /// </summary>
        public Vector3d DeformedCameraPos { get; private set; }

        /// <summary>
        /// The current viewer frustum planes in the deformed terrain space.
        /// </summary>
        public Vector4d[] DeformedFrustumPlanes { get; private set; }

        /// <summary>
        /// The current viewer position in the local terrain space.
        /// </summary>
        public Vector3d LocalCameraPos { get; private set; }

        /// <summary>
        /// The viewer distance at which a quad is subdivided, relatively to the quad size.
        /// </summary>
        public float SplitDist { get; private set; }

        /// <summary>
        /// The ratio between local and deformed lengths at localCameraPos.
        /// </summary>
        public float DistFactor { get; private set; }

        /// <summary>
        /// Local reference frame used to compute horizon occlusion culling.
        /// </summary>
        public Matrix2x2d LocalCameraDir { get; private set; }

        /// <summary>
        /// double precision local to world matrix
        /// </summary>
        public Matrix4x4d LocalToWorld { get; private set; }

        /// <summary>
        /// The rotation of the face to object space
        /// </summary>
        public Matrix4x4d FaceToLocal { get; private set; }

        public int MaxLevel { get { return m_maxLevel; } }

        public Material Material { get { return m_terrainMaterial; } }

        public int Face { get { return m_face; } }

		/// <summary>
        /// The material used by this terrain node
		/// </summary>
		[SerializeField]
        private Material m_terrainMaterial;
		
        /// <summary>
        /// Describes how the terrain quadtree must be subdivided based on the viewer
		/// distance. For a field of view of 80 degrees, and a viewport width of 1024
		/// pixels, a quad of size L will be subdivided into subquads if the viewer
		/// distance is less than splitFactor * L. For a smaller field of view and/or
		/// a larger viewport, the quad will be subdivided at a larger distance, so
		/// that its size in pixels stays more or less the same. This number must be
		/// strictly larger than 1.
        /// </summary>
		[SerializeField]
        private float m_splitFactor = 2.0f;
		
        /// <summary>
        /// The maximum level at which the terrain quadtree must be subdivided (inclusive).
		/// The terrain quadtree will never be subdivided beyond this level, even if the
		/// viewer comes very close to the terrain.
        /// </summary>
		[SerializeField]
        private int m_maxLevel = 16;

		/// <summary>
        /// The terrain quad half size (only use on start up).
		/// </summary>
		[SerializeField]
        private float m_size = 50000.0f; 

		/// <summary>
        /// The terrain quad zmin (only use on start up).
		/// </summary>
		[SerializeField]
        private float m_zmin = -5000.0f;

		/// <summary>
        /// The terrain quad zmax (only use on start up).
		/// </summary>
		[SerializeField]
        private float m_zmax = 5000.0f;

		/// <summary>
        /// Which face of the cube this terrain is for planets. Is 0 for terrains.
		/// </summary>
		[SerializeField]
        private int m_face = 0;

        /// <summary>
        /// Rasterized horizon elevation angle for each azimuth angle.
        /// </summary>
        private float[] m_horizon = new float[HORIZON_SIZE];
		
		protected override void Start () 
		{
			base.Start();

            SplitDist = 1.1f;
            HorizonCulling = true;
            DeformedFrustumPlanes = new Vector4d[6];

            World.SkyNode.InitUniforms(m_terrainMaterial);

            FaceToLocal = CalculateFaceToLocal(m_face);
			LocalToWorld = /*Matrix4x4d.ToMatrix4x4d(transform.localToWorldMatrix) * */  FaceToLocal;
		
			float size = m_size;
			if(World.IsDeformed) 
            {
				size = World.Radius;
				Deform = new SphericalDeformation(size);
			}
			else
				Deform = new Deformation();

			Root = new TerrainQuad(this, null, 0, 0, -size, -size, 2.0 * size, m_zmin, m_zmax);
		
		}
		
		public void UpdateNode() 
		{
			LocalToWorld = /*Matrix4x4d.ToMatrix4x4d(transform.localToWorldMatrix) * */ FaceToLocal;

			Matrix4x4d localToCamera = View.WorldToCamera * LocalToWorld;
			Matrix4x4d localToScreen = View.CameraToScreen * localToCamera;
			Matrix4x4d invLocalToCamera = localToCamera.Inverse;
			
			DeformedCameraPos = invLocalToCamera * (new Vector3d(0));

			Frustum3d.SetPlanes(DeformedFrustumPlanes, localToScreen);
	    	LocalCameraPos = Deform.DeformedToLocal(DeformedCameraPos);
			
			Matrix4x4d m = Deform.LocalToDeformedDifferential(LocalCameraPos, true);
	    	DistFactor = (float)Math.Max( (new Vector3d(m[0,0], m[1,0], m[2,0])).Magnitude, (new Vector3d(m[0,1], m[1,1], m[2,1])).Magnitude);

	    	Vector3d left = DeformedFrustumPlanes[0].xyz.Normalized;
	    	Vector3d right = DeformedFrustumPlanes[1].xyz.Normalized;
			
	    	float fov = (float)MathUtility.Safe_Acos(-Vector3d.Dot(left, right));
			float width = (float)Screen.width;
			
			SplitDist = m_splitFactor * width / 1024.0f * Mathf.Tan(40.0f * Mathf.Deg2Rad) / Mathf.Tan(fov / 2.0f);
			
	    	if (SplitDist < 1.1f || !MathUtility.IsFinite(SplitDist)) {
	        	SplitDist = 1.1f;
	    	}

			// initializes data structures for horizon occlusion culling
		    if (HorizonCulling && LocalCameraPos.z <= Root.ZMax) 
			{
		        Vector3d deformedDir = invLocalToCamera * (new Vector3d(0,0,1));
		        Vector2d localDir = (Deform.DeformedToLocal(deformedDir) - LocalCameraPos).xy.Normalized;
				
		        LocalCameraDir = new Matrix2x2d(localDir.y, -localDir.x, -localDir.x, -localDir.y);
				
		        for(int i = 0; i < HORIZON_SIZE; ++i) {
		            m_horizon[i] = float.NegativeInfinity;
		        }
		    }
		
			Root.Update();

			World.SkyNode.SetUniforms(m_terrainMaterial);
			World.SunNode.SetUniforms(m_terrainMaterial);
			World.SetUniforms(m_terrainMaterial);
			Deform.SetUniforms(this, m_terrainMaterial);

			if(World.OceanNode != null)
			   World.OceanNode.SetUniforms(m_terrainMaterial);
			else
				m_terrainMaterial.SetFloat("_Ocean_DrawBRDF", 0.0f);

		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="quad"></param>
        /// <param name="matPropertyBlock"></param>
        public void SetPerQuadUniforms(TerrainQuad quad, MaterialPropertyBlock matPropertyBlock)
        {
            Deform.SetUniforms(this, quad, matPropertyBlock);
        }

		/// <summary>
        /// Returns the visibility of the given bounding box from the current viewer position. 
		/// This visibility is computed with Deformation::GetVisbility.
		/// </summary>
		/// <param name="localBox"></param>
		/// <returns></returns>
		public FRUSTUM_VISIBILTY GetVisibility(Box3d localBox) 
        {
			return Deform.GetVisibility(this, localBox);
		}
		
		/// <summary>
		/// Returns true if the given bounding box is occluded by the bounding boxes previously added by AddOccluder().
		/// </summary>
		/// <param name="box">a bounding box in local (i.e. non deformed) coordinates.</param>
		/// <returns>true if the given bounding box is occluded by the bounding boxes
		/// previously added as occluders by AddOccluder.</returns>
	    public bool IsOccluded(Box3d box)
		{

			if (!HorizonCulling || LocalCameraPos.z > Root.ZMax) {
		        return false;
		    }
			
		    Vector2d[] corners = new Vector2d[4];
		    Vector2d o = LocalCameraPos.xy;
			
		    corners[0] = LocalCameraDir * (new Vector2d(box.Min.x, box.Min.y) - o);
		    corners[1] = LocalCameraDir * (new Vector2d(box.Min.x, box.Max.y) - o);
		    corners[2] = LocalCameraDir * (new Vector2d(box.Max.x, box.Min.y) - o);
		    corners[3] = LocalCameraDir * (new Vector2d(box.Max.x, box.Max.y) - o);
			
		    if (corners[0].y <= 0.0 || corners[1].y <= 0.0 || corners[2].y <= 0.0 || corners[3].y <= 0.0) {
		        return false;
		    }
			
		    double dz = box.Max.z - LocalCameraPos.z;
			
		    corners[0] = new Vector2d(corners[0].x, dz) / corners[0].y;
		    corners[1] = new Vector2d(corners[1].x, dz) / corners[1].y;
		    corners[2] = new Vector2d(corners[2].x, dz) / corners[2].y;
		    corners[3] = new Vector2d(corners[3].x, dz) / corners[3].y;
			
		    double xmin = Math.Min(Math.Min(corners[0].x, corners[1].x), Math.Min(corners[2].x, corners[3].x)) * 0.33 + 0.5;
		    double xmax = Math.Max(Math.Max(corners[0].x, corners[1].x), Math.Max(corners[2].x, corners[3].x)) * 0.33 + 0.5;
		    double zmax = Math.Max(Math.Max(corners[0].y, corners[1].y), Math.Max(corners[2].y, corners[3].y));
			
		    int imin = Math.Max( (int)Math.Floor(xmin * HORIZON_SIZE), 0 );
		    int imax = Math.Min( (int)Math.Ceiling(xmax * HORIZON_SIZE), HORIZON_SIZE - 1 );
			
		    for (int i = imin; i <= imax; ++i) {
		        if (zmax > m_horizon[i]) {
		            return false;
		        }
		    }
			
		    return (imax >= imin);
		}
		
		/// <summary>
		/// Adds the given bounding box as an occluder. The bounding boxes must
		/// be added in front to back order.
		/// </summary>
		/// <param name="occluder">a bounding box in local (i.e. non deformed) coordinates.</param>
		/// <returns>true if the given bounding box is occluded by the bounding boxes
		/// previously added as occluders by this method.</returns>
		public bool AddOccluder(Box3d occluder)
		{

		    if (!HorizonCulling || LocalCameraPos.z > Root.ZMax) {
		        return false;
		    }
			
		    Vector2d[] corners = new Vector2d[4];
		    Vector2d o = LocalCameraPos.xy;
		    corners[0] = LocalCameraDir * (new Vector2d(occluder.Min.x, occluder.Min.y) - o);
		    corners[1] = LocalCameraDir * (new Vector2d(occluder.Min.x, occluder.Max.y) - o);
		    corners[2] = LocalCameraDir * (new Vector2d(occluder.Max.x, occluder.Min.y) - o);
		    corners[3] = LocalCameraDir * (new Vector2d(occluder.Max.x, occluder.Max.y) - o);
			
		    if (corners[0].y <= 0.0 || corners[1].y <= 0.0 || corners[2].y <= 0.0 || corners[3].y <= 0.0) {
		        // skips bounding boxes that are not fully behind the "near plane"
		        // of the reference frame used for horizon occlusion culling
		        return false;
		    }
			
		    double dzmin = occluder.Min.z - LocalCameraPos.z;
		    double dzmax = occluder.Max.z- LocalCameraPos.z;
		    Vector3d[] bounds = new Vector3d[4];
			
		    bounds[0] = new Vector3d(corners[0].x, dzmin, dzmax) / corners[0].y;
		    bounds[1] = new Vector3d(corners[1].x, dzmin, dzmax) / corners[1].y;
		    bounds[2] = new Vector3d(corners[2].x, dzmin, dzmax) / corners[2].y;
		    bounds[3] = new Vector3d(corners[3].x, dzmin, dzmax) / corners[3].y;
			
		    double xmin = Math.Min(Math.Min(bounds[0].x, bounds[1].x), Math.Min(bounds[2].x, bounds[3].x)) * 0.33 + 0.5;
		    double xmax = Math.Max(Math.Max(bounds[0].x, bounds[1].x), Math.Max(bounds[2].x, bounds[3].x)) * 0.33 + 0.5;
		    double zmin = Math.Min(Math.Min(bounds[0].y, bounds[1].y), Math.Min(bounds[2].y, bounds[3].y));
		    double zmax = Math.Max(Math.Max(bounds[0].z, bounds[1].z), Math.Max(bounds[2].z, bounds[3].z));
		
		    int imin = Math.Max((int)Math.Floor(xmin * HORIZON_SIZE), 0);
		    int imax = Math.Min((int)Math.Ceiling(xmax * HORIZON_SIZE), HORIZON_SIZE - 1);
		
		    // first checks if the bounding box projection is below the current horizon line
		    bool occluded = (imax >= imin);
		    for (int i = imin; i <= imax; ++i) {
		        if (zmax > m_horizon[i]) {
		            occluded = false;
		            break;
		        }
		    }
		    if (!occluded) {
		        // if it is not, updates the horizon line with the projection of this bounding box
		        imin = Math.Max((int)Math.Ceiling(xmin * HORIZON_SIZE), 0);
		        imax = Math.Min((int)Math.Floor(xmax * HORIZON_SIZE), HORIZON_SIZE - 1);
				
		        for (int i = imin; i <= imax; ++i) {
		            m_horizon[i] = (float)Math.Max(m_horizon[i], zmin);
		        }
		    }
		    return occluded;
		}
		
		/// <summary>
		/// Returns the distance between the current viewer position and the
		/// given bounding box. This distance is measured in the local terrain
		/// space (with Deformation::GetLocalDist), with altitudes divided by
		/// GetDistFactor() to take deformations into account.
		/// </summary>
	    public double GetCameraDist(Box3d localBox)
		{
			return 	Math.Max(Math.Abs(LocalCameraPos.z - localBox.Max.z) / DistFactor,
			        Math.Max(Math.Min(Math.Abs(LocalCameraPos.x - localBox.Min.x), Math.Abs(LocalCameraPos.x - localBox.Max.x)),
			        Math.Min(Math.Abs(LocalCameraPos.y - localBox.Min.y), Math.Abs(LocalCameraPos.y - localBox.Max.y))));
		}
        
        /// <summary>
        /// If this terrain is deformed into a sphere the face matrix is the rotation of the 
        /// terrain needed to make up the spherical planet. 
        /// In this case there should be 6 terrains, each with a unique face number
        /// </summary>
        /// <returns></returns>
        private Matrix4x4d CalculateFaceToLocal(int face)
        {

            Vector3d[] faces = new Vector3d[]
            {  
                new Vector3d(0,0,0), new Vector3d(90,0,0), new Vector3d(90,90,0), 
				new Vector3d(90,180,0), new Vector3d(90,270,0), new Vector3d(0,180,180)
            };

            Matrix4x4d faceToLocal = Matrix4x4d.Identity;

            if (face - 1 >= 0 && face - 1 < 6)
                faceToLocal = RotateFace(faces[face - 1]);

            return faceToLocal;
        }

        private Matrix4x4d RotateFace(Vector3d rotation)
        {
            Quaternion3d x = new Quaternion3d(new Vector3d(1, 0, 0), rotation.x * MathUtility.Deg2Rad);
            Quaternion3d y = new Quaternion3d(new Vector3d(0, 1, 0), rotation.y * MathUtility.Deg2Rad);
            Quaternion3d z = new Quaternion3d(new Vector3d(0, 0, 1), rotation.z * MathUtility.Deg2Rad);

            return (z * y * x).ToMatrix4x4d();
        }

    }
}





















