using UnityEngine;
using System.Collections;

namespace Proland
{

	/// <summary>
	/// Provides a common interface for nodes (ie terrain node, ocean node etc)
	/// Also for tile samplers and producers. Provides access to the world so
	/// common data can be shared.
	/// </summary>
	public abstract class Node : MonoBehaviour 
	{

        protected World World { get; private set; }

		public TerrainView View { get { return World.Controller.View; } }

		protected virtual void Awake() 
        {
			FindManger();
		}

		protected virtual void Start () 
        {
			if(World == null) FindManger();
		}

		protected virtual void OnDestroy()
		{

		}

		private void FindManger()
		{
			Transform t = transform;
	
			while(t != null) 
            {
				World manager = t.GetComponent<World>();

				if(manager != null) 
                {
					World = manager;
					break;
				}

				t = t.parent;
			}

			if(World == null) 
            {
				Debug.Log("Could not find world. This gameObject must be a child of the world");
				Debug.Break();
			}

		}

	}

}
