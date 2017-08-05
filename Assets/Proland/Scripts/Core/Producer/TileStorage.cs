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
 * */

using UnityEngine;
using System.Collections.Generic;

namespace Proland
{

    /// <summary>
    /// A slot managed by a TileStorage. Concrete sub classes of this class must
    /// provide a reference to the actual tile data.
    /// </summary>
    public abstract class Slot
    {
        /// <summary>
        /// The TileStorage that manages this slot.
        /// </summary>
        public TileStorage Owner { get; private set; }

        public Slot(TileStorage owner)
        {
            Owner = owner;
        }

        /// <summary>
        /// override this if the slot needs to release data on destroy
        /// </summary>
        public virtual void Release()
        {
        }
    };

    /// <summary>
    /// A shared storage to store tiles of the same kind. This abstract class defines
	/// the behavior of tile storages but does not provide any storage itself.The
    /// slots managed by a tile storage can be used to store any tile identified by
    /// its(level, tx, ty) coordinates.This means that a Slot can store
    /// the data of some tile at some moment, and then be reused to store the data of
    /// tile some time later.The mapping between tiles and Slot is not
    /// managed by the TileStorage itself, but by a TileCache. A TileStorage just
    /// keeps track of which slots in the pool are currently associated with a
    /// tile (i.e., store the data of a tile), and which are not.The first ones are
    /// called allocated slots, the others free slots.
    /// </summary>
    [RequireComponent(typeof(TileCache))]
	public abstract class TileStorage : MonoBehaviour
    {

        public int TileSize { get { return m_tileSize; } }

        /// <summary>
        /// The total number of slots managed by this TileStorage. This includes both
        /// unused and used tiles.
        /// </summary>
        public int Capacity { get; private set; }

        /// <summary>
        /// Returns the number of slots in this TileStorage that are currently unused.
        /// </summary>
        public int FreeSlots { get { return m_freeSlots.Count; } }

        /// <summary>
        /// The size of each tile. For tiles made of raster data, this size is the
		/// tile width in pixels(the tile height is supposed equal to the tile
        /// width).
        /// </summary>
		[SerializeField]
		private int m_tileSize;

        private Slot[] m_allSlots;

        private LinkedList<Slot> m_freeSlots;

		protected virtual void Awake() 
		{
			Capacity = GetComponent<TileCache>().Capacity;

			m_allSlots = new Slot[Capacity];
			m_freeSlots = new LinkedList<Slot>();
		}

		public void OnDestroy()
		{
			for(int i = 0; i < Capacity; i++)
				m_allSlots[i].Release();
		}

		protected void AddSlot(int i, Slot slot)
		{
			m_allSlots[i] = slot;
			m_freeSlots.AddLast(slot);
        }

        /// <summary>
        /// Returns a free slot in the pool of slots managed by this TileStorage.
		/// return a free slot, or NULL if all tiles are currently allocated.The
        /// returned slot is then considered to be allocated, until it is
		/// released with deleteSlot.
        /// </summary>
        public Slot NewSlot()
		{
			if(m_freeSlots.Count != 0) 
			{
				Slot s = m_freeSlots.First.Value;
				m_freeSlots.RemoveFirst();
				return s;
			}
			else {
				return null;
			}
        }

        /// <summary>
        /// Notifies this storage that the given slot is free. The given slot can
		/// then be allocated to store a new tile, i.e., it can be returned by a
		//  subsequent call to newSlot.
        /// </summary>
        /// <param name="t">a slot that is no longer in use</param>
        public void DeleteSlot(Slot t)
        {
			m_freeSlots.AddLast(t);
		}

	}

}

































