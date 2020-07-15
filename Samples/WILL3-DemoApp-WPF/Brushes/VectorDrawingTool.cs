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

        /// <summary>
        /// Calculator delegate for input from mouse input
        /// Calculates the path point properties based on pointer input.
        /// </summary>
        /// <param name="previous"></param>
        /// <param name="current"></param>
        /// <param name="next"></param>
        /// <returns>PathPoint with calculated properties</returns>
        protected PathPoint CalculatorForMouseAndTouch(PointerData previous, PointerData current, PointerData next)
        {
            var size = current.ComputeValueBasedOnSpeed(previous, next, SizeConfig.minValue, SizeConfig.maxValue, SizeConfig.initValue, SizeConfig.finalValue, SizeConfig.minSpeed, SizeConfig.maxSpeed, SizeConfig.remap);

            if (size.HasValue)
            {
                PreviousSize = size.Value;
            }
            else
            {
                size = PreviousSize;
            }

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = size
            };

            return pp;
        }

    }

    /// <summary>
    /// Vector drawing tool for rendering pen-style output
    /// </summary>
    class PenTool : VectorDrawingTool
    {
        private static readonly ToolConfig mConfig = new ToolConfig()
        {
            minSpeed = 180,
            maxSpeed = 2100,
            minValue = 1.5f,
            maxValue = 3,
            initValue = 1.5f,
            finalValue = 1.5f,
            remap = v => (float)Math.Pow(v, 0.35f)
        };

        protected override float PreviousSize { get; set; } = 1.5f;

        public override PathPointLayout GetLayoutMouse()
        {
            return new PathPointLayout(PathPoint.Property.X,
                                    PathPoint.Property.Y,
                                    PathPoint.Property.Size);
        }

        public override PathPointLayout GetLayoutStylus()
        {
            return new PathPointLayout(PathPoint.Property.X,
                                    PathPoint.Property.Y,
                                    PathPoint.Property.Size);
        }

        public override Calculator GetCalculatorMouse()
        {
            return CalculatorForMouseAndTouch;
        }

        public override Calculator GetCalculatorStylus()
        {
            return CalculatorForMouseAndTouch;
        }

        /// <summary>
        /// Calculator delegate for input from a stylus (pen)
        /// Calculates the path point properties based on pointer input.
        /// </summary>
        /// <param name="previous"></param>
        /// <param name="current"></param>
        /// <param name="next"></param>
        /// <returns>PathPoint with calculated properties</returns>
        private PathPoint CalculatorForStylus(PointerData previous, PointerData current, PointerData next)
        {
            if (!current.Force.HasValue)
            {
                return CalculatorForMouseAndTouch(previous, current, next);
            }
            else
            {
                var size = ComputeValueBasedOnPressure(current, 1.5f, 3f, 180f, 2100f, false, v => (float)Math.Pow(v, 0.35f));

                if (size.HasValue)
                {
                    PreviousSize = size.Value;
                }
                else
                {
                    size = PreviousSize;
                }

                PathPoint pp = new PathPoint(current.X, current.Y)
                {
                    Size = size
                };
                return pp;
            }
        }

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
            minSpeed = 80,
            maxSpeed = 1400,
            minValue = 3,
            maxValue = 7,
            initValue = 3f,
            finalValue = 3f,
            remap = v => (float)Math.Pow(v, 0.65f)
        };

        public override VectorBrush Shape => mCircleBrush;
        protected override ToolConfig SizeConfig => mConfig;

        protected override float PreviousSize { get; set; } = 2f;

        public override PathPointLayout GetLayoutMouse()
        {
            return new PathPointLayout(PathPoint.Property.X,
                                        PathPoint.Property.Y,
                                        PathPoint.Property.Size);
        }
        public override PathPointLayout GetLayoutStylus()
        {
            return new PathPointLayout(PathPoint.Property.X,
                                        PathPoint.Property.Y,
                                        PathPoint.Property.Size,
                                        PathPoint.Property.Rotation,
                                        PathPoint.Property.ScaleX,
                                        PathPoint.Property.OffsetX); 
        }

        public override Calculator GetCalculatorMouse()
        {
            return CalculatorForMouseAndTouch;
        }

        public override Calculator GetCalculatorStylus()
        {
            return CalculatorForStylus;
        }

        /// <summary>
        /// Calculator delegate for input from a stylus (pen)
        /// Calculates the path point properties based on pointer input.
        /// </summary>
        /// <param name="previous"></param>
        /// <param name="current"></param>
        /// <param name="next"></param>
        /// <returns>PathPoint with calculated properties</returns>
        private PathPoint CalculatorForStylus(PointerData previous, PointerData current, PointerData next)
        {
            float? size;

            if (!current.Force.HasValue)
            {
                size = current.ComputeValueBasedOnSpeed(previous, next, 1, 5, null, null, 0, 3500, v => (float)Math.Pow(v, 1.17f));
            }
            else
            {
                size = ComputeValueBasedOnPressure(current, 1, 5, 0, 1, false, v => (float)Math.Pow(v, 1.17f));
            }
            if (size.HasValue)
            {
                PreviousSize = size.Value;
            }
            else
            {
                size = PreviousSize;
            }


            var cosAltitudeAngle = (float)Math.Abs(Math.Cos(current.AltitudeAngle.Value));

            var tiltScale = 1.5f * cosAltitudeAngle;
            var scaleX = 1.0f + tiltScale;
            var offsetX = size * tiltScale;
            var rotation = current.ComputeNearestAzimuthAngle(previous);

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = size,
                Rotation = rotation,
                ScaleX = scaleX,
                OffsetX = offsetX
            };
            return pp;
        }
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
            minValue = 10,
            maxValue = 17.2f,
            initValue = 10f,
            finalValue = 10f,
            remap = v => (float)Math.Pow(v, 1.19f)
        };

        public override VectorBrush Shape => mCircleBrush;
        protected override ToolConfig SizeConfig => mConfig; 
        protected override float? Alpha => 0.7f;

        protected override float PreviousSize { get; set; } = 10;


        public override PathPointLayout GetLayoutMouse()
        {
            return new PathPointLayout(PathPoint.Property.X,
                                        PathPoint.Property.Y,
                                        PathPoint.Property.Size);
        }

        public override PathPointLayout GetLayoutStylus()
        {
            return new PathPointLayout(PathPoint.Property.X,
                                        PathPoint.Property.Y,
                                        PathPoint.Property.Size,
                                        PathPoint.Property.Rotation,
                                        PathPoint.Property.ScaleX,
                                        PathPoint.Property.OffsetX);
        }

        public override Calculator GetCalculatorMouse()
        {
            return CalculatorForMouseAndTouch;
        }

        public override Calculator GetCalculatorStylus()
        {
            return CalculatorForStylus;
        }

        /// <summary>
        /// Calculator delegate for input from a stylus (pen)
        /// Calculates the path point properties based on pointer input.
        /// </summary>
        /// <param name="previous"></param>
        /// <param name="current"></param>
        /// <param name="next"></param>
        /// <returns>PathPoint with calculated properties</returns>
        private PathPoint CalculatorForStylus(PointerData previous, PointerData current, PointerData next)
        {
            float? size;

            if (!current.Force.HasValue)
            {
                size = current.ComputeValueBasedOnSpeed(previous, next, 1.5f, 10.2f, null, null, 0, 3500, v => (float)Math.Pow(v, 1.17f));
            }
            else
            {
                size = ComputeValueBasedOnPressure(current, 1.5f, 10.2f, 0, 1, false, v => (float)Math.Pow(v, 1.17f));
            }
            if (size.HasValue)
            {
                PreviousSize = size.Value;
            }
            else
            {
                size = PreviousSize;
            }


            var cosAltitudeAngle = (float)Math.Abs(Math.Cos(current.AltitudeAngle.Value));

            var tiltScale = 1.5f * cosAltitudeAngle;
            var scaleX = 1.0f + tiltScale;
            var offsetX = size * tiltScale;
            var rotation = current.ComputeNearestAzimuthAngle(previous);

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = size,
                Rotation = rotation,
                ScaleX = scaleX,
                OffsetX = offsetX
            };
            return pp;
        }
    }
}
