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
    /// A slot managed by a CBTileStorage containing the buffer
    /// </summary>
    public class CBSlot : Slot
    {
        public ComputeBuffer Buffer { get; private set; }

        public CBSlot(TileStorage owner, ComputeBuffer buffer)
            : base(owner)
        {
            Buffer = buffer;
        }

        public override void Release()
        {
            if (Buffer != null)
                Buffer.Release();
        }

    };

	/// <summary>
    /// A tile storage that can contain compute buffers
	/// </summary>
	public class CBTileStorage : TileStorage 
	{
	
		public enum DATA_TYPE { FLOAT, INT, BYTE };

        public ComputeBufferType BufferType { get { return m_bufferType; } }

        /// <summary>
        /// what type of data is held in the buffer. ie float, int, etc
        /// </summary>
        [SerializeField]
		private DATA_TYPE m_dataType = DATA_TYPE.FLOAT;

        /// <summary>
        /// How many channels has the data, ie a float1, float2, etc
        /// </summary>
        [SerializeField]
        private int m_channels = 1;

		[SerializeField]
        private ComputeBufferType m_bufferType = ComputeBufferType.Default;

		protected override void Awake() 
		{
			base.Awake();
			
            for (int i = 0; i < Capacity; i++)
			{
				ComputeBuffer buffer;

				switch(m_dataType)
				{
					case DATA_TYPE.FLOAT:
                        buffer = new ComputeBuffer(TileSize, sizeof(float) * m_channels, m_bufferType);
						break;

					case DATA_TYPE.INT:
                        buffer = new ComputeBuffer(TileSize, sizeof(int) * m_channels, m_bufferType);
						break;

					case DATA_TYPE.BYTE:
                        buffer = new ComputeBuffer(TileSize, sizeof(byte) * m_channels, m_bufferType);
						break;

					default:
                        buffer = new ComputeBuffer(TileSize, sizeof(float) * m_channels, m_bufferType);
						break;
				};

				CBSlot slot = new CBSlot(this, buffer);

				AddSlot(i, slot);
			}
		}

	}
	
}


























