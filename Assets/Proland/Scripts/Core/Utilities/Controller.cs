
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

namespace Proland
{
	/// <summary>
	/// Controller used to collect user input and move the view (TerrainView or PlanetView)
	/// Provides smooth interpolation from the views current to new position
	/// </summary>
    [RequireComponent(typeof(TerrainView))]
	public class Controller : MonoBehaviour 
	{
        /// <summary>
        /// The view thats being controlled
        /// </summary>
        public TerrainView View
        {
            get { return m_view ?? (m_view = GetComponent<TerrainView>()); }
        }
        private TerrainView m_view;

        /// <summary>
        /// Speed settings for the different typs of movement
        /// </summary>
        [SerializeField]
		private double m_moveSpeed = 1e-3;

		[SerializeField]
        private double m_turnSpeed = 5e-3;

		[SerializeField]
        private double m_zoomSpeed = 1.0;

		[SerializeField]
        private double m_rotateSpeed = 0.1;

		[SerializeField]
        private double m_dragSpeed = 0.01;

		/// <summary>
        /// True to use exponential damping to go to target positions, false to go to target positions directly.
		/// </summary>
		[SerializeField]
        private bool m_smooth = true;

		/// <summary>
        /// True if the PAGE_DOWN key is currently pressed.
		/// </summary>
        private bool m_near;

		/// <summary>
        /// True if the PAGE_UP key is currently pressed.
		/// </summary>
        private bool m_far;

		/// <summary>
        /// True if the UP key is currently pressed.
		/// </summary>
        private bool m_forward;

		/// <summary>
        /// True if the DOWN key is currently pressed.
		/// </summary>
        private bool m_backward;

		/// <summary>
        /// True if the LEFT key is currently pressed.
		/// </summary>
        private bool m_left;

		/// <summary>
        /// True if the RIGHT key is currently pressed.
		/// </summary>
        private bool m_right;

		/// <summary>
        /// True if the target position target is initialized.
		/// </summary>
        private bool m_initialized;

		/// <summary>
        /// The target position manipulated by the user via the mouse and keyboard.
		/// </summary>
        private Position m_target;

		/// <summary>
        /// Start position for an animation between two positions.
		/// </summary>
        private Position m_start;

		/// <summary>
        /// End position for an animation between two positions.
		/// </summary>
        private Position m_end;

        private Vector3d m_previousMousePos;
		
		/// <summary>
		/// Animation status. Negative values mean no animation.
		/// 0 corresponds to the start position, 1 to the end position,
		/// and values between 0 and 1 to intermediate positions between
		/// the start and end positions.
		/// </summary>
        private double m_animation = -1.0;

        private void Start() 
		{
			m_target = new Position();
			m_start = new Position();
			m_end = new Position();
			m_previousMousePos = new Vector3d(Input.mousePosition.x, Input.mousePosition.y, Input.mousePosition.z);
		}

		public void UpdateController() 
		{
			if (!m_initialized) 
            {
				GetPosition(m_target);
				m_initialized = true;
			}

			//Check for input
			KeyDown();
			MouseWheel();
			MouseMotion();

			double dt = Time.deltaTime * 1000.0;

			//If animation requried interpolate from start to end position
			//NOTE - has not been tested and not currently used.
			if(m_animation >= 0.0) 
			{
				m_animation = View.Interpolate(m_start.x0, m_start.y0, m_start.theta, m_start.phi, m_start.distance,
				                                 m_end.x0, m_end.y0, m_end.theta, m_end.phi, m_end.distance, m_animation);
				
				if (m_animation == 1.0) 
                {
					GetPosition(m_target);
					m_animation = -1.0;
				}
			} 
			else
				UpdateController(dt);

			//Update the view so the new positions are relected in the matrices
			View.UpdateView();
		}

