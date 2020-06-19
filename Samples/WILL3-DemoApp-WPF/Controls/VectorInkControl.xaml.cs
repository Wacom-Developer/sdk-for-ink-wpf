using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using Microsoft.Wpf.Interop.DirectX;


using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;
using Wacom.Ink.Serialization.Model;


// Alias to avoid ambiguity with Wacom.Ink.Serialization.Model.Color
using MediaColor = System.Windows.Media.Color;

namespace Wacom
{
    using PolygonVertices = List<Vector2>;

    /// <summary>
    /// Interaction logic for VectorInkControl.xaml
    /// </summary>
    public partial class VectorInkControl : InkControlBase, IDisposable
    {

        #region Fields

        /// <summary>
        /// Stores a completed ("dry") stroke
        /// </summary>
        class DryStroke
        {
            public MediaColor mColor;
            public Wacom.Ink.Rendering.Polygon mPolygon;
        }

        private VectorBrushStyle mBrushStyle;

        private VectorInkBuilder mInkBuilder = null;
        private Wacom.Ink.Rendering.Polygon mAddedPolygon = new Wacom.Ink.Rendering.Polygon();
        private Wacom.Ink.Rendering.Polygon mPredictedPolygon = new Wacom.Ink.Rendering.Polygon();
        private List<DryStroke> mDryStrokes = new List<DryStroke>();

        #endregion

        #region Properties

        protected override InkBuilder InkBuilder => mInkBuilder;
        protected override D3D11Image DxImage => _DxImage;
        protected override Image ImageCtrl => _ImageCtrl;
        protected override FrameworkElement ImageHost => _ImageHost;
        protected override IEnumerable<object> AllStrokes => mDryStrokes;
        public override MediaColor BrushColor { get; set; }
        public override BrushType BrushType { get { return BrushType.Vector; } }

        public VectorBrushStyle BrushStyle {
            get { return mBrushStyle; }
            set
            {
                mBrushStyle = value; 
                mInkBuilder.BrushStyle = value;
            }

        }
        #endregion

