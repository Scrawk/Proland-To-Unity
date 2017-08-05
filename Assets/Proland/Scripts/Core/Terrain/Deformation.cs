
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
using System;
using System.Collections;

using Common.Mathematics.LinearAlgebra;
using Common.Geometry.Shapes;
using Common.Unity.Mathematics;

namespace Proland
{	
	/// <summary>
	/// A deformation of space. Such a deformation maps a 3D source point to a 3D
	/// destination point. The source space is called the local space, while
	/// the destination space is called the deformed space. Source and
	/// destination points are defined with their x,y,z coordinates in an orthonormal
	/// reference frame. A Deformation is also responsible to set the shader uniforms
	/// that are necessary to project a TerrainQuad on screen, taking the deformation
	/// into account. The default implementation of this class implements the
	/// identity deformation, i.e. the deformed point is equal to the local one.
	/// </summary>
	public class Deformation 
	{
		protected DeformUniforms m_uniforms;
		protected Matrix4x4d m_localToCamera;
		protected Matrix4x4d m_localToScreen;
		protected Matrix3x3d m_localToTangent;

		public Deformation()
		{
			m_uniforms = new DeformUniforms();
			m_localToCamera = new Matrix4x4d();
			m_localToScreen = new Matrix4x4d();
			m_localToTangent = new Matrix3x3d();
		}
		
		/// <summary>
		/// Returns the deformed point corresponding to the given source point.
		/// </summary>
		/// <param name="localPt">a point in the local (i.e., source) space</param>
		/// <returns>the corresponding point in the deformed (i.e., destination) space</returns>
		public virtual Vector3d LocalToDeformed(Vector3d localPt)
		{
			return localPt;
		}

		/// <summary>
		/// Returns the differential of the deformation function at the given local
		/// point. This differential gives a linear approximation of the deformation
		/// around a given point, represented with a matrix. More precisely, if p
		/// is near localPt, then the deformed point corresponding to p can be
		/// approximated with localToDeformedDifferential(localPt) * (p - localPt).
		/// </summary>
		/// <param name="localPt">a point in the local (i.e., source) space. 
        /// The z coordinate of this point is ignored, and considered to be 0.</param>
		/// <param name="clamp"></param>
		/// <returns>the differential of the deformation function at the given local point.</returns>
		public virtual Matrix4x4d LocalToDeformedDifferential(Vector3d localPt, bool clamp = false)
		{
			return Matrix4x4d.Translate(new Vector3d(localPt.x, localPt.y, 0.0));
		}
		
		/// <summary>
		/// Returns the local point corresponding to the given source point.
		/// </summary>
		/// <param name="deformedPt">a point in the deformed (i.e., destination) space.</param>
		/// <returns>the corresponding point in the local (i.e., source) space.</returns>
		public virtual Vector3d DeformedToLocal(Vector3d deformedPt)
		{
			return deformedPt;
		}

        /// <summary>
		/// Returns the local bounding box corresponding to the given source disk.
        /// </summary>
        /// <param name="deformedCenter">the source disk center in deformed space.</param>
        /// <param name="deformedRadius">the source disk radius in deformed space.</param>
        /// <returns>the local bounding box corresponding to the given source disk.</returns>
		public virtual Box2d DeformedToLocalBounds(Vector3d deformedCenter, double deformedRadius)
		{
			return new Box2d(deformedCenter.x - deformedRadius, deformedCenter.x + deformedRadius,
			             	 deformedCenter.y - deformedRadius, deformedCenter.y + deformedRadius);
		}

