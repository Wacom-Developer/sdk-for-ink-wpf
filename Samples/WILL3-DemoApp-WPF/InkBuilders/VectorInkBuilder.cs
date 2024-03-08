using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Input;
using System.Windows.Media;
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

		//private readonly VectorBrush mBrush;
        private VectorDrawingTool ActiveTool = null;

		VectorBrushStyle mBrushStyle = VectorBrushStyle.Pen;

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
            //mBrush = VectorBrushGeometries.Circle;

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


		/*      /// <summary>
				/// Transform accumulated pointer input to ink geometry
				/// </summary>
				/// <param name="defaultSize">Default size for SplineInterpolator and BrushApplier</param>
				/// <param name="defaultScale">Default Scale for BrushApplier</param>
				/// <param name="defaultOffset">Default Offset for BrushApplier</param>
				/// <param name="defaultRotation">Default Rotation for BrushApplier</param>
				/// <returns>Tuple containing added data (Item1) and predicted or preliminary data (Item2)</returns>
				/// <remarks>Passes accumulated path segment (from PathProducer) through remaining stages of 
				/// the vector ink pipeline - Smoother, SplineProducer, SplineInterpolator, BrushApplier, ConvexHullChainProducer,
				/// PolygonMerger and PolygonSimplifier</remarks>
				public ProcessorResult<List<PolygonVertices>> GetPolygons()
				{
					var smoothPath = mSmoothingFilter.Add(mPathSegment.IsFirst, mPathSegment.IsLast, mPathSegment.AccumulatedAddition, mPathSegment.LastPrediction);

					var spline = SplineProducer.Add(mPathSegment.IsFirst, mPathSegment.IsLast, smoothPath.Addition, smoothPath.Prediction);

					var points = SplineInterpolator.Add(mPathSegment.IsFirst, mPathSegment.IsLast, spline.Addition, spline.Prediction);

					var polys = BrushApplier.Add(mPathSegment.IsFirst, mPathSegment.IsLast, points.Addition, points.Prediction);

					var hulls = mConvexHullChainProducer.Add(mPathSegment.IsFirst, mPathSegment.IsLast, polys.Addition, polys.Prediction);

					var merged = mPolygonMerger.Add(mPathSegment.IsFirst, mPathSegment.IsLast, hulls.Addition, hulls.Prediction);

					var simplified = PolygonSimplifier.Add(mPathSegment.IsFirst, mPathSegment.IsLast, merged.Addition, merged.Prediction);

					mPathSegment.Reset();
					mPointerDataUpdateCount = 0;

					return simplified;
				}*/

		#endregion

		#region Calculators

		/*        public void UpdatePipeline(LayoutMask layout, Calculator calculator, VectorBrush brush)
				{
					bool layoutChanged = false;

					if (layout != Layout)
					{
						Layout = layout;
						layoutChanged = true;
					}

					if (mPathProducer == null || calculator != mPathProducer.PathPointCalculator || layoutChanged)
					{
						mPathProducer = new PathProducer(Layout, calculator)
						{
							KeepAllData = true
						};
					}

					if (mSmoothingFilter == null || layoutChanged)
					{
						mSmoothingFilter = new SmoothingFilter()
						{
							KeepAllData = true
						};
					}

					if (SplineProducer == null || layoutChanged)
					{
						SplineProducer = new SplineProducer()
						{
							KeepAllData = true
						};
					}

					if (SplineInterpolator == null || layoutChanged)
					{
						SplineInterpolator = new CurvatureBasedInterpolator()
						{
							KeepAllData = true
						};
					}

					if (BrushApplier == null || (brush != BrushApplier.Prototype) || layoutChanged)
					{
						BrushApplier = new BrushApplier(brush)
						{
							KeepAllData = true
						};
					}

					if (mConvexHullChainProducer == null)
					{
						mConvexHullChainProducer = new ConvexHullChainProducer()
						{
							KeepAllData = true
						};

					}

					if (mPolygonMerger == null)
					{
						mPolygonMerger = new PolygonMerger()
						{
							KeepAllData = true
						};

					}

					if (PolygonSimplifier == null)
					{
						PolygonSimplifier = new PolygonSimplifier(0.1f)
						{
							KeepAllData = true
						};

					}
				}*/

		#endregion
	}
}
