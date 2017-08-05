
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
using System.Collections;
using System;

using Common.Mathematics.LinearAlgebra;
using Common.Geometry.Shapes;
using Common.Unity.Mathematics;

namespace Proland
{
	/// <summary>
	/// A quad in a terrain quadtree. The quadtree is subdivided based only
	/// on the current viewer position. All quads are subdivided if they
	/// meet the subdivision criterion, even if they are outside the view
	/// frustum. The quad visibility is stored in #visible. It can be used
	/// in TileSampler to decide whether or not data must be produced
	/// for invisible tiles (we recall that the terrain quadtree itself
	/// does not store any terrain data).
	/// </summary>
	public class TerrainQuad 
	{

        public float ZMax { get; private set; }

        public float ZMin { get; private set; }

        public bool IsVisible { get { return (Visible != FRUSTUM_VISIBILTY.INVISIBLE); } }

        /// <summary>
        /// Returns true if this quad is not subdivided.
        /// </summary>
        public bool IsLeaf { get {  return (m_children[0] == null); } }
		
	    /// <summary>
        /// The TerrainNode to which this terrain quadtree belongs.
	    /// </summary>
        public TerrainNode Owner { get; private set; }

	    /// <summary>
        /// The level of this quad in the quadtree (0 for the root).
	    /// </summary>
        public int Level { get; private set; }

	    /// <summary>
        /// The logical x,y coordinate of this quad (between 0 and 2^level).
	    /// </summary>
        public int Tx { get; private set; }
        public int Ty { get; private set; }

	    /// <summary>
        /// The physical x,y coordinate of the lower left corner of this quad (in local space).
	    /// </summary>
        public double Ox { get; private set; }
        public double Oy { get; private set; }

	    /// <summary>
        /// The physical size of this quad (in local space).
	    /// </summary>
        public double Length { get; private set; }

		/// <summary>
        /// local bounding box
		/// </summary>
        public Box3d LocalBox { get; private set; }

        /// <summary>
        /// Should the quad be drawn.
        /// </summary>
        public bool Drawable { get; set; }
		
        /// <summary>
        /// The visibility of the bounding box of this quad from the current
		/// viewer position. The bounding box is computed using zmin and
		/// zmax, which must therefore be up to date to get a correct culling
		/// of quads out of the view frustum. This visibility only takes frustum
		/// culling into account.
        /// </summary>
        public FRUSTUM_VISIBILTY Visible { get; private set; }
		
		/// <summary>
		/// True if the bounding box of this quad is occluded by the bounding
		/// boxes of the quads in front of it.
		/// </summary>
		public bool Occluded { get; private set; }

        /// <summary>
        /// The four subquads of this quad. If this quad is not subdivided,
        /// the four values are NULL. The subquads are stored in the
        /// following order: bottomleft, bottomright, topleft, topright.
        /// </summary>
        private TerrainQuad[] m_children = new TerrainQuad[4];

        /// <summary>
        /// The parent quad of this quad.
        /// </summary>
        private TerrainQuad m_parent;
		
		/// <summary>
		/// Creates a new TerrainQuad.
		/// </summary>
		/// <param name="owner">the TerrainNode to which the terrain quadtree belongs</param>
		/// <param name="parent">the parent quad of this quad</param>
		/// <param name="tx">the logical x coordinate of this quad</param>
		/// <param name="ty">the logical y coordinate of this quad</param>
		/// <param name="ox">the physical x coordinate of the lower left corner of this quad</param>
		/// <param name="oy">the physical y coordinate of the lower left corner of this quad</param>
		/// <param name="length">the physical size of this quad</param>
		/// <param name="zmin">the minimum terrain elevation inside this quad</param>
		/// <param name="zmax">the maximum terrain elevation inside this quad</param>
		public TerrainQuad(TerrainNode owner, TerrainQuad parent, int tx, int ty, double ox, double oy, double length, float zmin, float zmax)
		{

			Owner = owner;
			m_parent = parent;
			Level = (m_parent == null) ? 0 : m_parent.Level + 1;
			Tx = tx;
			Ty = ty;
			Ox = ox;
			Oy = oy;
			ZMax = zmax;
			ZMin = zmin;
			Length = length;
			LocalBox = new Box3d(Ox, Ox + Length, Oy, Oy + Length, ZMin, ZMax);

		}