        #region Constructor
        public VectorInkControl(VectorBrushStyle brushStyle, MediaColor brushColor)
        {
            InitializeComponent();

            mBrushStyle = brushStyle;
            mInkBuilder = new VectorInkBuilder(brushStyle);

            BrushColor = brushColor;

            StartProcessingInput();
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Clear all saved strokes
        /// </summary>
        public override void ClearStrokes()
        {
            mDryStrokes.Clear();
            base.ClearStrokes();
        }

        /// <summary>
        /// Loads serialized ink
        /// </summary>
        public override void LoadInk(InkModel inkDocument)
        {
            base.LoadInk(inkDocument);
            mDryStrokes = new List<DryStroke>(RecreateDryStrokes(inkDocument));
        }

        #endregion

        #region IDispose

        public override void Dispose()
        {
            base.Dispose();

            mAddedPolygon?.Dispose();
            mPredictedPolygon?.Dispose();

            foreach (var ds in mDryStrokes)
            {
                ds.mPolygon.Dispose();
            }
        }

        #endregion

        #region Event Support

        protected override void DoPointerDown()
        {
        }

        protected override void DoControlLoaded()
        {
        }

        #endregion

        #region Stroke Handling

        protected override void RenderNewStrokeSegment()
        {
            ProcessorResult<List<PolygonVertices>> result;

            lock (mInkBuilderLock)
            {
                if (mInkBuilder.HasNewPoints)
                {
                    result = mInkBuilder.GetPolygons();
                }
                else
                {
                    return;
                }
            }

            //if (!mInkBuilder.HasNewPoints)
            //    return;

            //var result = mInkBuilder.GetPolygons();

            PolygonUtil.ConvertPolygon(result.Addition, mAddedPolygon);
            PolygonUtil.ConvertPolygon(result.Prediction, mPredictedPolygon);

            // Draw the added stroke
            mRenderingContext.SetTarget(mCurrentStrokeLayer);
            Rect addedStrokeRect = mRenderingContext.FillPolygon(mAddedPolygon, BrushColor, Wacom.Ink.Rendering.BlendMode.Max);

            // Measure the predicted stroke
            Rect predictedStrokeRect = mRenderingContext.MeasurePolygonBounds(mPredictedPolygon);

            // Calculate the  rect for this frame
            Rect updateRect = mDirtyRectManager.GetUpdateRect(addedStrokeRect, predictedStrokeRect);

            // Draw the predicted stroke
            mRenderingContext.SetTarget(mPrelimPathLayer);
            mRenderingContext.DrawLayerAtPoint(mCurrentStrokeLayer, updateRect, new Point(updateRect.X, updateRect.Y), Wacom.Ink.Rendering.BlendMode.Copy);
            mRenderingContext.FillPolygon(mPredictedPolygon, BrushColor, Wacom.Ink.Rendering.BlendMode.Max);

            // Reconstruct the scene under the current stroke (only within the updated rect)
            mRenderingContext.SetTarget(mSceneLayer, updateRect);
            mRenderingContext.ClearColor(BackgroundColor);
            mRenderingContext.DrawLayerAtPoint(mAllStrokesLayer, updateRect, new Point(updateRect.X, updateRect.Y), Wacom.Ink.Rendering.BlendMode.SourceOver);

            // Blend the current stroke on top (only within the updated rect)
            mRenderingContext.DrawLayerAtPoint(mPrelimPathLayer, updateRect, new Point(updateRect.X, updateRect.Y), Wacom.Ink.Rendering.BlendMode.SourceOver);
        }

        protected override void StoreCurrentStroke(string pointerDeviceType)
        {
            var polygons = mInkBuilder.PolygonSimplifier.AllData;
            var mergedPolygons = PolygonUtils.MergePolygons(polygons);
            Wacom.Ink.Rendering.Polygon polygon = PolygonUtil.ConvertPolygon(mergedPolygons);

            mDryStrokes.Add(new DryStroke { mPolygon = polygon, mColor = BrushColor });

            mRenderingContext.SetTarget(mAllStrokesLayer);
            mRenderingContext.DrawLayer(mCurrentStrokeLayer, null, Wacom.Ink.Rendering.BlendMode.SourceOver);

            mRenderingContext.SetTarget(mCurrentStrokeLayer);
            mRenderingContext.ClearColor(Colors.Transparent);

            mDirtyRectManager.Reset();
            mSerializer.EncodeCurrentStroke(pointerDeviceType, mInkBuilder, BrushColor);
        }

        protected override void DoRenderStroke(object o)
        {
            DryStroke stroke = (DryStroke)o;
            mRenderingContext.FillPolygon(stroke.mPolygon, stroke.mColor, Wacom.Ink.Rendering.BlendMode.SourceOver);
        }

        #endregion

        #region Serialization Support

        private List<DryStroke> RecreateDryStrokes(InkModel inkDataModel)
        {
            if (inkDataModel.InkTree.Root == null)
                return new List<DryStroke>();

            List<DryStroke> dryStrokes = new List<DryStroke>(inkDataModel.Strokes.Count);

            DecodedVectorInkBuilder decodedVectorInkBuilder = new DecodedVectorInkBuilder();

            foreach (var stroke in inkDataModel.Strokes)
            {
                dryStrokes.Add(CreateDryStroke(decodedVectorInkBuilder, stroke, inkDataModel));
            }

            return dryStrokes;
        }

        private DryStroke CreateDryStroke(DecodedVectorInkBuilder decodedVectorInkBuilder, Stroke stroke, InkModel inkDataModel)
        {
            inkDataModel.Brushes.TryGetBrush(stroke.Style.BrushUri, out Wacom.Ink.Serialization.Model.Brush brush);

            if (brush is Wacom.Ink.Serialization.Model.VectorBrush vectorBrush)
            {
                return CreateDryStrokeFromVectorBrush(decodedVectorInkBuilder, vectorBrush, stroke);
            }
            else if (brush is RasterBrush rasterBrush)
            {
                throw new Exception("This sample does not support serialization of both raster and vector brushes");
            }
            else
            {
                throw new Exception("Brush not recognized");
            }
        }

        private DryStroke CreateDryStrokeFromVectorBrush(DecodedVectorInkBuilder decodedVectorInkBuilder, Wacom.Ink.Serialization.Model.VectorBrush vectorBrush, Stroke stroke)
        {
            Wacom.Ink.Geometry.VectorBrush vb = new Wacom.Ink.Geometry.VectorBrush(vectorBrush.BrushPolygons.ToArray());
            var result = decodedVectorInkBuilder.AddWholePath(stroke.Spline, stroke.Layout, vb);

            PathPointProperties ppp = stroke.Style.PathPointProperties;

            DryStroke dryStroke = new DryStroke
            {
                mPolygon = PolygonUtil.ConvertPolygon(result.Addition),
                mColor = MediaColor.FromArgb(
                            ppp.Alpha.HasValue ? (byte)(ppp.Alpha * 255.0f) : byte.MinValue,
                            ppp.Red.HasValue ? (byte)(ppp.Red * 255.0f) : byte.MinValue,
                            ppp.Green.HasValue ? (byte)(ppp.Green * 255.0f) : byte.MinValue,
                            ppp.Blue.HasValue ? (byte)(ppp.Blue * 255.0f) : byte.MinValue)
            };

            return dryStroke;
        }

        private class DecodedVectorInkBuilder
        {
            private ConvexHullChainProducer mConvexHullChainProducer = new ConvexHullChainProducer();
            private PolygonMerger mPolygonMerger = new PolygonMerger();
            private readonly PolygonSimplifier mPolygonSimplifier = new PolygonSimplifier(0.1f);

            public ProcessorResult<List<List<Vector2>>> AddWholePath(Spline path, PathPointLayout layout, Wacom.Ink.Geometry.VectorBrush vectorBrush)
            {
                var splineInterpolator = new CurvatureBasedInterpolator(layout);
                var brushApplier = new BrushApplier(layout, vectorBrush);

                var points = splineInterpolator.Add(true, true, path, null);

                var polys = brushApplier.Add(true, true, points.Addition, points.Prediction);

                var hulls = mConvexHullChainProducer.Add(true, true, polys.Addition, polys.Prediction);

                var merged = mPolygonMerger.Add(true, true, hulls.Addition, hulls.Prediction);

                //var simplified = m_polygonSimplifier.Add(true, true, merged.Item1, merged.Item2);
                //return simplified;

                return merged;
            }
        }

        #endregion

    }
}
