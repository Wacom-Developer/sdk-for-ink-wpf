using System;
using System.Collections.Generic;
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
    /// <summary>
    /// Interaction logic for RasterInkControl.xaml
    /// </summary>
    public partial class RasterInkControl : InkControlBase, IDisposable
    {
        #region Fields

        /// <summary>
        /// Stores a completed ("dry") stroke
        /// </summary>
        class DryStroke
        {
            public ParticleList Path { get; }
            public StrokeConstants StrokeConstants { get; }
            public uint RandomSeed { get; }

            public DryStroke(ParticleList path, uint seed, StrokeConstants StrokeParams)
            {
                Path = path;
                RandomSeed = seed;
                StrokeConstants = StrokeParams;
            }

        }

        private RasterInkBuilder mInkBuilder = null;
        private uint mStartRandomSeed;
        private Random mRand = new Random();
        private RasterBrushStyle mBrushStyle;

        private DrawStrokeResult mDrawStrokeResult;
        private ParticleList mAddedInterpolatedSpline;
        private ParticleList mPredictedInterpolatedSpline;
        private List<DryStroke> mDryStrokes = new List<DryStroke>();
        private StrokeConstants mStrokeConstants = new StrokeConstants();

        #endregion

        #region Properties

        protected override InkBuilder InkBuilder => mInkBuilder;
        protected override D3D11Image DxImage => _DxImage;
        protected override Image ImageCtrl => _ImageCtrl;
        protected override FrameworkElement ImageHost => _ImageHost;
        protected override IEnumerable<object> AllStrokes => mDryStrokes;
        public override BrushType BrushType { get { return BrushType.Raster; } }

        public override MediaColor BrushColor {
            get { return mStrokeConstants.Color; }
            set { mStrokeConstants.Color = value; }
        }

        public void SetBrushStyle(RasterBrushStyle  brushStyle)
        {
            CreateBrush(brushStyle);
        }

        #endregion

        #region Constructor
        public RasterInkControl(RasterBrushStyle brushStyle, MediaColor color)//
        {
            InitializeComponent();

            mInkBuilder = new RasterInkBuilder();
            mInkBuilder.LayoutUpdated += InkBuilder_LayoutUpdated;

            BrushColor = color;

            mBrushStyle = brushStyle;

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

            mStrokeConstants?.Dispose();
            //mBrush?.Dispose();
            mAddedInterpolatedSpline?.Dispose();
            mPredictedInterpolatedSpline?.Dispose();

            foreach (var ds in mDryStrokes)
            {
                ds.Path.Dispose();
                ds.StrokeConstants.Dispose();
            }
        }

        #endregion

        #region Event Support

        protected override void DoPointerDown()
        {
            mStartRandomSeed = (uint)mRand.Next();
            mDrawStrokeResult.RandomGeneratorSeed = mStartRandomSeed;
        }

        protected override void DoControlLoaded()
        {
            CreateBrush(mBrushStyle);
        }

        private void InkBuilder_LayoutUpdated(object sender, EventArgs e)
        {
            uint channelMask = (uint)mInkBuilder.SplineInterpolator.InterpolatedSplineLayout.ChannelMask;
            mAddedInterpolatedSpline = new ParticleList(channelMask);
            mPredictedInterpolatedSpline = new ParticleList(channelMask);
        }

        private void CreateBrush(RasterBrushStyle brushStyle)
        {
            mInkBuilder.SetBrushStyle(brushStyle, mGraphics);
            mBrushStyle = brushStyle;
        }

        #endregion

        #region Stroke Handling
        protected override void RenderNewStrokeSegment()
        {
            //var result = mInkBuilder.GetPath();
            ProcessorResult<List<float>> result;

            lock (mInkBuilderLock)
            {
                if (mInkBuilder.HasNewPoints)
                {
                    result = mInkBuilder.GetPath();
                }
                else
                {
                    return;
                }
            }

            uint mChannelMask = (uint)mInkBuilder.SplineInterpolator.InterpolatedSplineLayout.ChannelMask;

            mAddedInterpolatedSpline.Assign(result.Addition);
            mPredictedInterpolatedSpline.Assign(result.Prediction);

            ParticleBrush brush = mInkBuilder.Brush;

            // Draw the added stroke
            mRenderingContext.SetTarget(mCurrentStrokeLayer);
            mDrawStrokeResult = mRenderingContext.DrawParticleStroke(mAddedInterpolatedSpline, mStrokeConstants, brush, Ink.Rendering.BlendMode.SourceOver, mDrawStrokeResult.RandomGeneratorSeed);

            // Measure the predicted stroke
            Rect predictedStrokeRect = mRenderingContext.MeasureParticleStrokeBounds(mPredictedInterpolatedSpline, mStrokeConstants, brush.Scattering);

            // Calculate the update rect for this frame
            Rect updateRect = mDirtyRectManager.GetUpdateRect(mDrawStrokeResult.DirtyRect, predictedStrokeRect);

            // Draw the predicted stroke
            mRenderingContext.SetTarget(mPrelimPathLayer);
            mRenderingContext.DrawLayerAtPoint(mCurrentStrokeLayer, updateRect, new Point(updateRect.X, updateRect.Y), Ink.Rendering.BlendMode.Copy);

            mRenderingContext.DrawParticleStroke(mPredictedInterpolatedSpline, mStrokeConstants, brush, Ink.Rendering.BlendMode.SourceOver, mDrawStrokeResult.RandomGeneratorSeed);

            // Reconstruct the scene under the current stroke (only within the updated rect)
            mRenderingContext.SetTarget(mSceneLayer, updateRect);
            mRenderingContext.ClearColor(BackgroundColor);
            mRenderingContext.DrawLayerAtPoint(mAllStrokesLayer, updateRect, new Point(updateRect.X, updateRect.Y), Ink.Rendering.BlendMode.SourceOver);

            // Blend the current stroke on top (only within the updated rect)
            mRenderingContext.DrawLayerAtPoint(mPrelimPathLayer, updateRect, new Point(updateRect.X, updateRect.Y), Ink.Rendering.BlendMode.SourceOver);
        }

        protected override void StoreCurrentStroke(string pointerDeviceType)
        {
            var allData = mInkBuilder.SplineInterpolator.AllData;

            var points = new List<float>();

            if (allData != null)
            {
                for (int i = 0; i < allData.Count; i++)
                {
                    points.Add(allData[i]);
                }

                if (points.Count > 0)
                {
                    uint channelMask = (uint)mInkBuilder.SplineInterpolator.InterpolatedSplineLayout.ChannelMask;
                    ParticleList path = new ParticleList(channelMask);
                    path.Assign(points);
                    mDryStrokes.Add(new DryStroke(path, mStartRandomSeed, mStrokeConstants.Clone()));
                }
            }

            mRenderingContext.SetTarget(mAllStrokesLayer);
            mRenderingContext.DrawLayer(mCurrentStrokeLayer, null, Ink.Rendering.BlendMode.SourceOver);

            mRenderingContext.SetTarget(mCurrentStrokeLayer);
            mRenderingContext.ClearColor(Colors.Transparent);

            mDirtyRectManager.Reset();

            mSerializer.EncodeCurrentStroke(pointerDeviceType, mInkBuilder, mStrokeConstants, mStartRandomSeed);
        }

        protected override void DoRenderStroke(object o)
        {
            DryStroke stroke = (DryStroke)o;

            mRenderingContext.DrawParticleStroke(stroke.Path, stroke.StrokeConstants, mInkBuilder.Brush, Ink.Rendering.BlendMode.SourceOver, stroke.RandomSeed);
        }


        #endregion

        #region Serialization Support

        private List<DryStroke> RecreateDryStrokes(InkModel inkDataModel)
        {
            if (inkDataModel.InkTree.Root == null)
                return new List<DryStroke>();

            List<DryStroke> dryStrokes = new List<DryStroke>(inkDataModel.Strokes.Count);

            DecodedRasterInkBuilder decodedRasterInkBuilder = new DecodedRasterInkBuilder();

            foreach (var stroke in inkDataModel.Strokes)
            {
                dryStrokes.Add(CreateDryStroke(decodedRasterInkBuilder, stroke, inkDataModel));
            }

            return dryStrokes;
        }

        private DryStroke CreateDryStroke(DecodedRasterInkBuilder decodedVectorInkBuilder, Stroke stroke, InkModel inkDataModel)
        {
            inkDataModel.Brushes.TryGetBrush(stroke.Style.BrushUri, out Wacom.Ink.Serialization.Model.Brush brush);

            if (brush is Wacom.Ink.Serialization.Model.VectorBrush vectorBrush)
            {
                throw new Exception("This sample does not support serialization of both raster and vector brushes");
            }
            else if (brush is RasterBrush rasterBrush)
            {
                return CreateDryStrokeFromRasterBrush(decodedVectorInkBuilder, rasterBrush, stroke);
            }
            else
            {
                throw new Exception("Brush not recognized");
            }
        }

        private DryStroke CreateDryStrokeFromRasterBrush(DecodedRasterInkBuilder decodedRasterInkBuilder, RasterBrush rasterBrush, Stroke stroke)
        {
            var result = decodedRasterInkBuilder.AddWholePath(stroke.Spline.Data, rasterBrush.Spacing, stroke.Layout);

            List<float> points = new List<float>(result.Addition);

            uint channelMask = (uint)decodedRasterInkBuilder.SplineInterpolator.InterpolatedSplineLayout.ChannelMask;

            ParticleList particleList = new ParticleList(channelMask);
            particleList.Assign(points);

            PathPointProperties ppp = stroke.Style.PathPointProperties;

            StrokeConstants strokeConstants = new StrokeConstants
            {
                Color = MediaColor.FromArgb(
                            ppp.Alpha.HasValue ? (byte)(ppp.Alpha * 255.0f) : byte.MinValue,
                            ppp.Red.HasValue ? (byte)(ppp.Red * 255.0f) : byte.MinValue,
                            ppp.Green.HasValue ? (byte)(ppp.Green * 255.0f) : byte.MinValue,
                            ppp.Blue.HasValue ? (byte)(ppp.Blue * 255.0f) : byte.MinValue)
            };
            DryStroke dryStroke = new DryStroke(particleList, stroke.Style.RandomSeed, strokeConstants);

            return dryStroke;
        }

        private class DecodedRasterInkBuilder
        {
            #region Fields

            private const int splitCount = 1;

            #endregion

            #region Properties

            public SplineInterpolator SplineInterpolator { get; private set; }

            #endregion

            public ProcessorResult<List<float>> AddWholePath(List<float> path, float spacing, PathPointLayout layout)
            {
                if (path.Count == 0)
                    throw new Exception("Path has no points!");

                SplineInterpolator = new DistanceBasedInterpolator(layout, spacing, splitCount, true, true);

                var iterpolatedPoints = SplineInterpolator.Add(true, true, new Spline(layout.ChannelMask, path), new Spline(layout.ChannelMask));

                return iterpolatedPoints;
            }
        }

        #endregion
    }

}
