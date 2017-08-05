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
    /// A tile identifier. Contains a producer id and tile coordinates level,tx,ty.
   /// </summary>
    public struct TId
    {
        public int ProducerId { get; private set; }

        public int Level { get; private set; }

        public int Tx { get; private set; }

        public int Ty { get; private set; }

        public TId(int producerId, int level, int tx, int ty)
        {
            ProducerId = producerId;
            Level = level;
            Tx = tx;
            Ty = ty;
        }

        public bool Equals(TId id)
        {
            return (ProducerId == id.ProducerId && Level == id.Level && Tx == id.Tx && Ty == id.Ty);
        }

        public override int GetHashCode()
        {
            int hashcode = 23;
            hashcode = (hashcode * 37) + ProducerId;
            hashcode = (hashcode * 37) + Level;
            hashcode = (hashcode * 37) + Tx;
            hashcode = (hashcode * 37) + Ty;

            return hashcode;
        }

        public override string ToString()
        {
            return string.Format("[TId: ProducerId={0}, Level={1}, Tx={2}, Ty={3}]", ProducerId, Level, Tx, Ty);
        }
    }

    /// <summary>
    /// A Tid is compared based on its producer, level, tx and ty
    /// </summary>
    public class EqualityComparerTID : IEqualityComparer<TId>
    {
        public static EqualityComparerTID Instance { get { return m_instance; } }

        private static EqualityComparerTID m_instance = new EqualityComparerTID();

        public bool Equals(TId t1, TId t2)
        {
            return t1.Equals(t2);
        }

        public int GetHashCode(TId t)
        {
            return t.GetHashCode();
        }
    }

}