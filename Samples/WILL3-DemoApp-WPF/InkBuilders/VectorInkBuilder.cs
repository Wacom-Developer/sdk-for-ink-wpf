using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Input;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;

namespace Wacom
{
    using PolygonVertices = List<Vector2>;

	/// <summary>
	/// Manages ink geometry pipeline for vector brushes.
	/// </summary>
	public class VectorInkBuilder : InkBuilder
	{
		#region Fields

		private StockVectorInkBuilder mStockVectorInkBuilder = new StockVectorInkBuilder();
        private VectorDrawingTool ActiveTool = null;
		private VectorBrushStyle mBrushStyle = VectorBrushStyle.Pen;

		#endregion

		#region Properties

		public VectorBrushStyle BrushStyle
        {
            get
            {
                return mBrushStyle;
            }
            set
            {
                mBrushStyle = value;

				switch (mBrushStyle)
                {
                    case VectorBrushStyle.Pen:
                        ActiveTool = new PenTool();
                        break;

                    case VectorBrushStyle.Felt:
                        ActiveTool = new FeltTool();
                        break;

                    case VectorBrushStyle.Brush:
                        ActiveTool = new BrushTool();
                        break;

                    default:
                        throw new Exception("Unknown brush type");
                }
            }
        }

        #endregion

        #region Constructors

        public VectorInkBuilder(VectorBrushStyle brushStyle)
        {
            BrushStyle = brushStyle;

			mPointerDataProvider = mStockVectorInkBuilder.PointerDataProvider;
		}

		#endregion

		#region Public Interface

		public bool HasNewPoints => mStockVectorInkBuilder.HasNewPoints;

		public VectorBrush VectorBrush
		{
			get => mStockVectorInkBuilder.BrushApplier.Prototype;
		}

		public ProcessorResult<List<PolygonVertices>> GetCurrentPolygons()
		{
			return mStockVectorInkBuilder.GetCurrentPolygons();
		}

		public List<PolygonVertices> CreateStrokePolygon()
		{
			return mStockVectorInkBuilder.CreateStrokePolygon();
		}

		public Spline GetAccumulatedSplineCopy()
		{
			return mStockVectorInkBuilder.SplineAccumulator.Accumulated.Clone();
		}

		public override void SetupForStylus(StylusPointDescription sd, Graphics graphics)
		{
			base.SetupForStylus(sd, graphics);

			UpdateVectorInkPipeline(ActiveTool.GetLayoutStylus(), ActiveTool.GetCalculatorStylus(), ActiveTool.Shape);
		}

		public override void SetupForMouse(Graphics graphics)
		{
			UpdateVectorInkPipeline(ActiveTool.GetLayoutMouse(), ActiveTool.GetCalculatorMouse(), ActiveTool.Shape);
		}

		private void UpdateVectorInkPipeline(
			LayoutMask layoutMask,
			Calculator calculator,
			VectorBrush brush,
			float constSize = 1.0f,
			float constRotation = 0.0f,
			float scaleX = 1.0f,
			float scaleY = 1.0f,
			float offsetX = 0.0f,
			float offsetY = 0.0f)
		{
			mStockVectorInkBuilder.PathProducer.LayoutMask = layoutMask;
			mStockVectorInkBuilder.PathProducer.PathPointCalculator = calculator;

			mStockVectorInkBuilder.BrushApplier.Prototype = brush;
			mStockVectorInkBuilder.BrushApplier.DefaultSize = constSize;
			mStockVectorInkBuilder.BrushApplier.DefaultRotation = constRotation;
			mStockVectorInkBuilder.BrushApplier.DefaultScale = new Vector3(scaleX, scaleY, 1.0f);
			mStockVectorInkBuilder.BrushApplier.DefaultOffset = new Vector3(offsetX, offsetY, 0.0f);
		}

		#endregion
	}
}