	    /// <summary>
        /// Returns the number of quads in the tree below this quad.
	    /// </summary>
	    public int Size
		{
            get
            {
                int s = 1;
                if (IsLeaf)
                    return s;
                else
                {
                    return s + m_children[0].Size + m_children[1].Size +
                               m_children[2].Size + m_children[3].Size;
                }
            }
		}

	    /// <summary>
        /// Returns the depth of the tree below this quad.
	    /// </summary>
	    public int Depth
		{
            get
            {
                if (IsLeaf)
                    return Level;
                else
                {
                    return Mathf.Max(Mathf.Max(m_children[0].Depth, m_children[1].Depth),
                           Mathf.Max(m_children[2].Depth, m_children[3].Depth));
                }
            }
		}

        public TerrainQuad GetChild(int i)
        {
            return m_children[i];
        }

		void Release()
		{

			for(int i = 0; i < 4; i++)
			{
				if(m_children[i] != null)
				{
					m_children[i].Release();
					m_children[i] = null;
				}
			}

		}

	    /// <summary>
	    /// Subdivides or unsubdivides this quad based on the current
	    /// viewer distance to this quad, relatively to its size. This
	    /// method uses the current viewer position provided by the
	    /// TerrainNode to which this quadtree belongs.
	    /// </summary>
	    public void Update()
		{
				
			FRUSTUM_VISIBILTY v = (m_parent == null) ? FRUSTUM_VISIBILTY.PARTIALLY : m_parent.Visible;
			
			if (v == FRUSTUM_VISIBILTY.PARTIALLY)
		        Visible = Owner.GetVisibility(LocalBox);
			else
		        Visible = v;
		
		    // here we reuse the occlusion test from the previous frame:
		    // if the quad was found unoccluded in the previous frame, we suppose it is
		    // still unoccluded at this frame. If it was found occluded, we perform
		    // an occlusion test to check if it is still occluded.
		    if (Visible != FRUSTUM_VISIBILTY.INVISIBLE && Occluded) 
			{
		        Occluded = Owner.IsOccluded(LocalBox);
				
		        if(Occluded)
		            Visible = FRUSTUM_VISIBILTY.INVISIBLE;
		    }

		    double ground = Owner.View.GroundHeight;
		    double dist = Owner.GetCameraDist(new Box3d(Ox, Ox + Length, Oy, Oy + Length, Math.Min(0.0, ground), Math.Max(0.0, ground)));
			
		    if ((Owner.SplitInvisibleQuads || Visible != FRUSTUM_VISIBILTY.INVISIBLE) && dist < Length * Owner.SplitDist && Level < Owner.MaxLevel) 
			{
		        if (IsLeaf) Subdivide();
		
		        int[] order = new int[4];
		        double ox = Owner.LocalCameraPos.x;
		        double oy = Owner.LocalCameraPos.y;
		        double cx = Ox + Length / 2.0;
		        double cy = Oy + Length / 2.0;
				
		        if (oy < cy) {
		            if (ox < cx) {
		                order[0] = 0;
		                order[1] = 1;
		                order[2] = 2;
		                order[3] = 3;
		            } else {
		                order[0] = 1;
		                order[1] = 0;
		                order[2] = 3;
		                order[3] = 2;
		            }
		        } else {
		            if (ox < cx) {
		                order[0] = 2;
		                order[1] = 0;
		                order[2] = 3;
		                order[3] = 1;
		            } else {
		                order[0] = 3;
		                order[1] = 1;
		                order[2] = 2;
		                order[3] = 0;
		            }
		        }
		
		        m_children[order[0]].Update();
		        m_children[order[1]].Update();
		        m_children[order[2]].Update();
		        m_children[order[3]].Update();
		
		        // we compute a more precise occlusion for the next frame (see above),
		        // by combining the occlusion status of the child nodes
		        Occluded = (m_children[0].Occluded && m_children[1].Occluded && m_children[2].Occluded && m_children[3].Occluded);
		    } 
			else 
			{
		        if (Visible != FRUSTUM_VISIBILTY.INVISIBLE)
                {
		            // we add the bounding box of this quad to the occluders list
		            Occluded = Owner.AddOccluder(LocalBox);
		            if (Occluded)
		                Visible = FRUSTUM_VISIBILTY.INVISIBLE;
		        }

                if (!IsLeaf) Release();
		    }
			
		}
		
