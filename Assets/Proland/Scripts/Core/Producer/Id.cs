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
    /// A tile identifier for a given producer. Contains the tile coordinates level, tx, ty.
    /// </summary>
    public struct Id
    {
        public int Level { get; private set; }

        public int Tx { get; private set; }

        public int Ty { get; private set; }

        public Id(int level, int tx, int ty)
        {
            Level = level;
            Tx = tx;
            Ty = ty;
        }

        public int Compare(Id id)
        {
            return Level.CompareTo(id.Level);
        }

        public bool Equals(Id id)
        {
            return (Level == id.Level && Tx == id.Tx && Ty == id.Ty);
        }

        public override int GetHashCode()
        {
            int hashcode = 23;
            hashcode = (hashcode * 37) + Level;
            hashcode = (hashcode * 37) + Tx;
            hashcode = (hashcode * 37) + Ty;

            return hashcode;
        }

        public override string ToString()
        {
            return string.Format("[Id: Level={0}, Tx={1}, Ty={2}]", Level, Tx, Ty);
        }
    }

    /// <summary>
    /// A Id is sorted based as its level. Sorts from lowest level to highest
    /// </summary>
    public class ComparerID : IComparer<Id>
    {
        public static ComparerID Instance { get { return m_instance; } }

        private static ComparerID m_instance = new ComparerID();

        public int Compare(Id a, Id b)
        {
            return a.Compare(b);
        }
    }

    /// <summary>
    /// A Id is compared based on its level, tx and ty
    /// </summary>
    public class EqualityComparerID : IEqualityComparer<Id>
    {
        public static EqualityComparerID Instance { get { return m_instance; } }

        private static EqualityComparerID m_instance = new EqualityComparerID();

        public bool Equals(Id t1, Id t2)
        {
            return t1.Equals(t2);
        }

        public int GetHashCode(Id t)
        {
            return t.GetHashCode();
        }
    }

}