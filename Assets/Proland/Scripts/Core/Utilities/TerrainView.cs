
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
using System;

using Common.Mathematics.LinearAlgebra;
using Common.Unity.Mathematics;

namespace Proland
{
    [Serializable]
    public class Position
    {
        /// <summary>
        /// The x and y coordinate of the point the camera is looking at on the ground.
        /// For a planet view these are the longitudes and latitudes
        /// </summary>
        public double x0, y0;

        /// <summary>
        /// The zenith angle of the vector between the "look at" point and the camera.
        /// </summary>
        public double theta;

        /// <summary>
        /// The azimuth angle of the vector between the "look at" point and the camera.
        /// </summary>
        public double phi;

        /// <summary>
        /// The distance between the "look at" point and the camera.
        /// </summary>
        public double distance;

        public override string ToString()
        {
            return string.Format("[Position: x0={0}, y0={1}, theta={2}, phi={3}, distance={4}]", x0, y0, theta, phi, distance);
        }
    };

    /// <summary>
    /// A view for flat terrains. The camera position is specified
    /// from a "look at" position (x0,y0) on ground, with a distance d between
    /// camera and this position, and two angles (theta,phi) for the direction
    /// of this vector.
    /// </summary>
    [RequireComponent(typeof(Camera))]
	[RequireComponent(typeof(Controller))]
	public class TerrainView : MonoBehaviour 
	{

		/// <summary>
        /// The x0,y0,theta,phi and distance parameters
		/// </summary>
		[SerializeField]
		protected Position m_position;

        public Position Position { get { return m_position; } }

		/// <summary>
        /// The localToWorld matrix in double precision
		/// </summary>
        public Matrix4x4d WorldToCamera { get; protected set; }

		/// <summary>
        /// The inverse world to camera matrix
		/// </summary>
        public Matrix4x4d CameraToWorld { get; protected set; }

		/// <summary>
        /// The projectionMatrix in double precision
		/// </summary>
        public Matrix4x4d CameraToScreen { get; protected set; }

		/// <summary>
        /// inverse projection matrix
		/// </summary>
        public Matrix4x4d ScreenToCamera { get; protected set; }

		/// <summary>
        /// The world camera pos
		/// </summary>
        public Vector3d WorldCameraPos { get; protected set; }

		/// <summary>
        /// The camera direction
		/// </summary>
        public Vector3d CameraDir { get; protected set; }

		/// <summary>
        /// the height below the camera of the ground.
		/// </summary>
        public double GroundHeight { get; set; }

        /// <summary>
        /// Camera height.
        /// </summary>
        public virtual double Height { get { return WorldCameraPos.z; } }

		/// <summary>
        /// returns the position the camera is currently looking at
		/// </summary>
		public virtual Vector3d LookAtPos { get { return new Vector3d(m_position.x0, m_position.y0, 0.0); } }

		/// <summary>
        /// Any contraints you need on the position are applied here
		/// </summary>
		public virtual void Constrain() 
        {
			m_position.theta = Math.Max(0.0001, Math.Min(Math.PI, m_position.theta));
			m_position.distance = Math.Max(0.1, m_position.distance);
		}
		
		protected virtual void Start() 
		{
			WorldToCamera = Matrix4x4d.Identity;
			CameraToWorld = Matrix4x4d.Identity;
			CameraToScreen = Matrix4x4d.Identity;
			ScreenToCamera = Matrix4x4d.Identity;
			WorldCameraPos = new Vector3d();
			CameraDir = new Vector3d();

			Constrain();
		}

		protected virtual void OnDestroy()
		{

		}
		
		public virtual void UpdateView() 
		{
			Constrain();

			SetWorldToCameraMatrix();
			SetProjectionMatrix();

			CameraDir = (WorldCameraPos - LookAtPos).Normalized;

            //Debug.Log(WorldCameraPos);
		}
		
