using System;
using System.Windows.Input;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Wacom.Ink.Geometry;

namespace Wacom
{
    /// <summary>
    /// Abstract base class for raster based drawing tools 
    /// </summary>
    abstract class VectorDrawingTool : DrawingTool
    {
        protected static readonly VectorBrush mCircleBrush = new VectorBrush(
            new BrushPolygon(0.0f, VectorBrushFactory.CreateEllipseBrush(4, 1.0f, 1.0f)),
            new BrushPolygon(2.0f, VectorBrushFactory.CreateEllipseBrush(8, 1.0f, 1.0f)),
            new BrushPolygon(6.0f, VectorBrushFactory.CreateEllipseBrush(16, 1.0f, 1.0f)),
            new BrushPolygon(18.0f, VectorBrushFactory.CreateEllipseBrush(32, 1.0f, 1.0f)));

        public abstract VectorBrush Shape { get; }

    }

    /// <summary>
    /// Vector drawing tool for rendering pen-style output
    /// </summary>
    class PenTool : VectorDrawingTool
    {
        private static readonly ToolConfig mConfig = new ToolConfig()
        {
            minSpeed = 5,
            maxSpeed = 210,
            minValue = 0.5f,
            maxValue = 1.6f,
            remap = v => (1 + 0.62f) * v / ((float)Math.Abs(v) + 0.62f)
        };

        public override VectorBrush Shape => mCircleBrush; 
        protected override ToolConfig SizeConfig => mConfig; 
    }

    /// <summary>
    /// Vector drawing tool for rendering felt pen-style output
    /// </summary>
    class FeltTool : VectorDrawingTool
    {
        private static readonly ToolConfig mConfig = new ToolConfig()
        {
            minSpeed = 33,
            maxSpeed = 628,
            minValue = 1.03f,
            maxValue = 2.43f,
            remap = v => 0.5f - 0.5f * (float)Math.Cos(3 * Math.PI * v)
        };

        public override VectorBrush Shape => mCircleBrush;
        protected override ToolConfig SizeConfig => mConfig; 
    }

    /// <summary>
    /// Vector drawing tool for rendering brush-style output
    /// </summary>
    class BrushTool : VectorDrawingTool
    {
        private static readonly ToolConfig mConfig = new ToolConfig()
        {
            minSpeed = 182,
            maxSpeed = 3547,
            minValue = 3.4f,
            maxValue = 17.2f,
            remap = v => (float)Math.Pow(v, 1.19),
        };

        public override VectorBrush Shape => mCircleBrush;
        protected override ToolConfig SizeConfig => mConfig; 
        protected override float? Alpha => 0.7f;
    }
}
