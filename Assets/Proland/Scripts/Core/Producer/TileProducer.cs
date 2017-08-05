
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
 * */

using UnityEngine;
using System.Collections.Generic;

namespace Proland
{

    /// <summary>
    /// An abstract producer of tiles. A TileProducer must be inherited from and overide the DoCreateTile
    /// function to create the tiles data.
    /// Note that several TileProducer can share the same TileCache, and hence the
    /// same TileStorage.
    /// </summary>
	[RequireComponent(typeof(TileSampler))]
	public abstract class TileProducer : Node 
	{

        /// <summary>
        /// Returns the TileCache that stores the tiles produced by this producer.
        /// </summary>
        public TileCache Cache { get { InitCache(); return m_cache; } }

        public bool IsGPUProducer { get { return m_isGPUProducer; } }

        public int Id { get { return m_id; } }

        public string Name {  get { return m_name; } }

        /// <summary>
        /// The tile sampler associated with this producer
        /// </summary>
        public TileSampler Sampler { get; private set; }

        public TerrainNode TerrainNode {  get { return Sampler.TerrainNode; } }

        /// <summary>
        /// The tile cache game object that stores the tiles produced by this producer.
        /// </summary>
        [SerializeField]
        private TileCache m_cache;

        /// <summary>
        /// The name of the uniforms this producers data will be bound if used in a shader
        /// </summary>
        [SerializeField]
        private string m_name;

        /// <summary>
        /// Does this producer use the gpu 
        /// </summary>
        [SerializeField]
        private bool m_isGPUProducer = true;

        /// <summary>
        /// The id of this producer. This id is local to the TileCache used by this
        /// producer, and is used to distinguish all the producers that use this cache.
        /// </summary>
        private int m_id;

        private bool m_isInit;

		protected override void Start() 
		{
			base.Start();
			InitCache();

			//Get the samplers attached to game object. Must have one sampler attahed.
			Sampler = GetComponent<TileSampler>();
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
		}

        /// <summary>
        /// It is posible that a producer will have a call to get its cache before
        /// its start fuction has been called. Call InitCache in the start and get functions
        /// to ensure that the cache is always init before being returned.
        /// </summary>
        void InitCache() 
		{
			if(!m_isInit) 
			{
                m_isInit = true;
                m_id = m_cache.NextProducerId;
				m_cache.InsertProducer(m_id, this);
			}
		}

		public int GetTileSize(int i)
        {
			return Cache.GetStorage(i).TileSize;
		}

		public int GetTileSizeMinBorder(int i)
        {
			int s = Cache.GetStorage(i).TileSize;
			return s - Border*2;
        }

        /// <summary>
        /// Returns the size in pixels of the border of each tile. Tiles made of
		/// raster data may have a border that contains the value of the neighboring
        /// pixels of the tile.For instance if the tile size(returned by
		/// TileStorage.GetTileSize) is 196, and if the tile border is 2, this means
        /// that the actual tile data is 192x192 pixels, with a 2 pixel border that
        /// contains the value of the neighboring pixels.Using a border introduces
        /// data redundancy but is usefull to get the value of the neighboring pixels
        /// of a tile without needing to load the neighboring tiles.
        /// </summary>
        public virtual int Border {  get { return 0; } }

		/// <summary>
        ///  Returns true if this producer can produce the given tile.
        /// </summary>
        /// <param name="level">the tile's quadtree level</param>
        /// <param name="tx">the tile's quadtree x coordinate</param>
        /// <param name="ty">the tile's quadtree y coordinate</param>
        public virtual bool HasTile(int level, int tx, int ty) {
			return true;
		}

        /// <summary>
        /// Returns true if this producer can produce the children of the given tile.
        /// </summary>
        /// <param name="level">the tile's quadtree level</param>
        /// <param name="tx">the tile's quadtree x coordinate</param>
        /// <param name="ty">the tile's quadtree y coordinate</param>
		public virtual bool HasChildren(int level, int tx, int ty){
			return HasTile(level + 1, 2 * tx, 2 * ty);
        }

        /// <summary>
        /// Decrements the number of users of this tile by one. If this number
        /// becomes 0 the tile is marked as unused, and so can be evicted from the
        /// cache at any moment.
        /// </summary>
        /// <param name="tile">a tile currently in use</param>
        public virtual void PutTile(Tile tile) {
			m_cache.PutTile(tile);
        }

        /// <summary>
        /// Returns the requested tile, creating it if necessary. If the tile is
		/// currently in use it is returned directly.If it is in cache but unused,
        /// it marked as used and returned.Otherwise a new tile is created, marked
        /// as used and returned.In all cases the number of users of this tile is
		/// incremented by one.
        /// </summary>
        /// <param name="level">the tile's quadtree level</param>
        /// <param name="tx">the tile's quadtree x coordinate</param>
        /// <param name="ty">the tile's quadtree y coordinate</param>
        /// <returns></returns>
        public virtual Tile GetTile(int level, int tx, int ty) 
		{
			return m_cache.GetTile(m_id, level, tx, ty);
        }

        /// <summary>
        /// Looks for a tile in the TileCache of this TileProducer.
        /// </summary>
        /// <param name="level">the tile's quadtree level</param>
        /// <param name="tx">the tile's quadtree x coordinate</param>
        /// <param name="ty">the tile's quadtree y coordinate</param>
        /// <param name="includeUnusedCache">true to include both used and unused tiles in the search, false to include only the used tiles.
        /// <param name="done">true to check that the tile's creation task is done</param>
        /// <returns>return the requested tile, or NULL if it is not in the TileCache or
        /// if 'done' is true, if it is not ready.This method does not change the
        /// number of users of the returned tile.</returns>
        public virtual Tile FindTile(int level, int tx, int ty, bool includeUnusedCache, bool done) 
		{
			Tile tile = m_cache.FindTile(m_id, level, tx, ty, includeUnusedCache);

			if (done && tile != null && !tile.IsDone) {
				tile = null;
			}

			return tile;
		}

        /// <summary>
        /// Creates a Task to produce the data of the given tile.
        /// </summary>
        /// <param name="level">the tile's quadtree level</param>
        /// <param name="tx">the tile's quadtree x coordinate</param>
        /// <param name="ty">the tile's quadtree y coordinate</param>
        /// <returns></returns>
		public virtual CreateTileTask CreateTask(int level, int tx, int ty, List<Slot> slot)
        {
			return new CreateTileTask(this, level, tx, ty, slot);
        }

        /// <summary>
        /// Creates the given tile. If this task requires tiles produced by other
        /// The default implementation of this method calls DoCreateTile on
        /// each Layer of this producer.
        /// </summary>
        /// <param name="level">the tile's quadtree level</param>
        /// <param name="tx">the tile's quadtree x coordinate</param>
        /// <param name="ty">the tile's quadtree y coordinate</param>
        /// <param name="slot">where the created tile data must be stored</param>
        public virtual void DoCreateTile(int level, int tx, int ty, List<Slot> slot)
        {

		}

	}
}
