		private void UpdateController(double dt)
		{

			double dzFactor = Math.Pow(1.02, Math.Min(dt, 1.0));

			if(m_near)
				m_target.distance = m_target.distance / (dzFactor * m_zoomSpeed);
			else if(m_far)
				m_target.distance = m_target.distance * dzFactor * m_zoomSpeed;

			Position p = new Position();
			GetPosition(p);
			SetPosition(m_target);

			if(m_forward) 
			{
				double speed = Math.Max(View.Height, 1.0);
				View.MoveForward(speed * dt * m_moveSpeed);
			} 
			else if(m_backward) 
			{
				double speed = Math.Max(View.Height, 1.0);
				View.MoveForward(-speed * dt * m_moveSpeed);
			}

			if(m_left) 
				View.Turn(dt * m_turnSpeed);
			else if(m_right)
				View.Turn(-dt * m_turnSpeed);

			GetPosition(m_target);
					
			if(m_smooth) 
			{
				double lerp = 1.0 - Math.Exp(-dt * 2.301e-3);
				double x0 = 0.0;
				double y0 = 0.0;
				View.InterpolatePos(p.x0, p.y0, m_target.x0, m_target.y0, lerp, ref x0, ref y0);
				p.x0 = x0;
				p.y0 = y0;
				p.theta = Mix2(p.theta, m_target.theta, lerp);
				p.phi = Mix2(p.phi, m_target.phi, lerp);
				p.distance = Mix2(p.distance, m_target.distance, lerp);
				SetPosition(p);
			} 
			else 
				SetPosition(m_target);

		}

        private double Mix2(double x, double y, double t)
        {
			return Math.Abs(x - y) < Math.Max(x, y) * 1e-5 ? y : x*(1.0-t) + y*t;
		}

        private void GetPosition(Position p)
		{
			p.x0 = View.Position.x0;
			p.y0 = View.Position.y0;
			p.theta = View.Position.theta;
			p.phi = View.Position.phi;
			p.distance = View.Position.distance;
		}

        private void SetPosition(Position p)
		{
			View.Position.x0 = p.x0;
			View.Position.y0 = p.y0;
			View.Position.theta = p.theta;
			View.Position.phi = p.phi;
			View.Position.distance = p.distance;
			m_animation = -1.0;
		}

        private void GoToPosition(Position p)
		{
			GetPosition(m_start);
			m_end = p;
			m_animation = 0.0;
		}

        private void JumpToPosition(Position p)
		{
			SetPosition(p);
			m_target = p;
		}

        private void MouseWheel()
		{
			m_near = false;
			m_far = false;

			if (Input.GetAxis("Mouse ScrollWheel") < 0.0f || Input.GetKey(KeyCode.PageUp))
				m_far = true;
			
			if (Input.GetAxis("Mouse ScrollWheel") > 0.0f || Input.GetKey(KeyCode.PageDown))
				m_near = true;
			
		}

        private void KeyDown()
		{
			m_forward = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
			m_backward = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
			m_left = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
			m_right = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
		}

        private void MouseMotion()
		{

			if(Input.GetMouseButton(0) && Input.GetKey(KeyCode.LeftControl)) 
			{
				m_target.phi -= Input.GetAxis("Mouse X") * m_rotateSpeed;
				m_target.theta += Input.GetAxis("Mouse Y") * m_rotateSpeed;
			} 
			else if(Input.GetMouseButton(0)) 
			{

				Vector3d mousePos = new Vector3d();
				mousePos.x = Input.mousePosition.x;
				mousePos.y = Input.mousePosition.y;
				mousePos.z = 0.0;

				Vector3d preMousePos = new Vector3d();
				preMousePos.x = m_previousMousePos.x;
				preMousePos.y = m_previousMousePos.y;
				preMousePos.z = 0.0;

				Vector3d oldp = View.CameraToWorld * preMousePos;
				Vector3d p = View.CameraToWorld * mousePos;

				if (!(double.IsNaN(oldp.x) || double.IsNaN(oldp.y) || double.IsNaN(oldp.z) || double.IsNaN(p.x) || double.IsNaN(p.y) || double.IsNaN(p.z))) 
				{
					Position current = new Position();
					GetPosition(current);
					SetPosition(m_target);
				
					View.Move(new Vector3d(oldp.x, oldp.y, oldp.z), new Vector3d(p.x, p.y, p.z), m_dragSpeed);
					GetPosition(m_target);
					SetPosition(current);
				}
			} 

			m_previousMousePos = new Vector3d(Input.mousePosition.x, Input.mousePosition.y, Input.mousePosition.z);
		}
	}

}
























