using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Akka.Actor;

namespace ChartApp.Actors
{
    public class ChartingActor : ReceiveActor
    {
        #region Messages

        public class InitializeChart
        {
            public InitializeChart(Dictionary<string, Series> initialSeries)
            {
                InitialSeries = initialSeries;
            }

            public Dictionary<string, Series> InitialSeries { get; private set; }
        }

        public class AddSeries
        {
            public AddSeries(Series series)
            {
                Series = series;
            }

            public Series Series { get; }
        }

        public class RemoveSeries
        {
            public string Series { get; }

            public RemoveSeries(string series)
            {
                Series = series;
            }
        }

        public class TogglePause { }

        #endregion

        // max points in series
        private const int MaxPoints = 250;

        // incrementing counter to plot along x
        private int xPosCounter = 0;

        private readonly Chart _chart;
        private readonly Button _pauseButton;
        private Dictionary<string, Series> _seriesIndex;

        public ChartingActor(Chart chart, Button pauseButton) : this(chart, pauseButton, new Dictionary<string, Series>())
        {
        }

        public ChartingActor(Chart chart, Button pauseButton, Dictionary<string, Series> seriesIndex)
        {
            _chart = chart;
            _pauseButton = pauseButton;
            _seriesIndex = seriesIndex;

            // start in Charting mode
            Charting();
        }

        private void Charting()
        {
            // add receive handlers
            Receive<InitializeChart>(ic => HandleInitialize(ic));
            Receive<Metric>(met => HandleMetrics(met));
            Receive<AddSeries>(ser => HandleAddSeries(ser));
            Receive<RemoveSeries>(ser => HandleRemoveSeries(ser));

            Receive<TogglePause>(pause =>
            {
                _pauseButton.BackColor = Color.LightGoldenrodYellow;
                BecomeStacked(Paused);
            });
        }

        private void Paused()
        {
            Receive<Metric>(metric => HandleMetricsPaused(metric));
            Receive<TogglePause>(pause =>
            {
                _pauseButton.BackColor = Color.LightGreen;
                UnbecomeStacked();
            });
        }

        

        #region Individual Message Type Handlers

        private void HandleInitialize(InitializeChart ic)
        {
            if (ic.InitialSeries != null)
            {
                // swap the two series out
                _seriesIndex = ic.InitialSeries;
            }

            // delete any existing series
            _chart.Series.Clear();

            // set the axes up
            var area = _chart.ChartAreas[0];
            area.AxisX.IntervalType = DateTimeIntervalType.Number;
            area.AxisY.IntervalType = DateTimeIntervalType.Number;

            SetChartBoundaries();

            // attempt to render the initial chart
            if (_seriesIndex.Any())
            {
                foreach (var series in _seriesIndex)
                {
                    // force both the chart and the internal index to use the same names
                    series.Value.Name = series.Key;
                    _chart.Series.Add(series.Value);
                }
            }

            SetChartBoundaries();
        }

        private void HandleAddSeries(AddSeries series)
        {
            if (!string.IsNullOrEmpty(series.Series.Name) && !_seriesIndex.ContainsKey(series.Series.Name))
            {
                _seriesIndex.Add(series.Series.Name, series.Series);
                _chart.Series.Add(series.Series);
                SetChartBoundaries();
            }
        }

        private void HandleRemoveSeries(RemoveSeries msg)
        {
            if (string.IsNullOrEmpty(msg.Series)) return;
            if (_seriesIndex.ContainsKey(msg.Series))
            {
                _chart.Series.Remove(_seriesIndex[msg.Series]);
                _seriesIndex.Remove(msg.Series);
            }
            SetChartBoundaries();
        }

        private void HandleMetrics(Metric metric)
        {
            if (!string.IsNullOrEmpty(metric.Series) &&
                _seriesIndex.ContainsKey(metric.Series))
            {
                var series = _seriesIndex[metric.Series];
                var points = series?.Points;
                if (points != null)
                {
                    points.AddXY(xPosCounter++, metric.CounterValue);
                    while (points.Count > MaxPoints) points.RemoveAt(0);
                }
                SetChartBoundaries();
            }
        }

        private void HandleMetricsPaused(Metric metric)
        {
            if (!string.IsNullOrEmpty(metric.Series)
                && _seriesIndex.ContainsKey(metric.Series))
            {
                var series = _seriesIndex[metric.Series];
                // set the Y value to zero when we're paused
                series.Points.AddXY(xPosCounter++, 0.0d);
                while (series.Points.Count > MaxPoints) series.Points.RemoveAt(0);
                SetChartBoundaries();
            }
        }

        #endregion


        private void SetChartBoundaries()
        {
            double maxAxisX, maxAxisY, minAxisX, minAxisY = 0.0d;
            List<DataPoint> allPoints = new List<DataPoint>();
            foreach (var points in _seriesIndex.Values.Select(series => series?.Points))
            {
                if (points != null) allPoints.AddRange(points);
            }
            var yValues = allPoints.SelectMany(point => point.YValues).ToList();
            maxAxisX = xPosCounter;
            minAxisX = xPosCounter - MaxPoints;
            maxAxisY = yValues.Count > 0 ? Math.Ceiling(yValues.Max()) : 1.0d;
            minAxisY = yValues.Count > 0 ? Math.Floor(yValues.Min()) : 0.0d;
            if (allPoints.Count > 2)
            {
                var area = _chart.ChartAreas[0];
                area.AxisX.Minimum = minAxisX;
                area.AxisX.Maximum = maxAxisX;
                area.AxisY.Minimum = minAxisY;
                area.AxisY.Maximum = maxAxisY;
            }
        }
    }
}
