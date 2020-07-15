using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

using Microsoft.Win32;

using Wacom.Ink;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;
using Wacom.Ink.Serialization;

// Alias to avoid ambiguity with Wacom.Ink.Serialization.Model.Color
using MediaColor = System.Windows.Media.Color;

namespace Wacom
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MediaColor BrushColor = Colors.DarkBlue;

        public MainWindow()
        {
            InitializeComponent();

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
                BrushColor = MediaColor.FromRgb(colorDlg.Color.R, colorDlg.Color.G, colorDlg.Color.B);
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
                    writer.Write(Will3Codec.Encode(inkCtrl.Serializer.InkDocument));
                }
            }
        }

        private void OnLoad_Click(object sender, RoutedEventArgs e)
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
                    var inkDocument = Will3Codec.Decode(buff);
                    InkControlBase inkCtrl = null;

                    if (inkDocument.Brushes.RasterBrushes.Count > 0 && inkDocument.Brushes.VectorBrushes.Count > 0)
                    {
                        MessageBox.Show("This sample does not support serialization of both raster and vector brushes");
                    }
                    else if (inkDocument.Brushes.RasterBrushes.Count > 0)
                    {
                        SetCurrentControl(inkCtrl = new RasterInkControl(RasterBrushStyle.Pencil, BrushColor, inkDocument));
                        ToggleBrushButton(BtnPencil);
                    }
                    else if (inkDocument.Brushes.VectorBrushes.Count > 0)
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
    }

 
}
