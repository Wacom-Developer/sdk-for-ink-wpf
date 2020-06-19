using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Wacom
{
	public class PointerManager
	{
		#region Fields

		private InputDevice mDevice;

		#endregion

		#region Stulys Events

		public bool OnPressed(StylusEventArgs e)
		{
			// If currently there is an unfinished stroke - do not interrupt it
			if (mDevice != null)
			{
				return false;
			}

			mDevice = e.StylusDevice;

			return true;
		}

		public bool OnMoved(StylusEventArgs e)
		{
			// Accept only the saved pointer, reject others
			return (mDevice != null) && (mDevice == e.StylusDevice);
		}

		public bool OnReleased(StylusEventArgs e)
		{
			// Reject events from other pointers
			if (mDevice == null || mDevice != e.StylusDevice)
			{
				return false;
			}

			mDevice = null;

			return true;
		}

		#endregion

		#region Mouse Events

		public bool OnPressed(MouseEventArgs e)
		{
			if (e.StylusDevice != null)
				return false;

			// If currently there is an unfinished stroke - do not interrupt it
			if (mDevice != null)
			{
				return false;
			}

			mDevice = e.MouseDevice;

			return true;
		}

		public bool OnMoved(MouseEventArgs e)
		{
			if (e.StylusDevice != null)
				return false;

			// Accept only the saved pointer, reject others
			return (mDevice != null) && (mDevice == e.MouseDevice);
		}

		public bool OnReleased(MouseEventArgs e)
		{
			if (e.StylusDevice != null)
				return false;

			// Reject events from other pointers
			if (mDevice == null || mDevice != e.MouseDevice)
			{
				return false;
			}

			mDevice = null;

			return true;
		}

		#endregion
	}
}
