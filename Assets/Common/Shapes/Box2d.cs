using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Common.Mathematics.LinearAlgebra;

namespace Common.Geometry.Shapes
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Box2d
    {

        public Vector2d Center { get { return (Min + Max) / 2.0; } }

        public Vector2d Size { get { return new Vector2d(Width, Height); } }

        public double Width { get { return Max.x - Min.x; } }

        public double Height { get { return Max.y - Min.y; } }

        public double Area { get { return (Max.x - Min.x) * (Max.y - Min.y); } }

        public Vector2d Min { get; set; }

        public Vector2d Max { get; set; }

        public Box2d(double min, double max)
        {
            Min = new Vector2d(min);
            Max = new Vector2d(max);
        }

        public Box2d(double minX, double maxX, double minY, double maxY)
        {
            Min = new Vector2d(minX, minY);
            Max = new Vector2d(maxX, maxY);
        }

        public Box2d(Vector2d min, Vector2d max)
        {
            Min = min;
            Max = max;
        }

        public Box2d(Vector2i min, Vector2i max)
        {
            Min = new Vector2d(min.x, min.y);
            Max = new Vector2d(max.x, max.y); ;
        }

        public void GetCorners(IList<Vector2d> corners)
        {
            corners[0] = new Vector2d(Min.x, Min.y);
            corners[1] = new Vector2d(Min.x, Max.y);
            corners[2] = new Vector2d(Max.x, Max.y);
            corners[3] = new Vector2d(Max.x, Min.y);
        }

        /// <summary>
        /// Returns the bounding box containing this box and the given point.
        /// </summary>
        public Box2d Enlarge(Vector2d p)
        {
            return new Box2d(Math.Min(Min.x, p.x), Math.Max(Max.x, p.x), Math.Min(Min.y, p.y), Math.Max(Max.y, p.y));
        }

        /// <summary>
        /// Returns the bounding box containing this box and the given box.
        /// </summary>
        public Box2d Enlarge(Box2d r)
        {
            return new Box2d(Math.Min(Min.x, r.Min.x), Math.Max(Max.x, r.Max.x), Math.Min(Min.y, r.Min.y), Math.Max(Max.y, r.Max.y));
        }

        /// <summary>
        /// Returns true if this bounding box contains the given bounding box.
        /// </summary>
        public bool Intersects(Box2d a)
        {
            if (Max.x < a.Min.x || Min.x > a.Max.x) return false;
            if (Max.y < a.Min.y || Min.y > a.Max.y) return false;
            return true;
        }

        /// <summary>
        /// Does the shape contain the point.
        /// </summary>
        public bool Contains(Vector2d p)
        {
            if (p.x > Max.x || p.x < Min.x) return false;
            if (p.y > Max.y || p.y < Min.y) return false;
            return true;
        }

    }

}

















