using System;
using System.Windows.Media;

namespace Wacom
{
	public static class Utils
	{
		static DateTime s_epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		static Random s_random = new Random();

		public static void SafeDispose(object obj)
		{
			IDisposable disposable = obj as IDisposable;

			if (disposable != null)
			{
				disposable.Dispose();
			}
		}

		public static Color GetRandomColor(byte alpha = 127)
		{
			return Color.FromArgb(
				alpha,
				(byte)s_random.Next(0, 255),
				(byte)s_random.Next(0, 255),
				(byte)s_random.Next(0, 255));
		}

		public static long GetTimestampMicroseconds()
		{
			long usec = (long)(1000 * DateTime.Now.ToUniversalTime().Subtract(s_epoch).TotalMilliseconds);
			return usec;
		}

	}
}