		/// <summary>
        /// Creates the four subquads of this quad.
		/// </summary>
		private void Subdivide()
		{
		    float hl = (float) Length / 2.0f;
		    m_children[0] = new TerrainQuad(Owner, this, 2 * Tx, 2 * Ty, Ox, Oy, hl, ZMin, ZMax);
            m_children[1] = new TerrainQuad(Owner, this, 2 * Tx + 1, 2 * Ty, Ox + hl, Oy, hl, ZMin, ZMax);
            m_children[2] = new TerrainQuad(Owner, this, 2 * Tx, 2 * Ty + 1, Ox, Oy + hl, hl, ZMin, ZMax);
            m_children[3] = new TerrainQuad(Owner, this, 2 * Tx + 1, 2 * Ty + 1, Ox + hl, Oy + hl, hl, ZMin, ZMax);
		}

        private static int[,] ORDER = new int[,] { { 1, 0 }, { 2, 3 }, { 0, 2 }, { 3, 1 } };

		/// <summary>
		/// Used to draw the outline of the terrain quads bounding box.
		/// See the DrawQuadTree.cs script for more info.
		/// </summary>
		public void DrawQuadOutline(Camera camera, Material lineMaterial, Color lineColor)
		{
			if(IsLeaf)
			{
				if(Visible == FRUSTUM_VISIBILTY.INVISIBLE) return;

				Vector3[] verts = new Vector3[8];

                verts[0] = MathConverter.ToVector3(Owner.Deform.LocalToDeformed(new Vector3d(Ox, Oy, ZMin)));
                verts[1] = MathConverter.ToVector3(Owner.Deform.LocalToDeformed(new Vector3d(Ox + Length, Oy, ZMin)));
                verts[2] = MathConverter.ToVector3(Owner.Deform.LocalToDeformed(new Vector3d(Ox, Oy + Length, ZMin)));
                verts[3] = MathConverter.ToVector3(Owner.Deform.LocalToDeformed(new Vector3d(Ox + Length, Oy + Length, ZMin)));

                verts[4] = MathConverter.ToVector3(Owner.Deform.LocalToDeformed(new Vector3d(Ox, Oy, ZMax)));
                verts[5] = MathConverter.ToVector3(Owner.Deform.LocalToDeformed(new Vector3d(Ox + Length, Oy, ZMax)));
                verts[6] = MathConverter.ToVector3(Owner.Deform.LocalToDeformed(new Vector3d(Ox, Oy + Length, ZMax)));
                verts[7] = MathConverter.ToVector3(Owner.Deform.LocalToDeformed(new Vector3d(Ox + Length, Oy + Length, ZMax)));

				GL.PushMatrix();
				
				GL.LoadIdentity();
				GL.MultMatrix(camera.worldToCameraMatrix * MathConverter.ToMatrix4x4(Owner.LocalToWorld));
				GL.LoadProjectionMatrix(camera.projectionMatrix);
				
				lineMaterial.SetPass( 0 );
				GL.Begin( GL.LINES );
				GL.Color( lineColor );

				for(int i = 0; i < 4; i++) 
				{
                    //Draw bottom quad
                    GL.Vertex3(verts[ORDER[i, 0]].x, verts[ORDER[i, 0]].y, verts[ORDER[i, 0]].z);
                    GL.Vertex3(verts[ORDER[i, 1]].x, verts[ORDER[i, 1]].y, verts[ORDER[i, 1]].z);
                    //Draw top quad
                    GL.Vertex3(verts[ORDER[i, 0] + 4].x, verts[ORDER[i, 0] + 4].y, verts[ORDER[i, 0] + 4].z);
                    GL.Vertex3(verts[ORDER[i, 1] + 4].x, verts[ORDER[i, 1] + 4].y, verts[ORDER[i, 1] + 4].z);
                    //Draw verticals
                    GL.Vertex3(verts[ORDER[i, 0]].x, verts[ORDER[i, 0]].y, verts[ORDER[i, 0]].z);
                    GL.Vertex3(verts[ORDER[i, 0] + 4].x, verts[ORDER[i, 0] + 4].y, verts[ORDER[i, 0] + 4].z);
				}
				
				GL.End();
				
				GL.PopMatrix();
			}

			if(!IsLeaf)
			{
				m_children[0].DrawQuadOutline(camera, lineMaterial, lineColor);
				m_children[1].DrawQuadOutline(camera, lineMaterial, lineColor);
				m_children[2].DrawQuadOutline(camera, lineMaterial, lineColor);
				m_children[3].DrawQuadOutline(camera, lineMaterial, lineColor);
			}
		}

	}
}

























