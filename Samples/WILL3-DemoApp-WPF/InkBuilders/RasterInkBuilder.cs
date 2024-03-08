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
