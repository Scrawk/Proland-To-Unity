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
 * Modified and ported to Unity by Justin Hawkins 2014
 */

using UnityEngine;
using System.Collections;
using System;

using Common.Mathematics.LinearAlgebra;
using Common.Unity.Mathematics;

namespace Proland
{

    /// <summary>
    /// A TerrainView for spherical terrains (i.e., planets). This subclass
    /// interprets the x0 and y0 fields as longitudes and latitudes on the planet,
    /// and considers theta and phi as relative to the tangent plane at the(x0, y0) point.
    /// </summary>
	[RequireComponent(typeof(Camera))]
	public class PlanetView : TerrainView
	{
        /// <summary>
        /// The radius of the planet at sea level. This is the size value of the terrain nodes.
        /// Set by the world script.
        /// </summary>
        public double Radius { get; set; }

        public override double Height {  get { return WorldCameraPos.Magnitude - Radius; } }

        /// <summary>
        /// returns the position the camera is currently looking at
        /// </summary>
        public override Vector3d LookAtPos
		{
            get
            {
                double co = Math.Cos(m_position.x0); // x0 = longitude
                double so = Math.Sin(m_position.x0);
                double ca = Math.Cos(m_position.y0); // y0 = latitude
                double sa = Math.Sin(m_position.y0);

                return new Vector3d(co * ca, so * ca, sa) * Radius;
            }
		}

        /// <summary>
        /// Any contraints you need on the position are applied here
        /// </summary>
        public override void Constrain()
        {
			m_position.y0 = Math.Max(-Math.PI / 2.0, Math.Min(Math.PI / 2.0, m_position.y0));
			m_position.theta = Math.Max(0.1, Math.Min(Math.PI, m_position.theta));
			m_position.distance = Math.Max(0.1, m_position.distance);
		}

		protected override void Start()  
		{
			base.Start();
			Constrain();
		}

		protected override void SetWorldToCameraMatrix()
		{
	
			double co = Math.Cos(m_position.x0); // x0 = longitude
			double so = Math.Sin(m_position.x0);
			double ca = Math.Cos(m_position.y0); // y0 = latitude
			double sa = Math.Sin(m_position.y0);

			Vector3d po = new Vector3d(co*ca, so*ca, sa) * Radius;
			Vector3d px = new Vector3d(-so, co, 0.0);
			Vector3d py = new Vector3d(-co*sa, -so*sa, ca);
			Vector3d pz = new Vector3d(co*ca, so*ca, sa);
			
			double ct = Math.Cos(m_position.theta);
			double st = Math.Sin(m_position.theta);
			double cp = Math.Cos(m_position.phi);
			double sp = Math.Sin(m_position.phi);

			Vector3d cx = px * cp + py * sp;
			Vector3d cy = (px*-1.0) * sp*ct + py * cp*ct + pz * st;
			Vector3d cz = px * sp*st - py * cp*st + pz * ct;

            Vector3d worldPos = po + cz * m_position.distance;
			
			if (worldPos.Magnitude < Radius + 10.0 + GroundHeight)
            {
                double inv = (Radius + 10.0 + GroundHeight) / worldPos.Magnitude;
                worldPos = worldPos * inv;
			}
			
			Matrix4x4d view = new Matrix4x4d(	cx.x, cx.y, cx.z, 0.0,
			           							cy.x, cy.y, cy.z, 0.0,
			           							cz.x, cz.y, cz.z, 0.0,
			           							0.0, 0.0, 0.0, 1.0);

            Matrix4x4d worldToCamera = view * Matrix4x4d.Translate(worldPos * -1.0);

            //Flip first row to match Unitys winding order.
            worldToCamera[0,0] *= -1.0;
            worldToCamera[0,1] *= -1.0;
            worldToCamera[0,2] *= -1.0;
            worldToCamera[0,3] *= -1.0;

            WorldToCamera = worldToCamera;
            CameraToWorld = worldToCamera.Inverse;

            WorldCameraPos = worldPos;

			GetComponent<Camera>().worldToCameraMatrix = MathConverter.ToMatrix4x4(WorldToCamera);
			GetComponent<Camera>().transform.position = MathConverter.ToVector3(worldPos);
			
		}
		
		public override void Move(Vector3d oldp, Vector3d p, double speed)
		{
			Vector3d oldpos = oldp.Normalized;
			Vector3d pos = p.Normalized;

			double oldlat = MathUtility.Safe_Asin(oldpos.z);
			double oldlon = Math.Atan2(oldpos.y, oldpos.x);
			double lat = MathUtility.Safe_Asin(pos.z);
			double lon = Math.Atan2(pos.y, pos.x);

			m_position.x0 -= (lon - oldlon) * speed * Math.Max(1.0, Height);
			m_position.y0 -= (lat - oldlat) * speed * Math.Max(1.0, Height);
		}
		
		public override void MoveForward(double distance)
		{
			double co = Math.Cos(m_position.x0); // x0 = longitude
			double so = Math.Sin(m_position.x0);
			double ca = Math.Cos(m_position.y0); // y0 = latitude
			double sa = Math.Sin(m_position.y0);

			Vector3d po = new Vector3d(co*ca, so*ca, sa) * Radius;
			Vector3d px = new Vector3d(-so, co, 0.0);
			Vector3d py = new Vector3d(-co*sa, -so*sa, ca);
			Vector3d pd = (po - px * Math.Sin(m_position.phi) * distance + py * Math.Cos(m_position.phi) * distance).Normalized;

			m_position.x0 = Math.Atan2(pd.y, pd.x);
			m_position.y0 = MathUtility.Safe_Asin(pd.z);
		}

		public override void Turn(double angle)
        {
			m_position.phi += angle;
		}
		
		public override double Interpolate(	double sx0, double sy0, double stheta, double sphi, double sd,
		                           			double dx0, double dy0, double dtheta, double dphi, double dd, double t)
		{
			Vector3d s = new Vector3d(Math.Cos(sx0) * Math.Cos(sy0), Math.Sin(sx0) * Math.Cos(sy0), Math.Sin(sy0));
			Vector3d e = new Vector3d(Math.Cos(dx0) * Math.Cos(dy0), Math.Sin(dx0) * Math.Cos(dy0), Math.Sin(dy0));
			double dist = Math.Max(MathUtility.Safe_Acos(Vector3d.Dot(s, e)) * Radius, 1e-3);
			
			t = Math.Min(t + Math.Min(0.1, 5000.0 / dist), 1.0);
			double T = 0.5 * Math.Atan(4.0 * (t - 0.5)) / Math.Atan(4.0 * 0.5) + 0.5;
			
			InterpolateDirection(sx0, sy0, dx0, dy0, T, ref m_position.x0, ref m_position.y0);
			InterpolateDirection(sphi, stheta, dphi, dtheta, T, ref m_position.phi, ref m_position.theta);
			
			double W = 10.0;
			m_position.distance = sd * (1.0 - t) + dd * t + dist * (Math.Exp(-W * (t - 0.5) * (t - 0.5)) - Math.Exp(-W * 0.25));
			
			return t;
		}
		
		public override void InterpolatePos(double sx0, double sy0, double dx0, double dy0, double t, ref double x0, ref double y0)
		{
			InterpolateDirection(sx0, sy0, dx0, dy0, t, ref x0, ref y0);
		}
	}
}

















