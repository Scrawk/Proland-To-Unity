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
using System.Collections.Generic;

namespace Proland
{

    /// <summary>
    /// A slot managed by a GPUTileStorage containing the texture
    /// </summary>
    public class GPUSlot : Slot
    {
        public RenderTexture Texture { get; private set; }

        public GPUSlot(TileStorage owner, RenderTexture texture)
            : base(owner)
        {
            Texture = texture;
        }

        public override void Release()
        {
            if (Texture != null)
                Texture.Release();
        }
    };

	/// <summary>
    /// A TileStorage that stores tiles in 2D textures
	/// </summary>
	public class GPUTileStorage : TileStorage 
	{

        public RenderTextureFormat InternalFormat { get { return m_internalFormat; } }

        public TextureWrapMode WrapMode { get { return m_wrapMode; } }

        public FilterMode FilterMode { get { return m_filterMode; } }

        public RenderTextureReadWrite ReadWrite { get { return m_readWrite; } }

        public bool HasMipMaps { get { return m_mipmaps; } }

        public bool RandomWriteEnabled { get { return m_enableRandomWrite; } }

        [SerializeField]
		private RenderTextureFormat m_internalFormat = RenderTextureFormat.ARGB32;

		[SerializeField]
        private TextureWrapMode m_wrapMode = TextureWrapMode.Clamp;

		[SerializeField]
        private FilterMode m_filterMode = FilterMode.Point;

		[SerializeField]
        private RenderTextureReadWrite m_readWrite;

		[SerializeField]
        private bool m_mipmaps;

		[SerializeField]
        private bool m_enableRandomWrite;

		protected override void Awake() 
		{
			base.Awake();

			int tileSize = TileSize;
			int capacity = Capacity;

			for(int i = 0; i < capacity; i++)
			{
				RenderTexture texture = new RenderTexture(tileSize, tileSize, 0, m_internalFormat, m_readWrite);
				texture.filterMode = m_filterMode;
				texture.wrapMode = m_wrapMode;
				texture.useMipMap = m_mipmaps;
				texture.enableRandomWrite = m_enableRandomWrite;

				GPUSlot slot = new GPUSlot(this, texture);

				AddSlot(i, slot);
			}
		}

	}

}


























