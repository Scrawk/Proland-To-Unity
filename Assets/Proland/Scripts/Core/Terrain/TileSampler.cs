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
using System.Collections.Generic;

namespace Proland
{

	/// <summary>
    /// This class can set the uniforms necessary to access a given texture tile on GPU, stored
	/// in a GPUTileStorage.This class also manages the creation
    /// of new texture tiles when a terrain quadtree is updated, via a TileProducer.
    /// </summary>
	public class TileSampler : Node
	{

        public bool StoreLeaf { get { return m_storeLeaf; } }

        public int Priority { get { return m_priority; } }

        /// <summary>
        /// True to store texture tiles for leaf quads.
        /// </summary>
        [SerializeField]
        private bool m_storeLeaf = true;

        /// <summary>
        /// True to store texture tiles for non leaf quads.
        /// </summary>
        [SerializeField]
        private bool m_storeParent = true;

        /// <summary>
        /// The order in which to update samplers
        /// </summary>
        [SerializeField]
        private int m_priority = -1;

        /// <summary>
        /// An internal quadtree to store the texture tiles associated with each quad.
        /// </summary>
        private QuadTree m_root = null;

        private TileSamplerUniforms m_uniforms;

        /// <summary>
        /// The producer to be used to create texture tiles for newly created quads.
        /// </summary>
        public TileProducer Producer { get; private set; }

        /// <summary>
        /// The terrain node associated with this sampler
        /// </summary>
        public TerrainNode TerrainNode { get; private set; }

        protected override void Start() 
		{
			base.Start();

			Producer = GetComponent<TileProducer>();
			m_uniforms = new TileSamplerUniforms(Producer.Name);

           TerrainNode = FindTerrainNode(transform.parent);

            if (TerrainNode == null)
                throw new InvalidOperationException("TileSampler component must be the child of a terrain node.");
        }

        protected override void OnDestroy() 
		{
			base.OnDestroy();
			//Debug.Log("Max used tiles for producer " + Producer.Name + " = " + Producer.Cache.MaxUsedTiles);
		}

		public virtual void UpdateSampler() 
		{

			PutTiles(m_root, TerrainNode.Root);
			GetTiles(null, ref m_root, TerrainNode.Root);

			//Debug.Log("used = " + Producer.Cache.UsedTilesCount + " unused = " + Producer.Cache.UnusedTilesCount);
		}

        /// <summary>
        /// Returns true if a tile is needed for the given terrain quad.
        /// </summary>
        protected virtual bool NeedTile(TerrainQuad quad)
		{
			bool needTile = m_storeLeaf;

			//if the quad is not a leaf and producer has children 
			//and if have been asked not to store parent then dont need tile
			if (!m_storeParent && !quad.IsLeaf && Producer.HasChildren(quad.Level, quad.Tx, quad.Ty))
				needTile = false;
			
			//if this quad is not visilbe and have not been asked to store invisilbe quads dont need tile
			if (!quad.IsVisible)
				needTile = false;

			return needTile;
        }

		/// <summary>
        /// Updates the internal quadtree to make it identical to the given terrain
		/// quadtree.This method releases the texture tiles corresponding to
		/// deleted quads.
        /// </summary>
        protected virtual void PutTiles(QuadTree tree, TerrainQuad quad)
		{
			if (tree == null) return;

			//Check if this tile is needed, if not put tile.
			tree.NeedTile = NeedTile(quad);
			
			if (!tree.NeedTile && tree.Tile != null)
            {
				Producer.PutTile(tree.Tile);
				tree.Tile = null;
			}

			//If this qiad is a leaf then all children of the tree are not needed
			if (quad.IsLeaf)
            {
				if (!tree.IsLeaf) 
					tree.RecursiveDeleteChildren(this);
			}
			else if(Producer.HasChildren(quad.Level, quad.Tx, quad.Ty))
            {
				for (int i = 0; i < 4; ++i) 
					PutTiles(tree.Children[i], quad.GetChild(i));
			}

        }

		/// <summary>
        /// Updates the internal quadtree to make it identical to the given terrain
		/// quadtree.Collects the tasks necessary to create the missing texture
        /// tiles, corresponding to newly created quads.
        /// </summary>
        protected virtual void GetTiles(QuadTree parent, ref QuadTree tree, TerrainQuad quad)
		{
			//if tree not created, create a new tree and check if its tile is needed
			if (tree == null) 
			{
				tree = new QuadTree(parent);
				tree.NeedTile = NeedTile(quad);
			}

			//If this trees tile is needed get a tile and add its task to the schedular if the task is not already done
			if (tree.NeedTile && tree.Tile == null) 
			{
				tree.Tile = Producer.GetTile(quad.Level, quad.Tx, quad.Ty);

				if(!tree.Tile.IsDone) 
                    tree.Tile.CreateTile();
			}
			
			if (!quad.IsLeaf && Producer.HasChildren(quad.Level, quad.Tx, quad.Ty))
            {
				for (int i = 0; i < 4; ++i)
					GetTiles(tree, ref tree.Children[i], quad.GetChild(i));
			}
		}

