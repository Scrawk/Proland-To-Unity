using UnityEngine;
using System.Collections;

namespace Proland
{
	/// <summary>
	/// The SunNode contains the suns direction as well as a few other settings related to the sun.
	/// Binds any uniforms to shaders that require the sun settings.
	/// NOTE - all scenes must contain a SunNode.
	/// </summary>
	public class SunNode : MonoBehaviour 
	{

        public Vector3 Direction { get { return transform.forward; } }

        public bool HasMoved { get; private set; }

        /// <summary>
        /// The rotation needed to move the sun direction back to the z axis.
        /// The sky shader requires that the sun direction is always at the z axis.
        /// </summary>
        public Matrix4x4 WorldToLocalRotation { get; private set; }

        /// <summary>
        /// The rotation needed to move the sun direction from the z axis .
        /// </summary>
        public Matrix4x4 LocalToWorldRotation { get; private set; }

		/// <summary>
        /// Dont change this.
        /// The sky shader presumes the suns local direction is this axis.
		/// </summary>
		private static readonly Vector3 Z_AXIS = new Vector3(0,0,1);

		[SerializeField]
        private Vector3 m_startSunDirection = Z_AXIS;

		[SerializeField]
        private float m_sunIntensity = 100.0f;

		void Start() 
		{
			//if the sun direction entered is (0,0,0) which is not valid, change to default
			if(m_startSunDirection.magnitude < Mathf.Epsilon)
				m_startSunDirection = Z_AXIS;

			transform.forward = m_startSunDirection.normalized;
		}

		public void SetUniforms(Material mat)
		{
			//Sets uniforms that this or other gameobjects may need
			if(mat == null) return;

			mat.SetFloat("_Sun_Intensity", m_sunIntensity);
			mat.SetVector("_Sun_WorldSunDir", Direction);
		}

		public void UpdateNode() 
		{
			//Rotate the sun when the right mouse is held down and dragged.

			HasMoved = false;

			if(Input.GetMouseButton(1))
			{
				float y = Input.GetAxis("Mouse Y");
				float x = -Input.GetAxis("Mouse X");
				transform.Rotate(new Vector3(x,y,0), Space.Self);
				HasMoved = true;
			}

			Quaternion q = Quaternion.FromToRotation(Direction, Z_AXIS);
			WorldToLocalRotation = Matrix4x4.TRS(Vector3.zero, q, Vector3.one);
            LocalToWorldRotation = WorldToLocalRotation.inverse;

		}

	}

}