		/// <summary>
		/// Computes the world to camera matrix using double precision
		/// and applies it to the camera.
		/// </summary>
		protected virtual void SetWorldToCameraMatrix()
		{

			Vector3d po = new Vector3d(m_position.x0, m_position.y0, 0.0);
		    Vector3d px = new Vector3d(1.0, 0.0, 0.0);
		    Vector3d py = new Vector3d(0.0, 1.0, 0.0);
		    Vector3d pz = new Vector3d(0.0, 0.0, 1.0);
		
			double ct = Math.Cos(m_position.theta);
			double st = Math.Sin(m_position.theta);
			double cp = Math.Cos(m_position.phi);
			double sp = Math.Sin(m_position.phi);
			
		    Vector3d cx = px * cp + py * sp;
		    Vector3d cy = (px*-1.0) * sp*ct + py * cp*ct + pz * st;
		    Vector3d cz = px * sp*st - py * cp*st + pz * ct;

            Vector3d worldPos = po + cz * m_position.distance;
		
			if (worldPos.z < GroundHeight + 10.0)
				worldPos.z = GroundHeight + 10.0;

		    Matrix4x4d view = new Matrix4x4d(	cx.x, cx.y, cx.z, 0.0,
		            							cy.x, cy.y, cy.z, 0.0,
		            							cz.x, cz.y, cz.z, 0.0,
		            							0.0, 0.0, 0.0, 1.0);

            Matrix4x4d worldToCamera = view * Matrix4x4d.Translate(worldPos * -1.0);

			worldToCamera[0,0] *= -1.0;
			worldToCamera[0,1] *= -1.0;
			worldToCamera[0,2] *= -1.0;
			worldToCamera[0,3] *= -1.0;

            WorldToCamera = worldToCamera;
			CameraToWorld = worldToCamera.Inverse;

            WorldCameraPos = worldPos;

            Camera camera = GetComponent<Camera>();

            camera.worldToCameraMatrix = MathConverter.ToMatrix4x4(WorldToCamera);
            camera.transform.position = MathConverter.ToVector3(worldPos);

        }
		
		/// <summary>
        /// Get a copy of the projection matrix and convert in to double precision
	    /// and apply the bias if using dx11 and flip Y if deferred rendering is used
        /// </summary>
        protected virtual void SetProjectionMatrix()
		{

            Camera camera = GetComponent<Camera>();

            float h = (float)(Height - GroundHeight);
            camera.nearClipPlane = 0.1f * h;
            camera.farClipPlane = 1e6f * h;

            camera.ResetProjectionMatrix();

			Matrix4x4 p = camera.projectionMatrix;
		    if(camera.actualRenderingPath == RenderingPath.DeferredShading)
                p = GL.GetGPUProjectionMatrix(p, true);
            else
                p = GL.GetGPUProjectionMatrix(p, false);

            CameraToScreen = MathConverter.ToMatrix4x4d(p);
			ScreenToCamera = CameraToScreen.Inverse;
		}

        /// <summary>
        /// Moves the "look at" point so that "oldp" appears at the position of "p" on screen.
        /// </summary>
        public virtual void Move(Vector3d oldp, Vector3d p, double speed)
		{
			m_position.x0 -= (p.x - oldp.x) * speed * Math.Max (1.0, Height);
			m_position.y0 -= (p.y - oldp.y) * speed * Math.Max (1.0, Height);
		}

		public virtual void MoveForward(double distance)
		{
			m_position.x0 -= Math.Sin(m_position.phi) * distance;
			m_position.y0 += Math.Cos(m_position.phi) * distance;
		}

		public virtual void Turn(double angle)
        {
			m_position.phi += angle;
        }

		/// <summary>
        /// Sets the position as the interpolation of the two given positions with
		/// the interpolation parameter t(between 0 and 1). The source position is
		/// sx0,sy0,stheta,sphi,sd, the destination is dx0,dy0,dtheta,dphi,dd.
        /// </summary>
        public virtual double Interpolate(	double sx0, double sy0, double stheta, double sphi, double sd,
		                           			double dx0, double dy0, double dtheta, double dphi, double dd, double t)
		{
			// TODO interpolation
			m_position.x0 = dx0;
			m_position.y0 = dy0;
			m_position.theta = dtheta;
			m_position.phi = dphi;
			m_position.distance = dd;
			return 1.0;
		}

		public virtual void InterpolatePos(double sx0, double sy0, double dx0, double dy0, double t, ref double x0, ref double y0)
		{
			x0 = sx0 * (1.0 - t) + dx0 * t;
			y0 = sy0 * (1.0 - t) + dy0 * t;
        }

		/// <summary>
        /// Returns a direction interpolated between the two given direction.
        /// </summary>
        /// <param name="slon">start longitude</param>
        /// <param name="slat">start latitude</param>
        /// <param name="elon">end longitude</param>
        /// <param name="elat">end latitude</param>
        /// <param name="t">interpolation parameter between 0 and 1</param>
        /// <param name="lon">interpolated longitude</param>
        /// <param name="lat">interpolated latitude</param>
        public virtual void InterpolateDirection(double slon, double slat, double elon, double elat, double t, ref double lon, ref double lat)
		{
			Vector3d s = new Vector3d(Math.Cos(slon) * Math.Cos(slat), Math.Sin(slon) * Math.Cos(slat), Math.Sin(slat));
			Vector3d e = new Vector3d(Math.Cos(elon) * Math.Cos(elat), Math.Sin(elon) * Math.Cos(elat), Math.Sin(elat));
			Vector3d v = (s * (1.0 - t) + e * t).Normalized;
			lat = MathUtility.Safe_Asin(v.z);
			lon = Math.Atan2(v.y, v.x);
		}
		
	}
}
