		public void SetTile(MaterialPropertyBlock matPropertyBlock, int level, int tx, int ty)
		{
			if(!Producer.IsGPUProducer) return;

			RenderTexture tex = null;
			Vector3 coords = Vector3.zero, size = Vector3.zero;

			SetTile(ref tex, ref coords, ref size, level, tx, ty);

			matPropertyBlock.SetTexture(m_uniforms.tile, tex);
			matPropertyBlock.SetVector(m_uniforms.tileCoords, coords);
			matPropertyBlock.SetVector(m_uniforms.tileSize, size);
		}

		public void SetTile(Material mat, int level, int tx, int ty)
		{
			if(!Producer.IsGPUProducer) return;

			RenderTexture tex = null;
			Vector3 coords = Vector3.zero, size = Vector3.zero;
			
			SetTile(ref tex, ref coords, ref size, level, tx, ty);
			
			mat.SetTexture(m_uniforms.tile, tex);
			mat.SetVector(m_uniforms.tileCoords, coords);
			mat.SetVector(m_uniforms.tileSize, size);
		}

        /// <summary>
        /// Finds the Terrain node component on parent of game object.
        /// </summary>
        private TerrainNode FindTerrainNode(Transform transform)
        {
            if (transform == null) return null;

            TerrainNode node = transform.GetComponent<TerrainNode>();

            if (node == null)
                return FindTerrainNode(transform.parent);
            else
                return node;
        }

        /// <summary>
        /// Sets the uniforms necessary to access the texture tile for the given quad.
        /// The samplers producer must be using a GPUTileStorage at the first slot
        /// for this function to work
        /// </summary>
        private void SetTile(ref RenderTexture tex, ref Vector3 coord, ref Vector3 size, int level, int tx, int ty)
		{

			if(!Producer.IsGPUProducer) return;

			Tile t = null;
			int b = Producer.Border;
			int s = Producer.Cache.GetStorage(0).TileSize;

			float dx = 0;
			float dy = 0;
			float dd = 1;
			float ds0 = (s / 2) * 2.0f - 2.0f * b;
			float ds = ds0;

            if (!Producer.HasTile(level, tx, ty))
                throw new MissingTileException("Producer should have tile.");

			QuadTree tt = m_root;
			QuadTree tc;
			int tl = 0;

            while (tl != level && (tc = tt.Children[((tx >> (level - tl - 1)) & 1) | ((ty >> (level - tl - 1)) & 1) << 1]) != null)
            {
				tl += 1;
				tt = tc;
			}

			t = tt.Tile;

			dx = dx * ((s / 2) * 2 - 2 * b) / dd;
			dy = dy * ((s / 2) * 2 - 2 * b) / dd;

			if(t == null)
				throw new NullReferenceException("tile is null");

			GPUSlot gpuSlot = t.GetSlot(0) as GPUSlot;

			if(gpuSlot == null)
                throw new NullReferenceException("gpuSlot is null");

			float w = gpuSlot.Texture.width;
			float h = gpuSlot.Texture.height;
		
			Vector4 coords;
			if (s%2 == 0) 
				coords = new Vector4((dx + b) / w, (dy + b) / h, 0.0f, ds / w);
			 else 
				coords = new Vector4((dx + b + 0.5f) / w, (dy + b + 0.5f) / h, 0.0f, ds / w);

			tex = gpuSlot.Texture;
			coord = new Vector3(coords.x, coords.y, coords.z);
			size = new Vector3(coords.w, coords.w, (s / 2) * 2.0f - 2.0f * b);
		}

	}

    public class TileSamplerUniforms
    {
        public int tile, tileSize, tileCoords;

        public TileSamplerUniforms(string name)
        {
            tile = Shader.PropertyToID("_" + name + "_Tile");
            tileSize = Shader.PropertyToID("_" + name + "_TileSize");
            tileCoords = Shader.PropertyToID("_" + name + "_TileCoords");
        }
    }

    /// <summary>
    /// class used to sort a TileSampler based on its priority
    /// </summary>
    public class TileSamplerComparer : IComparer<TileSampler>
    {
        int IComparer<TileSampler>.Compare(TileSampler a, TileSampler b)
        {
            return a.Priority.CompareTo(b.Priority);
        }
    }
}























