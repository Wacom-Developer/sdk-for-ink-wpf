using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Wacom
{
    /// <summary>
    /// Interaction logic for OptionsDialog.xaml
    /// </summary>
    public partial class OptionsDialog : Window
    {
        // Illustrative set of brush colors 
        private static readonly Dictionary<Color, string> ColorMapping = new Dictionary<Color, string>
        {
            { Colors.Black, "Black" },
            { Colors.Blue,  "Blue"  },
            { Colors.Red,   "Red"  },
            { Colors.Green, "Green" }
        };

        public Dictionary<Color, string> BrushColors { get { return ColorMapping; } }

        public IList<BrushType> BrushTypes
        {
            get { return Enum.GetValues(typeof(BrushType)).Cast<BrushType>().ToList<BrushType>(); }
        }
        public IList<BrushThickness> BrushThicknesses
        {
            get { return Enum.GetValues(typeof(BrushThickness)).Cast<BrushThickness>().ToList<BrushThickness>(); }
        }
        public IList<VectorBrushShape> VectorBrushShapes
        {
            get { return Enum.GetValues(typeof(VectorBrushShape)).Cast<VectorBrushShape>().ToList<VectorBrushShape>(); }
        }

        BrushOptions m_options;

        public BrushType BrushType
        {
            get { return m_options.Type; }
            set { m_options.Type = value; }
        }
        public BrushThickness BrushThickness
        {
            get { return m_options.Thickness; }
            set { m_options.Thickness = value; }
        }
        public Color BrushColor
        {
            get { return m_options.Color; }
            set { m_options.Color = value; }
        }
        public VectorBrushShape VectorBrushShape
        {
            get { return m_options.Shape; }
            set { m_options.Shape = value; }
        }

        public OptionsDialog(BrushOptions options)
        {
            InitializeComponent();

            this.DataContext = this;
            m_options = options;
        }

        #region Event Handling
        private void OnBrushTypeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbxVectBrushShape != null)
            {
                cbxVectBrushShape.IsEnabled = (cbxBrushType.SelectedIndex == 0);
            }
        }

        #endregion

        private void OnOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