		/// <summary>
		/// Returns an orthonormal reference frame of the tangent space at the given
		/// deformed point. This reference frame is such that its xy plane is the
		/// tangent plane, at deformedPt, to the deformed surface corresponding to
		/// the local plane z=0. Note that this orthonormal reference frame does
		/// not give the differential of the inverse deformation funtion,
		/// which in general is not an orthonormal transformation. If p is a deformed
		/// point, then deformedToLocalFrame(deformedPt) * p gives the coordinates of
		/// p in the orthonormal reference frame defined above.
		/// </summary>
		/// <param name="deformedPt">a point in the deformed (i.e., destination) space.</param>
		/// <returns>the orthonormal reference frame at deformedPt defined above.</returns>
		public virtual Matrix4x4d DeformedToTangentFrame(Vector3d deformedPt)
		{
			return Matrix4x4d.Translate(new Vector3d(-deformedPt.x, -deformedPt.y, 0.0));
		}

		/// <summary>
		/// Returns the distance in local (i.e., source) space between a point and a bounding box.
		/// </summary>
		/// <param name="localPt">a point in local space.</param>
		/// <param name="localBox">a bounding box in local space.</param>
		/// <returns></returns>
		public virtual double GetLocalDist(Vector3d localPt, Box3d localBox)
		{
			return Math.Max(Math.Abs(localPt.z - localBox.Max.z),
			       Math.Max(Math.Min(Math.Abs(localPt.x - localBox.Min.x), Math.Abs(localPt.x - localBox.Max.x)),
			       Math.Min(Math.Abs(localPt.y - localBox.Min.y), Math.Abs(localPt.y - localBox.Max.y))));
		}

		/// <summary>
		/// Returns the visibility of a bounding box in local space, in a view
		/// frustum defined in deformed space.
		/// </summary>
		/// <param name="node">This is node is used to get the camera position
		/// in local and deformed space with TerrainNode::GetLocalCamera and
		/// TerrainNode::GetDeformedCamera, as well as the view frustum planes
		/// in deformed space with TerrainNode::GetDeformedFrustumPlanes. </param>
		/// <param name="localBox">a bounding box in local space.</param>
		/// <returns>the visibility of the bounding box in the view frustum.</returns>
		public virtual FRUSTUM_VISIBILTY GetVisibility(TerrainNode node, Box3d localBox)
		{
			// localBox = deformedBox, so we can compare the deformed frustum with it
			return Frustum3d.GetVisibility(node.DeformedFrustumPlanes, localBox);
		}

		/// <summary>
		/// Sets the shader uniforms that are necessary to project on screen the
		/// TerrainQuad of the given TerrainNode. This method can set the uniforms
		/// that are common to all the quads of the given terrain.
		/// </summary>
		public virtual void SetUniforms(TerrainNode node, Material mat)
		{
			if(mat == null || node == null) return;

			float d1 = node.SplitDist + 1.0f;
			float d2 = 2.0f * node.SplitDist;
			mat.SetVector(m_uniforms.blending, new Vector2(d1, d2 - d1));

			m_localToCamera = node.View.WorldToCamera * node.LocalToWorld;
			m_localToScreen = node.View.CameraToScreen * m_localToCamera;

			Vector3d localCameraPos = node.LocalCameraPos;
			Vector3d worldCamera = node.View.WorldCameraPos;

			Matrix4x4d A = LocalToDeformedDifferential(localCameraPos);
			Matrix4x4d B = DeformedToTangentFrame(worldCamera);

			Matrix4x4d ltot = B * node.LocalToWorld * A;

			m_localToTangent = new Matrix3x3d(	ltot[0,0], ltot[0,1], ltot[0,3],
			                                  	ltot[1,0], ltot[1,1], ltot[1,3],
			                                  	ltot[3,0], ltot[3,1], ltot[3,3]);

			mat.SetMatrix(m_uniforms.localToScreen, MathConverter.ToMatrix4x4(m_localToScreen));
			mat.SetMatrix(m_uniforms.localToWorld, MathConverter.ToMatrix4x4(node.LocalToWorld));

		}

