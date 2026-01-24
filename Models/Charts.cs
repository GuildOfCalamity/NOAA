using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace NOAA
{
    public class ChartPoint
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
        public string Uom { get; set; } // unit of measure
        public ChartPoint(DateTime time, double value, string uom)
        {
            Time = time;
            Value = value;
            Uom = uom;
        }
    }

    public class ChartSeries
    {
        public List<ChartPoint> Points { get; set; } = new();
        public Brush Stroke { get; set; } = Brushes.DeepSkyBlue;
        public Brush Fill { get; set; } = Brushes.LightSkyBlue;
        public double StrokeThickness { get; set; } = 3.5;

        #region [Gridlines]
        public Brush GridPen { get; set; } = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        public double GridThickness { get; set; } = 1.25;
        #endregion

        #region [Static points]
        public bool ShowPoints { get; set; } = true;
        #endregion
    }
}
