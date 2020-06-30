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

        #endregion

        #region Constructors

        public RasterInkBuilder()
        {
        }

        #endregion

        #region Properties

        public ParticleBrush Brush => ActiveTool.Brush;

        public event EventHandler LayoutUpdated;

        RasterBrushStyle mBrushStyle = RasterBrushStyle.Pencil;

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
        #endregion

        /// <summary>
        /// Transform accumulated pointer input to ink geometry
        /// </summary>
        /// <returns>Tuple containing added data (Item1) and predicted or preliminary data (Item2)</returns>
        /// <remarks>Passes accumulated path segment (from PathProducer) through remaining stages of 
        /// the raster ink pipeline - Smoother, SplineProducer & SplineInterpolator</remarks>
        public ProcessorResult<List<float>> GetPath()
        {
            var smoothPath = mSmoothingFilter.Add(mPathSegment.IsFirst, mPathSegment.IsLast, mPathSegment.AccumulatedAddition, mPathSegment.LastPrediction);

            var spline = SplineProducer.Add(mPathSegment.IsFirst, mPathSegment.IsLast, smoothPath.Addition, smoothPath.Prediction);

            var points = SplineInterpolator.Add(mPathSegment.IsFirst, mPathSegment.IsLast, spline.Addition, spline.Prediction);

            mPathSegment.Reset();
            mPointerDataUpdateCount = 0;

            return points;
        }

        #region Public Interface

        public override void SetupForStylus(StylusPointDescription sd, Graphics graphics)
        {
            SetBrushStyle(mBrushStyle, graphics);
            UpdatePipeline(ActiveTool.GetLayoutStylus(), ActiveTool.GetCalculatorStylus(), ActiveTool.ParticleSpacing);
        }

        public void SetupForTouch(Graphics graphics)
        {
            SetBrushStyle(mBrushStyle, graphics);
            UpdatePipeline(ActiveTool.GetLayoutMouse(), ActiveTool.GetCalculatorMouse(), ActiveTool.ParticleSpacing);
        }

        public override void SetupForMouse(Graphics graphics)
        {
            SetBrushStyle(mBrushStyle, graphics);
            UpdatePipeline(ActiveTool.GetLayoutMouse(), ActiveTool.GetCalculatorMouse(), ActiveTool.ParticleSpacing);
        }

        public void UpdatePipeline(PathPointLayout layout, Calculator calculator, float spacing)
        {
            bool layoutChanged = false;
            bool otherChange = false;

            if ((Layout == null) || (layout.ChannelMask != Layout.ChannelMask))
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
                mSmoothingFilter = new SmoothingFilter(Layout.Count)
                {
                    KeepAllData = true
                };
                otherChange = true;
            }

            if (SplineProducer == null || layoutChanged)
            {
                SplineProducer = new SplineProducer(Layout, true);
                otherChange = true;
            }

            if (SplineInterpolator == null || layoutChanged)
            {
                SplineInterpolator = new DistanceBasedInterpolator(Layout, spacing, splitCount, true, true, true);
                otherChange = true;
            }
            ((DistanceBasedInterpolator)SplineInterpolator).Spacing = spacing;

            if (layoutChanged || otherChange)
            {
                LayoutUpdated?.Invoke(this, EventArgs.Empty);
            }
        }       

        public Wacom.Ink.Serialization.Model.RasterBrush CreateSerializationBrush(string name)
        {
            return new Wacom.Ink.Serialization.Model.RasterBrush(name,
                                            (float)Brush.FillTileSize.Width, (float)Brush.FillTileSize.Height,
                                            true,
                                            (RotationMode)Brush.RotationMode,
                                            Brush.Scattering,
                                            ((DistanceBasedInterpolator)SplineInterpolator).Spacing,
                                            ActiveTool.Fill.ImageFileData,
                                            new List<byte[]>() { ActiveTool.Shape.ImageFileData },
                                            new List<string>(),
                                            string.Empty,
                                            Wacom.Ink.Serialization.Model.BlendMode.SourceOver
                                           );
        }
        #endregion
    }
}
