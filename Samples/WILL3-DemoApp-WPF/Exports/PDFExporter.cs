using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wacom.Ink.Geometry;
using Wacom.Ink.Serialization.Model;

namespace Wacom.Export
{
    class PDFExporter
    {
        public static float PDF_A4_WIDTH = 595.0f;
        public static float PDF_A4_HEIGHT = 842.0f;

        private static string PDF_TEMPLATE = "%PDF-1.4\n" +
                "%âãÏÓ\n" +
                "1 0 obj\n" +
                "<</Type/Page/Parent 3 0 R/Contents 2 0 R/MediaBox[0 0 $1$ $2$]/Resources<</ProcSet[/PDF]/ExtGState<<$3$>>>>>>\n" +
                "endobj\n" +
                "2 0 obj\n" +
                "<</Length $4$>>stream\n" +
                "$5$\n" +
                "endstream\n" +
                "endobj\n" +
                "3 0 obj\n" +
                "<</Type/Pages/Count 1/Kids[1 0 R]>>\n" +
                "endobj\n" +
                "4 0 obj\n" +
                "<</Type/Catalog/Pages 3 0 R>>\n" +
                "endobj\n" +
                "5 0 obj\n" +
                "<</Producer($6$)/CreationDate(D:$7$)/ModDate(D:$8$)>>\n" +
                "endobj\n" +
                "xref\n" +
                "0 6\n" +
                "0000000000 65535 f\n" +
                "$9$ 00000 n\n" +
                "$10$ 00000 n\n" +
                "$11$ 00000 n\n" +
                "$12$ 00000 n\n" +
                "$13$ 00000 n\n" +
                "trailer\n" +
                "<</Size 6/Root 4 0 R/Info 5 0 R/ID[<$14$><$15$>]>>\n" +
                "startxref\n" +
                "$16$\n" +
                "%%EOF";

        private ConvexHullChainProducer mConvexHullChainProducer = new ConvexHullChainProducer();
        private PolygonMerger mPolygonMerger = new PolygonMerger();
        private readonly PolygonSimplifier mPolygonSimplifier = new PolygonSimplifier(0.1f);

        private float minX = float.MaxValue;
        private float minY = float.MaxValue;
        private float maxX = 0.0f;
        private float maxY = 0.0f;

        private StringBuilder psCommands = new StringBuilder(); // PostScript commands for drawing the inking
        private List<float> graphicStates = new List<float>(); // Store the alphas

        public String ExportToPDF(InkModel inkDocument, float pdfWidth, float pdfHeight, bool fit)
        {
            System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";

            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

            // first of all we need to get the PostScript drawing commands from the stroke list
            DrawStrokes(inkDocument, pdfWidth, pdfHeight, fit);

            string commands = psCommands.ToString();

            string date = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "Z";

            string pdf = PDF_TEMPLATE.Replace("$1$", pdfWidth.ToString())
                                     .Replace("$2$", pdfHeight.ToString())
                                     .Replace("$3$", GetGSStates())
                                     .Replace("$4$", commands.Length.ToString())
                                     .Replace("$5$", commands)
                                     .Replace("$6$", "Wacom")
                                     .Replace("$7$", date)
                                     .Replace("$8$", date);

            pdf = pdf.Replace("$9$", this.Fill(pdf.IndexOf("1 0 obj")))
                     .Replace("$10$", this.Fill(pdf.IndexOf("2 0 obj")))
                     .Replace("$11$", this.Fill(pdf.IndexOf("3 0 obj")))
                     .Replace("$12$", this.Fill(pdf.IndexOf("4 0 obj")))
                     .Replace("$13$", this.Fill(pdf.IndexOf("5 0 obj")))
                     .Replace("$14$", System.Guid.NewGuid().ToString().Replace("-", ""))
                     .Replace("$15$", System.Guid.NewGuid().ToString().Replace("-", ""));

            pdf = pdf.Replace("$16$", pdf.IndexOf("xref").ToString());

            return pdf;
        }

        private string GetGSStates()
        {
            StringBuilder gsStates = new StringBuilder();
            for (int index = 0; index < graphicStates.Count; index++)
            {
                gsStates.Append("/GS").Append(index + 1).Append("<</ca ").Append(graphicStates[index]).Append(">>");
            }

            return gsStates.ToString();
        }

        private string Fill(int offset)
        {
            string str = "0000000000" + offset;
            return str.Substring(str.Length - 10);
        }

        private void DrawStrokes(InkModel inkDocument, float pdfWidth, float pdfHeight, bool fit)
        {
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

                            DrawStroke(strokeNode.Stroke, vb);
                        }
                    }
                }

                if (fit)
                {
                    // if fit we put a transformation matrix scaling the strokes
                    float scaleX = pdfWidth / maxX;
                    float scaleY = pdfHeight / maxY;
                    float scale = Math.Min(scaleX, scaleY);
                    String matrix = scale.ToString() + " 0 0 " + -scale + " 0 " + pdfHeight + " cm\n";
                    psCommands.Insert(0, matrix);
                }
                else
                {
                    // Strokes have Y coordinates from top = 0 to bottom = maxHeight,
                    // while PDF have Y coordinates from bottom = 0 to top = maxHeight,
                    // so we need to flip the Y coordinates. We do it with the following transformation matrix.
                    String matrix = "1 0 0 -1 0 " + pdfHeight + " cm\n";
                    psCommands.Insert(0, matrix);
                }
            }

        }

        private void DrawStroke(Stroke stroke, Wacom.Ink.Geometry.VectorBrush vectorBrush)
        {
            psCommands.Append("q\n"); //save the graphics state
            float alpha = stroke.Style.PathPointProperties.Alpha;
            if (!graphicStates.Contains(alpha))
            {
                graphicStates.Add(alpha);
            }

            psCommands.Append("/GS").Append(graphicStates.IndexOf(alpha) + 1).Append(" gs\n"); //put the alpha state
            psCommands.Append(stroke.Style.PathPointProperties.Red).Append(" ").Append(stroke.Style.PathPointProperties.Green).Append(" ").Append(stroke.Style.PathPointProperties.Blue).Append(" rg\n"); //put the stroke color

            var splineInterpolator = new CurvatureBasedInterpolator();
            var brushApplier = new BrushApplier(vectorBrush);

            var points = splineInterpolator.Add(true, true, stroke.Spline.ToSpline(), null);
            var polys = brushApplier.Add(true, true, points.Addition, points.Prediction);
            var hulls = mConvexHullChainProducer.Add(true, true, polys.Addition, polys.Prediction);
            var merged = mPolygonMerger.Add(true, true, hulls.Addition, hulls.Prediction);
            var simplified = mPolygonSimplifier.Add(true, true, merged.Addition, merged.Prediction);

            foreach (var poly in simplified.Addition)
            {
                for (int j = 0; j < poly.Count; j++)
                {
                    var p = poly[j];

                    if (j == 0)
                    {
                        psCommands.Append(p.X).Append(" ").Append(p.Y).Append(" m ");
                    }
                    else
                    {
                        psCommands.Append(p.X).Append(" ").Append(p.Y).Append(" l ");
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

                //psCommands.Append(" h ");
            }
            psCommands.Append("f ");

        }

    }
}
