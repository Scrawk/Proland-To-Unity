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

using UnityEngine;
using System;
using System.Collections.Generic;

namespace Proland
{

    /// <summary>
    /// A tile described by its level,tx,ty coordinates. A Tile
	/// describes where the tile is stored in the TileStorage, how its data can
	/// be produced, and how many users currently use it.
	/// Contains the keys (Id, Tid) commonly used to store the tiles in data structures like dictionaries
    /// </summary>
	public class Tile
	{

        public bool IsDone {  get { return m_task.IsDone; } }

        /// <summary>
        /// The tile id. Consists of a producer id, a level and a x,y coordinate.
        /// </summary>
        public TId TId { get; private set; }

        /// <summary>
        /// Returns the identifier of this tile.
        /// </summary>
        public Id Id {  get {  return new Id(TId.Level, TId.Tx, TId.Ty); } }

        /// <summary>
        /// Number of users currently using this tile
        /// </summary>
        public int Users { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public List<Slot> Slot { get { return m_task.Slot; } }

        /// <summary>
        /// The task that produces or produced the actual tile data.
        /// </summary>
        private CreateTileTask m_task;

		/// <summary>
		/// Creates a new tile.
		/// </summary>
		/// <param name="producerId">the id of the producer of this tile.</param>
		/// <param name="level">the quadtree level of this tile.</param>
		/// <param name="tx">tx the quadtree x coordinate of this tile.</param>
		/// <param name="ty">ty the quadtree y coordinate of this tile.</param>
		/// <param name="task">task the task that will produce the tile data.</param>
		public Tile(int producerId, int level, int tx, int ty, CreateTileTask task) 
		{
            if (task == null)
                throw new ArgumentNullException("task can not be null");

            TId = new TId(producerId, level, tx, ty);
			Users = 0;

            m_task = task;
		}

		public Slot GetSlot(int i) 
		{
			if(i >= m_task.Slot.Count)
				throw new IndexOutOfRangeException("slot at location " + i + " does not exist");
			
			return m_task.Slot[i];
		}

		public void IncrementUsers() {
			Users++;
		}

		public void DecrementUsers() {
			Users--;
		}

        public void CreateTile()
        {
            m_task.Run();
        }
	
	};

}






































