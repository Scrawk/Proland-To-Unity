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

namespace Proland
{

	/// <summary>
    /// An internal quadtree to store the texture tile associated with each terrain quad.
	/// </summary>
	public class QuadTree 
	{
		/// <summary>
        /// if a tile is needed for this quad
		/// </summary>
        public bool NeedTile { get; set; }

		/// <summary>
        /// The parent quad of this quad.
		/// </summary>
        public QuadTree Parent { get; set; }

		/// <summary>
        /// The texture tile associated with this quad.
		/// </summary>
        public Tile Tile { get; set; }

        /// <summary>
        /// Does this quad have no children.
        /// </summary>
        public bool IsLeaf { get { return (Children[0] == null); } }

		/// <summary>
        /// The subquads of this quad.
		/// </summary>
        public QuadTree[] Children { get; private set; }
		
		public QuadTree(QuadTree parent) 
        {
            Children = new QuadTree[4];
			Parent = parent;
		}
		
        /// <summary>
        /// Deletes All trees subelements. Releases
        /// all the corresponding texture tiles.
        /// </summary>
		public void RecursiveDeleteChildren(TileSampler owner)
		{
			if (Children[0] != null) {
				for(int i = 0; i < 4; i++) {
					Children[i].RecursiveDelete(owner);
					Children[i] = null;
				}
			}
		}
		
        /// <summary>
        /// Deletes this Tree and all its subelements. Releases
        /// all the corresponding texture tiles.
        /// </summary>
		public void RecursiveDelete(TileSampler owner)
		{
			if (Tile != null && owner != null) {
				owner.Producer.PutTile(Tile);
				Tile = null;
			}
			if (Children[0] != null) {
				for(int i = 0; i < 4; i++) {
					Children[i].RecursiveDelete(owner);
					Children[i] = null;
				}
			}
		}
	}

}












