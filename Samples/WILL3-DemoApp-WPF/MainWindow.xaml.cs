using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Wacom.Export;

using UimCodec = Wacom.Ink.Serialization.UimCodec;

namespace Wacom
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private Color BrushColor = Colors.DarkBlue;

		public MainWindow()
		{
			InitializeComponent();

			try
			{
				// For development and testing, we read our license string from a file
				// For production code, license string could be hard coded and passed 
				// directly to SetLicense
				string licenseFile = @"WacomLicense.txt";
				if (File.Exists(licenseFile))
				{
					using (var licenseStream = File.OpenText(licenseFile))
					{
						var license = licenseStream.ReadToEnd();
						if (!string.IsNullOrEmpty(license))
						{
							Wacom.Licensing.LicenseValidator.Instance.SetLicense(license);
						}
					}
				}
			}
			catch (Exception)
			{
				// Assumed to be invalid license, which will be reported below.
				// For more detail, catch individual Wacom.Licensing.License*Exception types
			}
			if (!Wacom.Licensing.LicenseValidator.Instance.HasLicense)
			{
				MessageBox.Show("WILL SDK for Ink is not licensed. Some functionality is not enabled");
			}

			CheckControlType(VectorBrushStyle.Pen, BtnPen);
			btnColor.Background = new SolidColorBrush(BrushColor);
		}

		private void SetCurrentControl(UserControl newControl)
		{
			IDisposable disposable = NavFrame.Content as IDisposable;

			disposable?.Dispose();

			NavFrame.Content = newControl;
		}

		private void OnExit_Click(object sender, RoutedEventArgs e)
		{
			Application.Current.Shutdown();
		}


		private void OnClear_Click(object sender, RoutedEventArgs e)
		{
			InkControlBase inkCtrl = NavFrame.Content as InkControlBase;
			inkCtrl?.ClearStrokes();
		}

		private ToggleButton mCurrentBrushBtn = null;

		private void CheckControlType(VectorBrushStyle brushStyle, ToggleButton btn)
		{
			var inkCtrl = NavFrame.Content as VectorInkControl;

			if (inkCtrl == null)
				SetCurrentControl(new VectorInkControl(brushStyle, BrushColor));
			else
				inkCtrl.BrushStyle = brushStyle;
			ToggleBrushButton(btn);
		}

		private void CheckControlType(RasterBrushStyle brushStyle, ToggleButton btn)
		{
			var inkCtrl = NavFrame.Content as RasterInkControl;

			if (inkCtrl == null)
				SetCurrentControl(new RasterInkControl(brushStyle, BrushColor));
			else
				inkCtrl.SetBrushStyle(brushStyle);
			ToggleBrushButton(btn);
		}

		private void ToggleBrushButton(ToggleButton btn)
		{
			if (mCurrentBrushBtn != null)
				mCurrentBrushBtn.IsChecked = false;
			mCurrentBrushBtn = btn;
			mCurrentBrushBtn.IsChecked = true;
		}

		private void OnColor_Click(object sender, RoutedEventArgs e)
		{
			var inkCtrl = NavFrame.Content as InkControlBase;
			var colorDlg = new System.Windows.Forms.ColorDialog
			{
				AllowFullOpen = false,
				Color = System.Drawing.Color.FromArgb((BrushColor.R << 16) | (BrushColor.G << 8) | BrushColor.B)
			};

			if (colorDlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				BrushColor = Color.FromRgb(colorDlg.Color.R, colorDlg.Color.G, colorDlg.Color.B);
				btnColor.Background = new SolidColorBrush(BrushColor);

				inkCtrl.BrushColor = BrushColor;
			}
		}

		private void OnPen_Click(object sender, RoutedEventArgs e)
		{
			CheckControlType(VectorBrushStyle.Pen, (ToggleButton)sender);
		}

		private void OnFelt_Click(object sender, RoutedEventArgs e)
		{
			CheckControlType(VectorBrushStyle.Felt, (ToggleButton)sender);
			BtnPen.IsChecked = false;
		}

		private void OnBrush_Click(object sender, RoutedEventArgs e)
		{
			CheckControlType(VectorBrushStyle.Brush, (ToggleButton)sender);
		}

		private void OnPencil_Click(object sender, RoutedEventArgs e)
		{
			CheckControlType(RasterBrushStyle.Pencil, (ToggleButton)sender);
		}

		private void OnWaterBrush_Click(object sender, RoutedEventArgs e)
		{
			CheckControlType(RasterBrushStyle.WaterBrush, (ToggleButton)sender);
		}

		private void OnCrayon_Click(object sender, RoutedEventArgs e)
		{
			CheckControlType(RasterBrushStyle.Crayon, (ToggleButton)sender);
		}

		private void OnSave_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				SaveFileDialog saveFileDlg = new SaveFileDialog()
				{
					Filter = "WILL3 file (*.uim)|*.uim|All files (*.*)|*.*"
				};
				if (saveFileDlg.ShowDialog() == true)
				{
					//MessageBox.Show(saveFileDlg.FileName);
					using (BinaryWriter writer = new BinaryWriter(File.Open(saveFileDlg.FileName, FileMode.Create)))
					{
						var inkCtrl = NavFrame.Content as InkControlBase;
						writer.Write(UimCodec.Encode(inkCtrl.Serializer.InkDocument));
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Exception: {ex.Message}");
			}
		}

		private void OnLoad_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				OpenFileDialog openFileDlg = new OpenFileDialog()
				{
					Filter = "WILL3 file (*.uim)|*.uim|All files (*.*)|*.*"
				};
				if (openFileDlg.ShowDialog() == true)
				{
					using (BinaryReader reader = new BinaryReader(File.Open(openFileDlg.FileName, FileMode.Open)))
					{
						var info = new FileInfo(openFileDlg.FileName);
						byte[] buff = new byte[info.Length];

						reader.Read(buff, 0, (int)info.Length);
						var inkDocument = UimCodec.Decode(buff);
						InkControlBase inkCtrl = null;

						if (inkDocument.Brushes.RasterBrushes.Any() && inkDocument.Brushes.VectorBrushes.Any())
						{
							MessageBox.Show("This sample does not support serialization of both raster and vector brushes");
						}
						else if (inkDocument.Brushes.RasterBrushes.Any())
						{
							SetCurrentControl(inkCtrl = new RasterInkControl(RasterBrushStyle.Pencil, BrushColor, inkDocument));
							ToggleBrushButton(BtnPencil);
						}
						else if (inkDocument.Brushes.VectorBrushes.Any())
						{
							SetCurrentControl(inkCtrl = new VectorInkControl(VectorBrushStyle.Pen, BrushColor, inkDocument));
							ToggleBrushButton(BtnPen);
						}
						else
						{
							inkCtrl = NavFrame.Content as InkControlBase;
							inkCtrl?.ClearStrokes();
						}
					}

				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Exception: {ex.Message}");
			}
		}

		private void ExportToPDF_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				SaveFileDialog saveFileDlg = new SaveFileDialog()
				{
					Filter = "PDF (*.pdf)|*pdf",
					DefaultExt = ".pdf"
				};
				if (saveFileDlg.ShowDialog() == true)
				{
					using (BinaryWriter writer = new BinaryWriter(File.Open(saveFileDlg.FileName, FileMode.Create)))
					{
						PDFExporter pdfExporter = new PDFExporter();
						var inkCtrl = NavFrame.Content as InkControlBase;
						var pdf = pdfExporter.ExportToPDF(inkCtrl.Serializer.InkDocument, PDFExporter.PDF_A4_WIDTH, PDFExporter.PDF_A4_HEIGHT, true);
						writer.Write(Encoding.UTF8.GetBytes(pdf));
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Exception: {ex.Message}");
			}
		}

		private void ExportToSVG_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				SaveFileDialog saveFileDlg = new SaveFileDialog()
				{
					Filter = "SVG (*.svg)|*svg",
					DefaultExt = ".svg"
				};
				if (saveFileDlg.ShowDialog() == true)
				{
					using (BinaryWriter writer = new BinaryWriter(File.Open(saveFileDlg.FileName, FileMode.Create)))
					{
						SVGExporter svgExporter = new SVGExporter();
						var inkCtrl = NavFrame.Content as InkControlBase;
						var svg = svgExporter.ExportToSVG(inkCtrl.Serializer.InkDocument, (float)Width, (float)Height, true);
						writer.Write(Encoding.UTF8.GetBytes(svg));
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Exception: {ex.Message}");
			}
		}

		private void ExportToPNG_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				SaveFileDialog saveFileDlg = new SaveFileDialog()
				{
					Filter = "PNG (*.png)|*png",
					DefaultExt = ".png"
				};
				if (saveFileDlg.ShowDialog() == true)
				{
					InkControlBase inkCtrl = NavFrame.Content as InkControlBase;
					System.Drawing.Bitmap bmp = inkCtrl?.ToBitmap(Colors.Transparent);
					bmp.Save(saveFileDlg.FileName, System.Drawing.Imaging.ImageFormat.Png);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Exception: {ex.Message}");
			}

		}

		private void ExportToJPEG_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				SaveFileDialog saveFileDlg = new SaveFileDialog()
				{
					Filter = "JPEG (*.jpeg)|*jpeg",
					DefaultExt = ".jpg"
				};
				if (saveFileDlg.ShowDialog() == true)
				{
					InkControlBase inkCtrl = NavFrame.Content as InkControlBase;
					System.Drawing.Bitmap bmp = inkCtrl?.ToBitmap(Colors.White);
					bmp.Save(saveFileDlg.FileName, System.Drawing.Imaging.ImageFormat.Jpeg);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Exception: {ex.Message}");
			}
		}
	}
}
