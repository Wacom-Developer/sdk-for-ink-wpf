using System;
using System.Collections.Generic;
using System.Windows.Input;

using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;
using Wacom.Ink.Serialization.Model;

namespace Wacom
{
	/// <summary>
	/// Manages ink geometry pipeline for raster (particle) brushes.
	/// </summary>
	public class RasterInkBuilder : InkBuilder
    {
        #region Fields

        private const float defaultSpacing = 0.15f;
        private const int splitCount = 1;

        private RasterDrawingTool ActiveTool = null;

		private StockRasterInkBuilder mStockRasterInkBuilder = new StockRasterInkBuilder();

		private RasterBrushStyle mBrushStyle = RasterBrushStyle.Pencil;

		#endregion

		#region Constructors

		public RasterInkBuilder()
        {
			mPointerDataProvider = mStockRasterInkBuilder.PointerDataProvider;
		}

        #endregion

        #region Properties

        public ParticleBrush Brush => ActiveTool.Brush;

        public void SetBrushStyle(RasterBrushStyle brushStyle, Graphics graphics)
        {
            if (mBrushStyle == brushStyle && ActiveTool != null)
                return;

            switch (mBrushStyle = brushStyle)
            {
                case RasterBrushStyle.Pencil:
                    ActiveTool = new PencilTool(graphics);
                    break;
                case RasterBrushStyle.WaterBrush:
                    ActiveTool = new WaterBrushTool(graphics);
                    break;
                case RasterBrushStyle.Crayon:
                    ActiveTool = new CrayonTool(graphics);
                    break;
                default:
                    throw new Exception("Unknown brush type");
            }
        }

        public bool HasNewPoints => mStockRasterInkBuilder.HasNewPoints;

        #endregion

        public ProcessorResult<Path> GetCurrentInterpolatedPaths()
        {
			return mStockRasterInkBuilder.GetCurrentInterpolatedPaths();
		}

		public Path GetFullInterpolatedPath()
		{
			return mStockRasterInkBuilder.GetFullInterpolatedPath();
		}

		public Spline GetAccumulatedSplineCopy()
		{
			return mStockRasterInkBuilder.SplineAccumulator.Accumulated.Clone();
		}

		/*        /// <summary>
				/// Transform accumulated pointer input to ink geometry
				/// </summary>
				/// <returns>Tuple containing added data (Item1) and predicted or preliminary data (Item2)</returns>
				/// <remarks>Passes accumulated path segment (from PathProducer) through remaining stages of 
				/// the raster ink pipeline - Smoother, SplineProducer & SplineInterpolator</remarks>
				public ProcessorResult<Path> GetPath()
				{
					var smoothPath = mSmoothingFilter.Add(mPathSegment.IsFirst, mPathSegment.IsLast, mPathSegment.AccumulatedAddition, mPathSegment.LastPrediction);

					var spline = SplineProducer.Add(mPathSegment.IsFirst, mPathSegment.IsLast, smoothPath.Addition, smoothPath.Prediction);

					var points = SplineInterpolator.Add(mPathSegment.IsFirst, mPathSegment.IsLast, spline.Addition, spline.Prediction);

					mPathSegment.Reset();
					mPointerDataUpdateCount = 0;

					return points;
				}*/

		#region Public Interface

		public override void SetupForStylus(StylusPointDescription sd, Graphics graphics)
		{
			base.SetupForStylus(sd, graphics);

			SetBrushStyle(mBrushStyle, graphics);

			UpdateParticleInkPipeline(ActiveTool.GetLayoutStylus(), ActiveTool.GetCalculatorStylus(), ActiveTool.ParticleSpacing);
		}

		public void SetupForTouch(Graphics graphics)
		{
			SetBrushStyle(mBrushStyle, graphics);

			UpdateParticleInkPipeline(ActiveTool.GetLayoutMouse(), ActiveTool.GetCalculatorMouse(), ActiveTool.ParticleSpacing);
		}

		public override void SetupForMouse(Graphics graphics)
		{
			SetBrushStyle(mBrushStyle, graphics);

			UpdateParticleInkPipeline(ActiveTool.GetLayoutMouse(), ActiveTool.GetCalculatorMouse(), ActiveTool.ParticleSpacing);
		}

		public void UpdateParticleInkPipeline(LayoutMask layoutMask, Calculator calculator, float spacing, float constSize = 1.0f)
		{
			mStockRasterInkBuilder.PathProducer.LayoutMask = layoutMask;
			mStockRasterInkBuilder.PathProducer.PathPointCalculator = calculator;

			mStockRasterInkBuilder.SplineInterpolator.Spacing = spacing;
			mStockRasterInkBuilder.SplineInterpolator.DefaultSize = constSize;

			mStockRasterInkBuilder.SplineInterpolator.InterpolateByLength = true;
			mStockRasterInkBuilder.SplineInterpolator.SplitCount = 6;
		}


		/*        public void UpdatePipeline(LayoutMask layout, Calculator calculator, float spacing)
				{
					bool layoutChanged = false;
					bool otherChange = false;

					if (layout != Layout)
					{
						Layout = layout;
						layoutChanged = true;
					}

					if (mPathProducer == null || calculator != mPathProducer.PathPointCalculator || layoutChanged)
					{
						mPathProducer = new PathProducer(Layout, calculator, true);
						otherChange = true;
					}

					if (mSmoothingFilter == null || layoutChanged)
					{
						mSmoothingFilter = new SmoothingFilter()
						{
							KeepAllData = true
						};
						otherChange = true;
					}

					if (SplineProducer == null || layoutChanged)
					{
						SplineProducer = new SplineProducer(true);
						otherChange = true;
					}

					if (SplineInterpolator == null || layoutChanged)
					{
						SplineInterpolator = new DistanceBasedInterpolator(spacing, splitCount, true, true, true);
						otherChange = true;
					}
					((DistanceBasedInterpolator)SplineInterpolator).Spacing = spacing;

					if (layoutChanged || otherChange)
					{
						LayoutUpdated?.Invoke(this, EventArgs.Empty);
					}
				}*/

		public Wacom.Ink.Serialization.Model.RasterBrush CreateSerializationBrush(string name)
        {
            return new Wacom.Ink.Serialization.Model.RasterBrush(
				name,
				(float)Brush.FillTileSize.Width,
				(float)Brush.FillTileSize.Height,
				true,
				(RotationMode)Brush.RotationMode,
				Brush.Scattering,
				mStockRasterInkBuilder.SplineInterpolator.Spacing,
				ActiveTool.Fill.ImageFileData,
				new List<byte[]>() { ActiveTool.Shape.ImageFileData },
				Wacom.Ink.Serialization.Model.BlendMode.SourceOver);
        }

        #endregion
    }
}
