using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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
    /// Abstract base class for functionality shared between Raster and Vector rendering controls
    /// </summary>
    public abstract class InkControlBase : UserControl, IDisposable
    {
		#region Fields

		protected PointerManager mPointerManager = new PointerManager();
		protected DirtyRectManager mDirtyRectManager = new DirtyRectManager();
		protected object mInkBuilderLock = new object();
		protected RenderingContext mRenderingContext;

		protected Graphics mGraphics = new Graphics();
		protected Layer mWpfImageLayer;
		protected Layer mSceneLayer;
		protected Layer mAllStrokesLayer;
		protected Layer mPrelimPathLayer;
		protected Layer mCurrentStrokeLayer;

		protected Serializer mSerializer = new Serializer();

        #endregion

        #region Properties

        public abstract MediaColor BrushColor { get; set; }
        public MediaColor BackgroundColor { get; set; } = Colors.White;
        public abstract BrushType BrushType { get; }


		/// <summary>
		/// InkBuilder (Vector or Raster) handling pipeline stages for building ink
		/// </summary>
		protected abstract InkBuilder InkBuilder { get; }
        
        public Serializer Serializer { get => mSerializer; }

        /// <summary>
        /// Collection of saved stroke data
        /// </summary>
        protected abstract IEnumerable<object> AllStrokes { get; }

        /// <summary>
        /// DirectX D3D11Image used for rendering ink strokes (declared in derived class's XAML)
        /// </summary>
        protected abstract D3D11Image DxImage { get; }

        /// <summary>
        /// Image control hosting D3D11Image (declared in derived class's XAML)
        /// </summary>
        protected abstract Image ImageCtrl { get; }

        /// <summary>
        /// Grid control hosting Image (declared in derived class's XAML)
        /// </summary>
        protected abstract FrameworkElement ImageHost { get; }

        #endregion

        #region Public Interface

        /// <summary>
        /// Clear all saved strokes
        /// </summary>
        public virtual void ClearStrokes()
        {
            mSerializer = new Serializer();
            ClearLayers();
            RequestRender();
        }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// Update display with newly captured ink
        /// </summary>
        protected abstract void RenderNewStrokeSegment();

        /// <summary>
        /// Store data for newly completed stroke 
        /// </summary>
        protected abstract void StoreCurrentStroke(string pointerDeviceType);

        /// <summary>
        /// Do brush-type specific initialization at start of stroke
        /// </summary>
        protected abstract void DoPointerDown();

        /// <summary>
        /// Do brush-type specific initialization on loading of control
        /// </summary>
        protected abstract void DoControlLoaded();

        /// <summary>
        /// Do brush-type specific rendering of a stroke
        /// </summary>
        /// <param name="stroke">Brush-type specific stroke data</param>
        protected abstract void DoRenderStroke(object stroke);

        #endregion

        #region IDispose

        public virtual void Dispose()
        {
            StopProcessingInput();

            mGraphics?.Dispose();
            mRenderingContext?.Dispose();
            mWpfImageLayer?.Dispose();
            mSceneLayer?.Dispose();
            mAllStrokesLayer?.Dispose();
            mPrelimPathLayer?.Dispose();
            mCurrentStrokeLayer?.Dispose();

            DxImage.Dispose();
        }

        #endregion

        #region Mouse Event Handling

        /// <summary>
        /// Initiates capture of a new ink stroke
        /// </summary>
        /// <param name="sender">Object where the event handler is attached</param>
        /// <param name="e">Event data</param>
        protected void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            // If currently there is an unfinished stroke - do not interrupt it
            if (!mPointerManager.OnPressed(e))
                return;

            ImageCtrl.CaptureMouse();

            DoPointerDown();

            Point mousePoint = e.GetPosition(ImageCtrl);

            // The event timestamp (e.Timestamp) is not OK
            long usec = Utils.GetTimestampMicroseconds();

            lock (mInkBuilderLock)
            {
                InkBuilder.SetupForMouse(mGraphics);
                InkBuilder.AddPointFromMouseEvent(Phase.Begin, usec, mousePoint);
            }

            RequestRender();
        }

        /// <summary>
        /// Adds to ink stroke as a result of movement of the mouse 
        /// </summary>
        /// <param name="sender">Object where the event handler is attached</param>
        /// <param name="e">Event data</param>
        protected void OnMouseMove(object sender, MouseEventArgs e)
        {
            e.Handled = true;

            // Ignore events from other pointers
            if (!mPointerManager.OnMoved(e))
                return;

            Point mousePoint = e.GetPosition(ImageCtrl);

            // The event timestamp (e.Timestamp) is not OK
            long usec = Utils.GetTimestampMicroseconds();

            lock (mInkBuilderLock)
            {
                InkBuilder.AddPointFromMouseEvent(Phase.Update, usec, mousePoint);
            }

            RequestRender();
        }

        /// <summary>
        /// Completes ink stroke 
        /// </summary>
        /// <param name="sender">Object where the event handler is attached</param>
        /// <param name="e">Event data</param>
        protected void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            // Ignore events from other pointers
            if (!mPointerManager.OnReleased(e))
                return;

            ImageCtrl.ReleaseMouseCapture();

            Point mousePoint = e.GetPosition(ImageCtrl);

            // The event timestamp (e.Timestamp) is not OK
            long usec = Utils.GetTimestampMicroseconds();

            lock (mInkBuilderLock)
            {
                InkBuilder.AddPointFromMouseEvent(Phase.End, usec, mousePoint);
            }

            RenderNewStrokeSegment();

            lock (mInkBuilderLock)
            {
                StoreCurrentStroke("Mouse");
            }

            RequestRender();
        }

        #endregion

        #region Stylus Event Handling

        /// <summary>
        /// Initiates capture of a new ink stroke
        /// </summary>
        /// <param name="sender">Object where the event handler is attached</param>
        /// <param name="e">Event data</param>
        protected void OnStylusDown(object sender, StylusDownEventArgs e)
        {
            e.Handled = true;

            // If currently there is an unfinished stroke - do not interrupt it
            if (!mPointerManager.OnPressed(e))
                return;

            e.StylusDevice.Capture(ImageCtrl);

            DoPointerDown();

            StylusPointCollection stylusPoints = e.GetStylusPoints(ImageCtrl);

            // The event timestamp (e.Timestamp) is not OK
            long usec = Utils.GetTimestampMicroseconds();

            lock (mInkBuilderLock)
            {
                InkBuilder.SetupForStylus(stylusPoints.Description, mGraphics);
                InkBuilder.AddPointsFromStylusEvent(Phase.Begin, usec, stylusPoints);
            }

            RequestRender();
        }

        /// <summary>
        /// Adds to ink stroke as a result of movement of the stylus/pen
        /// </summary>
        /// <param name="sender">Object where the event handler is attached</param>
        /// <param name="e">Event data</param>
        protected void OnStylusMove(object sender, StylusEventArgs e)
        {
            e.Handled = true;

            // Ignore events from other pointers
            if (!mPointerManager.OnMoved(e))
                return;

            StylusPointCollection stylusPoints = e.GetStylusPoints(ImageCtrl);

            // The event timestamp (e.Timestamp) is not OK
            long usec = Utils.GetTimestampMicroseconds();

            lock (mInkBuilderLock)
            {
                InkBuilder.AddPointsFromStylusEvent(Phase.Update, usec, stylusPoints);
            }

            RequestRender();
        }

        /// <summary>
        /// Completes ink stroke following stylus/pen up event
        /// </summary>
        /// <param name="sender">Object where the event handler is attached</param>
        /// <param name="e">Event data</param>
        protected void OnStylusUp(object sender, StylusEventArgs e)
        {
            e.Handled = true;

            // Ignore events from other pointers
            if (!mPointerManager.OnReleased(e))
                return;

            e.StylusDevice.Capture(null);

            // The event timestamp (e.Timestamp) is not OK
            StylusPointCollection stylusPoints = e.GetStylusPoints(ImageCtrl);

            long usec = Utils.GetTimestampMicroseconds();

            lock (mInkBuilderLock)
            {
                InkBuilder.AddPointsFromStylusEvent(Phase.End, usec, stylusPoints);
            }

            RenderNewStrokeSegment();

            lock (mInkBuilderLock)
            {
                StoreCurrentStroke("Pen");
            }

            RequestRender();
        }

        #endregion

        #region Control Event Handling

        /// <summary>
        /// Registers event handlers
        /// </summary>
        protected void StartProcessingInput()
        {
            ImageCtrl.StylusDown += OnStylusDown;
            ImageCtrl.StylusMove += OnStylusMove;
            ImageCtrl.StylusUp += OnStylusUp;

            ImageCtrl.MouseDown += OnMouseDown;
            ImageCtrl.MouseMove += OnMouseMove;
            ImageCtrl.MouseUp += OnMouseUp;

            Stylus.SetIsTouchFeedbackEnabled(ImageCtrl, false);
            Stylus.SetIsPressAndHoldEnabled(ImageCtrl, false);

            ImageHost.Loaded += new RoutedEventHandler(OnControlLoaded);
            ImageHost.SizeChanged += new SizeChangedEventHandler(OnSizeChanged);

        }

        /// <summary>
        /// Unregisters event handlers
        /// </summary>
        protected void StopProcessingInput()
        {
            ImageCtrl.StylusDown -= OnStylusDown;
            ImageCtrl.StylusMove -= OnStylusMove;
            ImageCtrl.StylusUp -= OnStylusUp;

            ImageCtrl.MouseDown -= OnMouseDown;
            ImageCtrl.MouseMove -= OnMouseMove;
            ImageCtrl.MouseUp -= OnMouseUp;

            ImageHost.Loaded -= new RoutedEventHandler(OnControlLoaded);
            ImageHost.SizeChanged -= new SizeChangedEventHandler(OnSizeChanged);


        }

        /// <summary>
        /// Completes initializatoin once control finishes loading
        /// </summary>
        /// <remarks>Uses ImageHost.Loaded as proxy for control loaded</remarks>
        /// <param name="sender">Object where the event handler is attached</param>
        /// <param name="e">Event data</param>
        protected void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            if (mRenderingContext == null)
            {
                mGraphics.Initialize();
                mRenderingContext = mGraphics.GetRenderingContext();

                DoControlLoaded();
            }

            SetSize();

            DxImage.WindowOwner = (new System.Windows.Interop.WindowInteropHelper(Application.Current.MainWindow)).Handle;
            DxImage.OnRender = this.OnRender;
            RequestRender();
        }

        /// <summary>
        /// Handles change of control size
        /// </summary>
        /// <remarks>Uses ImageHost.SizeChanged as proxy for control size changed</remarks>
        /// <param name="sender">Object where the event handler is attached</param>
        /// <param name="e">Event data</param>
        protected void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.PreviousSize == new Size())
            {
                return;
            }

            SetSize();

            RequestRender();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="surfaceResourcePointer"></param>
        /// <param name="isNewSurface"></param>
        protected void OnRender(IntPtr surfaceResourcePointer, bool isNewSurface)
        {
            if (isNewSurface)
            {
                if (mWpfImageLayer != null)
                {
                    mWpfImageLayer.Dispose();
                }

                mWpfImageLayer = mGraphics.CreateLayerFromSharedSurface(surfaceResourcePointer);
            }
            Debug.Assert(mWpfImageLayer != null);

            RenderNewStrokeSegment();

            // Copy the scene to the WPF interop layer
            mRenderingContext.SetTarget(mWpfImageLayer);
            mRenderingContext.DrawLayer(mSceneLayer, null, Ink.Rendering.BlendMode.Copy);

            // Flush backbuffer to the screen
            mRenderingContext.Flush();
        }

        #endregion

        #region Implementation

        /// <summary>
        /// Redraw all saved strokes
        /// </summary>
        protected void RedrawAllStrokes()
        {
            mRenderingContext.SetTarget(mSceneLayer);
            mRenderingContext.ClearColor(BackgroundColor);

            mRenderingContext.SetTarget(mAllStrokesLayer);
            mRenderingContext.ClearColor(Colors.Transparent);

            //int n = 0;
            foreach (var stroke in AllStrokes)
            {
                // Draw current stroke
                mRenderingContext.SetTarget(mCurrentStrokeLayer);
                mRenderingContext.ClearColor(Colors.Transparent);

                DoRenderStroke(stroke);

                // Blend stroke to Scene Layer
                mRenderingContext.SetTarget(mSceneLayer);
                mRenderingContext.DrawLayer(mCurrentStrokeLayer, null, Ink.Rendering.BlendMode.SourceOver);

                // Blend Current Stroke to All Strokes Layer
                mRenderingContext.SetTarget(mAllStrokesLayer);
                mRenderingContext.DrawLayer(mCurrentStrokeLayer, null, Ink.Rendering.BlendMode.SourceOver);

                //++n;
            }

            // Clear CurrentStroke to prepare for next draw
            mRenderingContext.SetTarget(mCurrentStrokeLayer);
            mRenderingContext.ClearColor(Colors.Transparent);

        }

        /// <summary>
        /// Clears all layers
        /// </summary>
        protected void ClearLayers()
        {
            if (mRenderingContext != null)
            {
                mRenderingContext.SetTarget(mSceneLayer);
                mRenderingContext.ClearColor(BackgroundColor);

                mRenderingContext.SetTarget(mAllStrokesLayer);
                mRenderingContext.ClearColor(Colors.Transparent);

                mRenderingContext.SetTarget(mPrelimPathLayer);
                mRenderingContext.ClearColor(Colors.Transparent);

                mRenderingContext.SetTarget(mCurrentStrokeLayer);
                mRenderingContext.ClearColor(Colors.Transparent);

            }
        }

        private void SetLayerSize(Size size, float scale, ref Layer layer)
        {
            if (layer == null)
            {
                layer = mGraphics.CreateLayer(size, scale);
            }
            else if (layer.Size != size)
            {
                layer.Dispose();
                layer = mGraphics.CreateLayer(size, scale);
            }
        }

        private void SetLayersSize(Size size, float scale)
        {
            SetLayerSize(size, scale, ref mCurrentStrokeLayer);
            SetLayerSize(size, scale, ref mPrelimPathLayer);
            SetLayerSize(size, scale, ref mSceneLayer);
            SetLayerSize(size, scale, ref mAllStrokesLayer);
        }

        public void RequestRender()
        {
            DxImage.RequestRender();
        }

        private void SetSize()
        {
            double dpiScale = 1.0f; // default value for 96 dpi

            // determine DPI
            // (as of .NET 4.6.1, this returns the DPI of the primary monitor, if you have several different DPIs)
            if (PresentationSource.FromVisual(this).CompositionTarget is HwndTarget hwndTarget)
            {
                dpiScale = hwndTarget.TransformToDevice.M11;
            }

            mGraphics.SetDpi((float)(96.0 * dpiScale));

            double width = (ImageHost.ActualWidth < 0) ? 0 : ImageHost.ActualWidth;
            double height = (ImageHost.ActualHeight < 0) ? 0 : ImageHost.ActualHeight;

            int widthPx = (int)(ImageHost.ActualWidth < 0 ? 0 : Math.Ceiling(width * dpiScale));
            int heightPx = (int)(ImageHost.ActualHeight < 0 ? 0 : Math.Ceiling(height * dpiScale));

            // Notify the D3D11Image of the pixel size desired for the DirectX rendering.
            // The D3DRendering component will determine the size of the new surface it is given, at that point.
            DxImage.SetPixelSize(widthPx, heightPx);

            if (width > 0.0 && height > 0.0)
            {
                mRenderingContext.SetTarget(null);
                SetLayersSize(new Size(width, height), (float)dpiScale);
                ClearLayers();
                RedrawAllStrokes();
            }
        }

        #endregion

        public System.Drawing.Bitmap toBitmap(System.Windows.Media.Color backgroundColor)
        {
            Rect bounds = new Rect(0, 0, Width, Height);

            mRenderingContext.SetTarget(mSceneLayer);
            mRenderingContext.ClearColor(backgroundColor);

            // Blend stroke to Scene Layer
            mRenderingContext.SetTarget(mSceneLayer);
            mRenderingContext.DrawLayer(mAllStrokesLayer, null, Ink.Rendering.BlendMode.SourceOver);

            PixelData pixelData = mRenderingContext.ReadPixels(ref bounds);
            System.Drawing.Bitmap bmp = new System.Drawing.Bitmap((int)pixelData.m_pixelWidth, (int)pixelData.m_pixelHeight, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            BitmapData bmpData = bmp.LockBits(
                       new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                       ImageLockMode.WriteOnly, bmp.PixelFormat);

            System.Runtime.InteropServices.Marshal.Copy(pixelData.Data, 0, bmpData.Scan0, pixelData.Data.Length);
            bmp.UnlockBits(bmpData);

            return bmp;
        }
    }
}