		/// <summary>
		/// Sets the shader uniforms that are necessary to project on screen the
		/// given TerrainQuad. This method can set the uniforms that are specific to
		/// the given quad.
		/// </summary>
		public virtual void SetUniforms(TerrainNode node, TerrainQuad quad, MaterialPropertyBlock matPropertyBlock)
		{

			if(matPropertyBlock == null || node == null || quad == null) return;

			double ox = quad.Ox;
			double oy = quad.Oy;
			double l = quad.Length;
			double distFactor = node.DistFactor;
			int level = quad.Level;

			matPropertyBlock.SetVector(m_uniforms.offset, new Vector4((float)ox, (float)oy, (float)l, (float)level));

			Vector3d camera = node.LocalCameraPos;

			matPropertyBlock.SetVector(m_uniforms.camera, new Vector4(	(float)((camera.x - ox) / l), (float)((camera.y - oy) / l),
			                                                 			(float)((camera.z - node.View.GroundHeight) / (l * distFactor)),
			                                                 			(float)camera.z));

			Vector3d c = node.LocalCameraPos;

			Matrix3x3d m = m_localToTangent * (new Matrix3x3d(l, 0.0, ox - c.x, 0.0, l, oy - c.y, 0.0, 0.0, 1.0));

			matPropertyBlock.SetMatrix(m_uniforms.tileToTangent, MathConverter.ToMatrix4x4(m));
			
			SetScreenUniforms(node, quad, matPropertyBlock);
		}

        /// <summary>
        /// 
        /// </summary>
		protected virtual void SetScreenUniforms(TerrainNode node, TerrainQuad quad, MaterialPropertyBlock matPropertyBlock)
		{

			double ox = quad.Ox;
			double oy = quad.Oy;
			double l = quad.Length;

			Vector3d p0 = new Vector3d(ox, oy, 0.0);
			Vector3d p1 = new Vector3d(ox + l, oy, 0.0);
			Vector3d p2 = new Vector3d(ox, oy + l, 0.0);
			Vector3d p3 = new Vector3d(ox + l, oy + l, 0.0);

			Matrix4x4d corners = new Matrix4x4d(p0.x, p1.x, p2.x, p3.x,
												p0.y, p1.y, p2.y, p3.y,
												p0.z, p1.z, p2.z, p3.z,
												1.0, 1.0, 1.0, 1.0);

			matPropertyBlock.SetMatrix(m_uniforms.screenQuadCorners, MathConverter.ToMatrix4x4(m_localToScreen * corners));

			Matrix4x4d verticals = new Matrix4x4d(	0.0, 0.0, 0.0, 0.0,
													0.0, 0.0, 0.0, 0.0,
													1.0, 1.0, 1.0, 1.0,
													0.0, 0.0, 0.0, 0.0);

			matPropertyBlock.SetMatrix(m_uniforms.screenQuadVerticals, MathConverter.ToMatrix4x4(m_localToScreen * verticals));

		}

        public class DeformUniforms
		{
			public int blending, localToWorld, localToScreen;
			public int offset, camera, screenQuadCorners;
			public int screenQuadVerticals, radius, screenQuadCornerNorms;
			public int tangentFrameToWorld, tileToTangent; 

			public DeformUniforms()
			{
				blending = Shader.PropertyToID("_Deform_Blending");
				localToWorld = Shader.PropertyToID("_Deform_LocalToWorld");
				localToScreen = Shader.PropertyToID("_Deform_LocalToScreen");
				offset = Shader.PropertyToID("_Deform_Offset");
				camera = Shader.PropertyToID("_Deform_Camera");
				screenQuadCorners = Shader.PropertyToID("_Deform_ScreenQuadCorners");
				screenQuadVerticals = Shader.PropertyToID("_Deform_ScreenQuadVerticals");
				radius = Shader.PropertyToID("_Deform_Radius");
				screenQuadCornerNorms = Shader.PropertyToID("_Deform_ScreenQuadCornerNorms");
				tangentFrameToWorld = Shader.PropertyToID("_Deform_TangentFrameToWorld");
				tileToTangent = Shader.PropertyToID("_Deform_TileToTangent");
			}
		}
			
	}	
}













	
