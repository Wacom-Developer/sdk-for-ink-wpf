using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Wacom.Ink.Geometry;
using Wacom.Ink.Serialization.Model;

namespace Wacom.Export
{
    class SVGExporter
    {
        private ConvexHullChainProducer mConvexHullChainProducer = new ConvexHullChainProducer();
        private PolygonMerger mPolygonMerger = new PolygonMerger();
        private readonly PolygonSimplifier mPolygonSimplifier = new PolygonSimplifier(0.1f);

        private float minX = float.MaxValue;
        private float minY = float.MaxValue;
        private float maxX = 0.0f;
        private float maxY = 0.0f;


        public String exportToSVG(InkModel inkDocument, float svgWidth, float svgHeight, bool fit)
        {
            System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";

            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

            XmlDocument document = new XmlDocument();
            var svg = document.CreateElement("svg", "http://www.w3.org/2000/svg");
            document.AppendChild(svg);

            // first of all we need to get the PostScript drawing commands from the stroke list
            drawStrokes(document, svg, inkDocument, svgWidth, svgHeight, fit);

            if (fit)
            {
                svg.SetAttribute("viewBox", "0 0 " + svgWidth + " " + svgHeight);
            }
            else
            {
                svg.SetAttribute("viewBox", "0 0 " + Math.Max(svgWidth, maxX) + " " + Math.Max(svgHeight, maxY));
            }

            using (var stringWriter = new StringWriter())
            using (var xmlTextWriter = XmlWriter.Create(stringWriter))
            {
                document.WriteTo(xmlTextWriter);
                xmlTextWriter.Flush();
                return stringWriter.GetStringBuilder().ToString();
            }
        }

        private void drawStrokes(XmlDocument document, XmlElement svg, InkModel inkDocument, float svgWidth, float svgHeight, bool fit)
        {
            var inkGroup = document.CreateElement("g", "http://www.w3.org/2000/svg");
            svg.AppendChild(inkGroup);

            if (inkDocument.InkTree.Root != null)
            {
                IEnumerator<InkNode> enumerator = inkDocument.InkTree.Root.GetRecursiveEnumerator();

                while (enumerator.MoveNext())
                {
                    if ((enumerator.Current is StrokeNode strokeNode))
                    {
                        inkDocument.Brushes.TryGetBrush(strokeNode.Stroke.Style.BrushUri, out Wacom.Ink.Serialization.Model.Brush brush);

                        // only exports vector strokes
                        if (brush is Wacom.Ink.Serialization.Model.VectorBrush vectorBrush)
                        {
                            Wacom.Ink.Geometry.VectorBrush vb;

                            if (vectorBrush.BrushPolygons.Count > 0)
                            {
                                vb = new Wacom.Ink.Geometry.VectorBrush(vectorBrush.BrushPolygons.ToArray());
                            }
                            else if (vectorBrush.BrushPrototypeURIs.Count > 0)
                            {
                                List<BrushPolygon> brushPolygons = new List<BrushPolygon>(vectorBrush.BrushPrototypeURIs.Count);

                                foreach (var uri in vectorBrush.BrushPrototypeURIs)
                                {
                                    brushPolygons.Add(BrushPolygon.CreateNormalized(uri.MinScale, ShapeUriResolver.ResolveShape(uri.ShapeUri)));
                                }

                                vb = new Wacom.Ink.Geometry.VectorBrush(brushPolygons.ToArray());
                            }
                            else
                            {
                                continue;
                            }

                            drawStroke(document, inkGroup, strokeNode.Stroke, vb);
                        }
                    }
                }

                if (fit)
                {
                    // if fit we put a transformation matrix scaling the strokes
                    float scaleX = svgWidth / maxX;
                    float scaleY = svgHeight / maxY;
                    float scale = Math.Min(scaleX, scaleY);
                    inkGroup.SetAttribute("transform", "matrix(" + scale + ",0,0," + scale + ",0,0)");
                }
            }

        }

        private void drawStroke(XmlDocument document, XmlElement inkGroup, Stroke stroke, Wacom.Ink.Geometry.VectorBrush vectorBrush)
        {
            var splineInterpolator = new CurvatureBasedInterpolator();
            var brushApplier = new BrushApplier(vectorBrush);

            var points = splineInterpolator.Add(true, true, stroke.Spline.ToSpline(), null);
            var polys = brushApplier.Add(true, true, points.Addition, points.Prediction);
            var hulls = mConvexHullChainProducer.Add(true, true, polys.Addition, polys.Prediction);
            var merged = mPolygonMerger.Add(true, true, hulls.Addition, hulls.Prediction);
            var simplified = mPolygonSimplifier.Add(true, true, merged.Addition, merged.Prediction);

            var color = System.Drawing.Color.FromArgb((int)(stroke.Style.PathPointProperties.Red * 255),
                                          (int)(stroke.Style.PathPointProperties.Green * 255),
                                          (int)(stroke.Style.PathPointProperties.Blue * 255));

            string hex = "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");

            var inkPath = document.CreateElement("path", "http://www.w3.org/2000/svg");
            inkPath.SetAttribute("fill", hex);
            inkPath.SetAttribute("fill-opacity", stroke.Style.PathPointProperties.Alpha.ToString());
            inkPath.SetAttribute("d", drawPolygon(simplified.Addition));
            inkGroup.AppendChild(inkPath);
        }

        private String drawPolygon(List<List<Vector2>> polygon)
        {
            if (polygon.Count == 0)
            {
                return "";
            }

            var path = new StringBuilder();
            foreach (var poly in polygon)
            {
                for (int j = 0; j < poly.Count; j++)
                {
                    var p = poly[j];

                    if (j == 0)
                    {
                        path.Append(" M ").Append(p.X).Append(" ").Append(p.Y);
                    }
                    else
                    {
                        path.Append(" L ").Append(p.X).Append(" ").Append(p.Y);
                    }

                    if (p.X > maxX)
                    {
                        maxX = p.X;
                    }
                    if (p.X < minX)
                    {
                        minX = p.X;
                    }
                    if (p.Y > maxY)
                    {
                        maxY = p.Y;
                    }
                    if (p.Y < minY)
                    {
                        minY = p.Y;
                    }
                }

            }

            return path.ToString();
        }

    }
}
