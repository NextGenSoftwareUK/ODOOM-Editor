
#region ================== Copyright (c) 2007 Pascal vd Heiden

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 */

#endregion

#region ================== Namespaces

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Rendering;
using SlimDX.Direct3D9;
using System.Drawing;
using CodeImp.DoomBuilder.Map;

#endregion

namespace CodeImp.DoomBuilder.Geometry
{
	public class Polygon : List<Vector2D>
	{
		#region ================== Variables

		#endregion

		#region ================== Properties

		#endregion

		#region ================== Constructors

		// Constructor
		public Polygon()
		{
			// Initialize
		}

		#endregion

		#region ================== Methods

		// Point inside the polygon?
		// See: http://local.wasp.uwa.edu.au/~pbourke/geometry/insidepoly/
		public bool Intersect(Vector2D p)
		{
			float miny, maxy, maxx, xint;
			Vector2D v1 = base[base.Count - 1];
			Vector2D v2;
			int index = 0;
			uint c = 0;

			// Go for all vertices
			while(index < base.Count)
			{
				// Get next vertex
				v2 = base[index];

				// Determine min/max values
				miny = Math.Min(v1.y, v2.y);
				maxy = Math.Max(v1.y, v2.y);
				maxx = Math.Max(v1.x, v2.x);

				// Check for intersection
				if((p.y > miny) && (p.y <= maxy))
				{
					if(p.x <= maxx)
					{
						if(v1.y != v2.y)
						{
							xint = (p.y - v1.y) * (v2.x - v1.x) / (v2.y - v1.y) + v1.x;
							if((v1.x == v2.x) || (p.x <= xint)) c++;
						}
					}
				}

				// Move to next
				v1 = v2;
				index++;
			}
			
			// Inside this polygon?
			return (c & 0x00000001UL) != 0;
		}

		#endregion
	}
}
