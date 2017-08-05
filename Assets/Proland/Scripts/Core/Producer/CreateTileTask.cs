using UnityEngine;
using System.Collections.Generic;

namespace Proland
{

    /// <summary>
    /// The task that creates the tiles. The task calles the producers DoCreateTile function
    /// and the data created is stored in the slot.
    /// </summary>
	public class CreateTileTask
	{

        /// <summary>
        /// Has the task been run
        /// </summary>
        public bool IsDone { get; private set; }

        /// <summary>
        /// Where the created tile data must be stored
        /// </summary>
        public List<Slot> Slot { get; private set; }

        /// <summary>
        /// The TileProducer that created this task.
        /// </summary>
        private TileProducer m_owner;

        /// <summary>
        /// The level of the tile to create.
        /// </summary>
        private int m_level;

        /// <summary>
        /// The quadtree x coordinate of the tile to create.
        /// </summary>
        private int m_tx;

        /// <summary>
        /// The quadtree y coordinate of the tile to create
        /// </summary>
        private int m_ty;

		public CreateTileTask(TileProducer owner, int level, int tx, int ty, List<Slot> slot)
		{
			m_owner = owner;
			m_level = level;
			m_tx = tx;
			m_ty = ty;
			Slot = slot;
		}

		public void Run() 
		{
            if (IsDone) return;

			m_owner.DoCreateTile(m_level, m_tx, m_ty, Slot);

            IsDone = true;
		}

		public override string ToString () {
			return string.Format("[CreateTileTask: name={0}, Level={1}, Tx={2}, Ty={3}]", m_owner.Name, m_level, m_tx, m_ty);
		}

	}

}















