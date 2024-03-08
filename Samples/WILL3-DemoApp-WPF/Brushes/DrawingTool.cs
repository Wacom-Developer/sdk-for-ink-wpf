using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Wacom.Ink.Geometry;

namespace Wacom
{
    /// <summary>
    /// Abstract base class for drawing tools used to render ink in a variety of styles
    /// </summary>
    abstract class DrawingTool
    {
        /// <summary>
        /// Holds configuration parameters for PathPoint calculators
        /// </summary>
        protected class ToolConfig
        {
            public float minSpeed;
            public float maxSpeed;

            public float minValue;
            public float maxValue;

            public float? initValue;
            public float? finalValue;

            public Func<float, float> remap;
        };

        protected abstract float PreviousSize { get; set; }

        /// <summary>
        /// Configuration parameters to use in PathPoint Size calculations
        /// </summary>
        protected abstract ToolConfig SizeConfig { get; }

        /// <summary>
        /// Configuration parameters to use in PathPoint Alpha calculations
        /// </summary>
        /// <remarks>For this sample, Alpha calcuation, based on speed, is demonstrated in <see cref="MouseInputCalculator"/></remarks>
        protected virtual ToolConfig AlphaConfig => null;

        /// <summary>
        /// Fixed value to set LayoutMask Alpha 
        /// </summary>
        /// <remarks>For this sample, a fixed Alpha is used in <see cref="StylusInputCalculator"/></remarks>
        protected virtual float? Alpha => 1;

        public abstract LayoutMask GetLayoutMouse();
        public abstract LayoutMask GetLayoutStylus();
        public abstract Calculator GetCalculatorMouse();
        public abstract Calculator GetCalculatorStylus();

        protected float? ComputeValueBasedOnPressure(PointerData pointerData, float minValue, float maxValue,
            float minPressure = 100f, float maxPressure = 4000f, bool reverse = false, Func<float, float> remap = null)
        {
            if (!pointerData.Force.HasValue)
                throw new InvalidOperationException("");

            float normalizePressure = (reverse)
                                    ? minPressure + (1 - pointerData.Force.Value) * (maxPressure - minPressure)
                                    : minPressure + pointerData.Force.Value * (maxPressure - minPressure);

            var pressureClamped = Math.Min(Math.Max(normalizePressure, minPressure), maxPressure);
            var k = (pressureClamped - minPressure) / (maxPressure - minPressure);
            if (remap != null)
                k = remap(k);

            return minValue + k * (maxValue - minValue);
        }
    }
}
