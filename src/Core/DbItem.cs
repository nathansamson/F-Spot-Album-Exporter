/*
 * FSpot.DbItem.cs
 *
 * Author(s):
 *	Larry Ewing
 *
 * This is free software. See COPYING for details.
 */

namespace FSpot
{
	public class DbItem {
		uint id;
		public uint Id {
			get { return id; }
		}
	
		protected DbItem (uint id) {
			this.id = id;
		}
	}
}