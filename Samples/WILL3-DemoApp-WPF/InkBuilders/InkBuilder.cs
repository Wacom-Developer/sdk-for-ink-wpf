using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;


namespace Wacom
{
	public abstract class InkBuilder
	{
		protected PointerDataProvider mPointerDataProvider;
		private long mLastPointTimestamp = 0;
		private bool mCollectPointerData = true;
		private List<PointerData> mPointerDataList = new List<PointerData>();

		internal const float mTargetMinTiltX = -90.0f;
		internal const float mTargetMaxTiltX = 90.0f;
		internal const float mTargetMinTiltY = -90.0f;
		internal const float mTargetMaxTiltY = 90.0f;

		internal float mScaleTiltX;
		internal float mScaleTiltY;
		internal int mInputMinTiltX;
		internal int mInputMaxTiltX;
		internal int mInputMinTiltY;
		internal int mInputMaxTiltY;

		public InkBuilder()
		{
		}

		public bool UseIntermediatePoints { get; set; } = true;

		public void AddPointFromMouseEvent(Phase phase, long timestampMicroseconds, System.Windows.Point mp)
		{
			float x = (float)mp.X;
			float y = (float)mp.Y;

			var pointerData = new PointerData(x, y, phase, timestampMicroseconds);

			AddPoint(pointerData);
		}

		public void AddPointsFromStylusEvent(Phase phase, long timestampMicroseconds, StylusPointCollection stylusPoints)
		{
			int lastIndex = stylusPoints.Count - 1;

			if (UseIntermediatePoints)
			{
				double d = 0.0;
				int pointsCount = stylusPoints.Count;

				if (phase == Phase.Update || phase == Phase.End)
				{
					d = (double)(timestampMicroseconds - mLastPointTimestamp) / pointsCount;
				}

				mLastPointTimestamp = timestampMicroseconds;

				for (int i = 0; i < pointsCount; i++)
				{
					long pointTimestamp = (long)Math.Floor(timestampMicroseconds - d * (lastIndex - i));

					Phase pointPhase = ((phase == Phase.End) && (i < (pointsCount - 1))) ? Phase.Update : phase;

					AddPoint(ConvertStylusPoint(pointPhase, pointTimestamp, stylusPoints[i]));
				}
			}
			else
			{
				AddPoint(ConvertStylusPoint(phase, timestampMicroseconds, stylusPoints[lastIndex]));
			}
		}

		public PointerData ConvertStylusPoint(Phase phase, long timestamp, StylusPoint sp)
		{
			float x = (float)sp.X;
			float y = (float)sp.Y;

			PointerData pointerData = null;

			if (mScaleTiltX == 0)
			{
				pointerData = new PointerData(x, y, phase, timestamp)
				{
					Force = sp.PressureFactor
				};
			}
			else
			{
				int tiltX = sp.GetPropertyValue(StylusPointProperties.XTiltOrientation);
				int tiltY = sp.GetPropertyValue(StylusPointProperties.YTiltOrientation);

				float tx = mTargetMinTiltX + (tiltX - mInputMinTiltX) * mScaleTiltX;
				float ty = mTargetMinTiltY + (tiltY - mInputMinTiltY) * mScaleTiltY;

				PointerData.CalculateAltitudeAndAzimuth(tx, ty, out float altitude, out float azimuth);

				pointerData = new PointerData(x, y, phase, timestamp)
				{
					Force = sp.PressureFactor,
					AltitudeAngle = altitude,
					AzimuthAngle = azimuth
				};
			}

			return pointerData;
		}

		private void AddPoint(PointerData addition)
		{
			Phase phase = addition.Phase;

			if (mCollectPointerData)
			{
				if (phase == Phase.Begin)
				{
					// Clear the pointer data list
					mPointerDataList.Clear();
					mPointerDataProvider.Reset();
				}

				mPointerDataList.Add(addition);
			}

			mPointerDataProvider.Add(addition);
		}

		public List<PointerData> GetPointerDataList()
		{
			if (!mCollectPointerData)
				throw new Exception("InkBuilder is not constructed to collect pointer data.");

			return new List<PointerData>(mPointerDataList);
		}

		public abstract void SetupForMouse(Graphics graphics);

		public virtual void SetupForStylus(StylusPointDescription sd, Graphics graphics)
		{
			GetStylusParams(sd);
		}

		protected void GetStylusParams(StylusPointDescription sd)
		{
			if (sd.HasProperty(StylusPointProperties.XTiltOrientation) &&
				sd.HasProperty(StylusPointProperties.YTiltOrientation))
			{
				StylusPointPropertyInfo tiltXInfo = sd.GetPropertyInfo(StylusPointProperties.XTiltOrientation);
				StylusPointPropertyInfo tiltYInfo = sd.GetPropertyInfo(StylusPointProperties.YTiltOrientation);

				mInputMinTiltX = tiltXInfo.Minimum;
				mInputMaxTiltX = tiltXInfo.Maximum;

				mInputMinTiltY = tiltYInfo.Minimum;
				mInputMaxTiltY = tiltYInfo.Maximum;

				mScaleTiltX = (mTargetMaxTiltX - mTargetMinTiltX) / (mInputMaxTiltX - mInputMinTiltX);
				mScaleTiltY = (mTargetMaxTiltY - mTargetMinTiltY) / (mInputMaxTiltY - mInputMinTiltY);
			}
			else
			{
				mScaleTiltX = 0;
				mScaleTiltY = 0;
			}
		}
	}
}
