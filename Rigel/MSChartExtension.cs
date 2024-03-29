﻿using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Drawing;
using System.Diagnostics;

namespace System.Windows.Forms.DataVisualization.Charting
{
    /// <summary>
    /// Extension class for MSChart
    /// </summary>
    public static class MSChartExtension
    {
        private static Chart chart;

        /// <summary>
        /// Chart control delegate function prototype.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public delegate void CursorPositionChanged(double x, double y, double xValue, double y1Value, double y2Value);
        public delegate void MousePositionChanged(double x, double y);

        /// <summary>
        /// MSChart Control States
        /// </summary>
        public enum ChartToolState
        {
            /// <summary>
            /// Undefined
            /// </summary>
            Unknown,
            /// <summary>
            /// Point Select Mode
            /// </summary>
            Select,
            /// <summary>
            /// Zoom
            /// </summary>
            Zoom,
            /// <summary>
            /// Pan
            /// </summary>
            Pan
        }

        /// <summary>
        /// Speed up MSChart data points clear operations.
        /// </summary>
        /// <param name="sender"></param>
        public static void ClearPoints(this Series sender)
        {
            sender.Points.SuspendUpdates();
            while (sender.Points.Count > 0)
                sender.Points.RemoveAt(sender.Points.Count - 1);
            sender.Points.ResumeUpdates();
            sender.Points.Clear(); //Force refresh.
        }

        /// <summary>
        /// Enable Zoom and Pan Controls.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="selectionChanged">Selection changed callabck. Triggered when user select a point with selec tool.</param>
        /// <param name="cursorMoved">Cursor moved callabck. Triggered when user move the mouse in chart area.</param>
        /// <remarks>Callback are optional.</remarks>
        public static void EnableZoomAndPanControls(this Chart sender,
            CursorPositionChanged selectionChanged,
            MousePositionChanged mouseMoved)
        {
            if (ChartContextMenuStrip == null) CreateChartContextMenu();
            if (!ChartTool.ContainsKey(sender))
            {
                ChartTool[sender] = new ChartData(sender);
                ChartData ptrChartData = ChartTool[sender];
                ptrChartData.Backup();
                ptrChartData.SelectionChangedCallback = selectionChanged;
                ptrChartData.MouseMovedCallback = mouseMoved;

                //Populate Context menu
                Chart ptrChart = sender;
                ptrChart.ContextMenuStrip = ChartContextMenuStrip;
                ptrChart.MouseDown += ChartControl_MouseDown;
                ptrChart.MouseMove += ChartControl_MouseMove;
                ptrChart.MouseUp += ChartControl_MouseUp;

                //Override settings.
                ChartArea ptrChartArea = ptrChart.ChartAreas[0];
                ptrChartArea.CursorX.AutoScroll = false;
                ptrChartArea.CursorX.Interval = 1e-06;
                ptrChartArea.CursorY.AutoScroll = false;
                ptrChartArea.CursorY.Interval = 1e-06;

                ptrChartArea.AxisX.ScrollBar.Enabled = false;
                ptrChartArea.AxisX2.ScrollBar.Enabled = false;
                ptrChartArea.AxisY.ScrollBar.Enabled = false;
                ptrChartArea.AxisY2.ScrollBar.Enabled = false;

                SetChartControlState(sender, ChartToolState.Select);

                chart = sender;
            }
        }

        /// <summary>
        /// Disable Zoom and Pan Controls
        /// </summary>
        /// <param name="sender"></param>
        public static void DisableZoomAndPanControls(this Chart sender)
        {
            Chart ptrChart = sender;
            ptrChart.ContextMenuStrip = null;
            if (ChartTool.ContainsKey(ptrChart))
            {
                ptrChart.MouseDown -= ChartControl_MouseDown;
                ptrChart.MouseMove -= ChartControl_MouseMove;
                ptrChart.MouseUp -= ChartControl_MouseUp;

                ChartTool[ptrChart].Restore();
                ChartTool.Remove(ptrChart);
            }
        }
        /// <summary>
        /// Get current control state.
        /// </summary>
        /// <param name="sender"></param>
        /// <returns></returns>
        public static ChartToolState GetChartToolState(this Chart sender)
        {
            if (!ChartTool.ContainsKey(sender))
                return ChartToolState.Unknown;
            else
                return ChartTool[sender].ToolState;

        }

        #region [ Chart Context Menu ]
        private static ContextMenuStrip ChartContextMenuStrip;
        private static ToolStripMenuItem ChartToolSelect;
        private static ToolStripMenuItem ChartToolZoom;
        private static ToolStripMenuItem ChartToolPan;
        private static ToolStripMenuItem ChartToolZoomOut;
        private static ToolStripSeparator ChartToolZoomOutSeparator;
        private static void CreateChartContextMenu()
        {
            ChartContextMenuStrip = new ContextMenuStrip();
            ChartToolZoomOut = new ToolStripMenuItem("Zoom Out");
            ChartToolZoomOutSeparator = new ToolStripSeparator();
            ChartToolSelect = new ToolStripMenuItem("Select");
            ChartToolZoom = new ToolStripMenuItem("Zoom");
            ChartToolPan = new ToolStripMenuItem("Pan");

            ChartContextMenuStrip.Items.Add(ChartToolZoomOut);
            ChartContextMenuStrip.Items.Add(ChartToolZoomOutSeparator);
            ChartContextMenuStrip.Items.Add(ChartToolSelect);
            ChartContextMenuStrip.Items.Add(ChartToolZoom);
            ChartContextMenuStrip.Items.Add(ChartToolPan);
            ChartContextMenuStrip.Items.Add(new ToolStripSeparator());

            ChartContextMenuStrip.Opening += ChartContext_Opening;
            ChartContextMenuStrip.ItemClicked += ChartContext_ItemClicked;
        }

        private static void ChartContext_Opening(object sender, CancelEventArgs e)
        {
            try
            {
                ContextMenuStrip menuStrip = (ContextMenuStrip)sender;
                Chart senderChart = (Chart)menuStrip.SourceControl;

                //Check Zoomed state
                if (senderChart.ChartAreas[0].AxisX.ScaleView.IsZoomed ||
                    senderChart.ChartAreas[0].AxisY.ScaleView.IsZoomed ||
                    senderChart.ChartAreas[0].AxisY2.ScaleView.IsZoomed)
                {
                    ChartToolZoomOut.Visible = true;
                    ChartToolZoomOutSeparator.Visible = true;
                }
                else
                {
                    ChartToolZoomOut.Visible = false;
                    ChartToolZoomOutSeparator.Visible = false;
                }

                //Get Chart Control State
                if (!ChartTool.ContainsKey(senderChart))
                {
                    //Initialize Chart Tool
                    SetChartControlState(senderChart, ChartToolState.Select);
                }

                //Update menu based on current state.
                ChartToolSelect.Checked = false;
                ChartToolZoom.Checked = false;
                ChartToolPan.Checked = false;
                switch (ChartTool[senderChart].ToolState)
                {
                    case ChartToolState.Select:
                        ChartToolSelect.Checked = true;
                        break;
                    case ChartToolState.Zoom:
                        ChartToolZoom.Checked = true;
                        break;
                    case ChartToolState.Pan:
                        ChartToolPan.Checked = true;
                        break;
                }

                // Remove all series menus.
                for (int x = 0; x < menuStrip.Items.Count; x++)
                {
                    if (menuStrip.Items[x].Tag != null)
                    {
                        menuStrip.Items.RemoveAt(x);
                        x--;
                    }
                }

                // Add the series menus again.
                SeriesCollection chartSeries = ((Chart)menuStrip.SourceControl).Series;
                foreach (Series ptrSeries in chartSeries)
                {
                    if (ptrSeries.Name.EndsWith("_Copy"))
                        continue;

                    ToolStripItem ptrItem = ChartContextMenuStrip.Items.Add(ptrSeries.Name);
                    ptrItem.Owner = ChartContextMenuStrip;
                    ptrItem.Tag = ptrSeries.Name;

                    ToolStripMenuItem ptrMenuItem = new ToolStripMenuItem("Enabled");
                    (ptrItem as ToolStripMenuItem).DropDownItems.Add(ptrMenuItem);
                    ptrMenuItem.Checked = ptrSeries.Enabled;
                    ptrMenuItem.Tag = "Enabled";

                    ptrMenuItem = new ToolStripMenuItem("Primary Y-axis");
                    (ptrItem as ToolStripMenuItem).DropDownItems.Add(ptrMenuItem);
                    ptrMenuItem.Checked = (ptrSeries.ChartArea == senderChart.ChartAreas[0].Name && ptrSeries.YAxisType == AxisType.Primary);
                    ptrMenuItem.Tag = "PrimaryY";

                    ptrMenuItem = new ToolStripMenuItem("Secondary Y-axis");
                    (ptrItem as ToolStripMenuItem).DropDownItems.Add(ptrMenuItem);
                    ptrMenuItem.Checked = (ptrSeries.ChartArea == senderChart.ChartAreas[0].Name && ptrSeries.YAxisType == AxisType.Secondary);
                    ptrMenuItem.Tag = "SecondaryY";

                    ptrMenuItem = new ToolStripMenuItem("Separate Y-axis");
                    (ptrItem as ToolStripMenuItem).DropDownItems.Add(ptrMenuItem);
                    ptrMenuItem.Checked = (ptrSeries.ChartArea != senderChart.ChartAreas[0].Name);
                    ptrMenuItem.Tag = "NewY";

                    (ptrItem as ToolStripMenuItem).DropDownItemClicked += ChartContext_DropDownItemClicked;
                }
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
            }
        }

        private static void ChartContext_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            try
            {
                ContextMenuStrip ptrMenuStrip = (ContextMenuStrip)sender;
                if (e.ClickedItem == ChartToolSelect)
                    SetChartControlState((Chart)ChartContextMenuStrip.SourceControl, ChartToolState.Select);
                else if (e.ClickedItem == ChartToolZoom)
                    SetChartControlState((Chart)ChartContextMenuStrip.SourceControl, ChartToolState.Zoom);
                else if (e.ClickedItem == ChartToolPan)
                    SetChartControlState((Chart)ChartContextMenuStrip.SourceControl, ChartToolState.Pan);
                else if (e.ClickedItem == ChartToolZoomOut)
                {
                    Chart ptrChart = (Chart)ptrMenuStrip.SourceControl;
                    ptrChart.ChartAreas[0].AxisX.ScaleView.ZoomReset();
                    ptrChart.ChartAreas[0].AxisY.ScaleView.ZoomReset();
                    ptrChart.ChartAreas[0].AxisY2.ScaleView.ZoomReset();
                }
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
            }
        }

        private static void ChartContext_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            try
            {
                if (e.ClickedItem.Tag == null || e.ClickedItem.Tag.ToString() == string.Empty) return;

                string action = e.ClickedItem.Tag.ToString();
                string seriesName = (sender as ToolStripMenuItem).Tag as string;

                ContextMenuStrip ptrMenuStrip = (sender as ToolStripDropDownItem).Owner as ContextMenuStrip;
                //Chart chart = ptrMenuStrip.SourceControl as Chart;
                SeriesCollection chartSeries = chart.Series;

                //Series enable / disable changed.
                if (action == "Enabled")
                {
                    chartSeries[seriesName].Enabled = !(e.ClickedItem as ToolStripMenuItem).Checked;
                }
                else if (action == "PrimaryY")
                {
                    if (chartSeries[seriesName].ChartArea != chart.ChartAreas[0].Name)
                    {
                        RemoveYAxis(chart, chartSeries, seriesName);
                        chartSeries[seriesName].ChartArea = chart.ChartAreas[0].Name;
                    }

                    chartSeries[seriesName].YAxisType = AxisType.Primary;
                    chart.ChartAreas[0].Position.Auto = true;
                    chart.ChartAreas[0].InnerPlotPosition.Auto = true;

                    chart.ChartAreas[0].AxisY.Maximum = Double.NaN;
                    chart.ChartAreas[0].AxisY.Minimum = Double.NaN;
                }
                else if (action == "SecondaryY")
                {
                    if (chartSeries[seriesName].ChartArea != chart.ChartAreas[0].Name)
                    {
                        RemoveYAxis(chart, chartSeries, seriesName);
                        chartSeries[seriesName].ChartArea = chart.ChartAreas[0].Name;
                    }

                    chartSeries[seriesName].YAxisType = AxisType.Secondary;
                    chart.ChartAreas[0].AxisY2.MajorGrid.Enabled = false;
                    chart.ChartAreas[0].AxisY2.MinorGrid.Enabled = false;

                    chart.ChartAreas[0].Position.Auto = true;
                    chart.ChartAreas[0].InnerPlotPosition.Auto = true;

                    chart.ChartAreas[0].AxisY2.Maximum = Double.NaN;
                    chart.ChartAreas[0].AxisY2.Minimum = Double.NaN;
                }
                else if (action == "NewY")
                {
                    //chart.ChartAreas[0].Position = new ElementPosition(10, 10, 90, 90);
                    //chart.ChartAreas[0].InnerPlotPosition = new ElementPosition(0, 0, 90, 90);

                    //CreateYAxis(chart, chart.ChartAreas[0], chartSeries[seriesName], 2, 8);
                }
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.ToString());
            }
        }

        public static void RemoveYAxis(Chart chart, SeriesCollection chartSeries, string seriesName)
        {
            chart.ChartAreas.Remove(chart.ChartAreas["AxisY_" + chart.ChartAreas[chartSeries[seriesName].ChartArea].Name]);
            chart.ChartAreas.Remove(chart.ChartAreas[chartSeries[seriesName].ChartArea]);
            chartSeries.Remove(chartSeries[seriesName + "_Copy"]);
        }

        public static void CreateYAxis(Chart chart, ChartArea area, Series series, float axisOffset, float labelsSize)
        {
            // Create new chart area for original series
            ChartArea areaSeries = chart.ChartAreas.Add("ChartArea_" + series.Name);
            areaSeries.BackColor = Color.Transparent;
            areaSeries.BorderColor = Color.Transparent;
            areaSeries.Position.FromRectangleF(area.Position.ToRectangleF());
            areaSeries.InnerPlotPosition.FromRectangleF(area.InnerPlotPosition.ToRectangleF());
            areaSeries.AxisX.MajorGrid.Enabled = false;
            areaSeries.AxisX.MajorTickMark.Enabled = false;
            areaSeries.AxisX.LabelStyle.Enabled = false;
            areaSeries.AxisY.MajorGrid.Enabled = false;
            areaSeries.AxisY.MajorTickMark.Enabled = false;
            areaSeries.AxisY.LabelStyle.Enabled = false;
            areaSeries.AxisY.IsStartedFromZero = area.AxisY.IsStartedFromZero;

            areaSeries.AxisX.Minimum = area.AxisX.Minimum;
            areaSeries.AxisX.Maximum = area.AxisX.Maximum;
            areaSeries.AxisX.MajorGrid = area.AxisX.MajorGrid;
            areaSeries.AxisX.MinorGrid = area.AxisX.MinorGrid;

            series.ChartArea = areaSeries.Name;

            // Create new chart area for axis
            ChartArea areaAxis = chart.ChartAreas.Add("AxisY_" + series.ChartArea);
            areaAxis.BackColor = Color.Transparent;
            areaAxis.BorderColor = Color.Transparent;
            areaAxis.Position.FromRectangleF(chart.ChartAreas[series.ChartArea].Position.ToRectangleF());
            areaAxis.InnerPlotPosition.FromRectangleF(chart.ChartAreas[series.ChartArea].InnerPlotPosition.ToRectangleF());

            // Create a copy of specified series
            Series seriesCopy = chart.Series.Add(series.Name + "_Copy");
            seriesCopy.ChartType = series.ChartType;
            foreach (DataPoint point in series.Points)
            {
                seriesCopy.Points.AddXY(point.XValue, point.YValues[0]);
            }

            // Hide copied series
            seriesCopy.IsVisibleInLegend = false;
            seriesCopy.Color = Color.Transparent;
            seriesCopy.BorderColor = Color.Transparent;
            seriesCopy.ChartArea = areaAxis.Name;

            // Disable grid lines & tickmarks
            areaAxis.AxisX.LineWidth = 0;
            areaAxis.AxisX.MajorGrid.Enabled = false;
            areaAxis.AxisX.MajorTickMark.Enabled = false;
            areaAxis.AxisX.LabelStyle.Enabled = false;
            areaAxis.AxisY.MajorGrid.Enabled = false;
            areaAxis.AxisY.IsStartedFromZero = area.AxisY.IsStartedFromZero;

            // Adjust area position
            areaAxis.Position.X -= axisOffset;
            areaAxis.InnerPlotPosition.X += axisOffset;//labelsSize;
        }
        #endregion

        #region [ Chart Control State + Events ]
        private class ChartData
        {
            private Chart Source;
            public ChartData(Chart chartSource) { Source = chartSource; }

            public ChartToolState ToolState { get; set; }
            public CursorPositionChanged SelectionChangedCallback;
            public MousePositionChanged MouseMovedCallback;

            public void Backup()
            {
                ContextMenuStrip = Source.ContextMenuStrip;
                ChartArea ptrChartArea = Source.ChartAreas[0];
                CursorXUserEnabled = ptrChartArea.CursorX.IsUserEnabled;
                CursorYUserEnabled = ptrChartArea.CursorY.IsUserEnabled;
                Cursor = Source.Cursor;
                CursorXInterval = ptrChartArea.CursorX.Interval;
                CursorYInterval = ptrChartArea.CursorY.Interval;
                CursorXAutoScroll = ptrChartArea.CursorX.AutoScroll;
                CursorYAutoScroll = ptrChartArea.CursorY.AutoScroll;
                ScrollBarX = ptrChartArea.AxisX.ScrollBar.Enabled;
                ScrollBarX2 = ptrChartArea.AxisX2.ScrollBar.Enabled;
                ScrollBarY = ptrChartArea.AxisY.ScrollBar.Enabled;
                ScrollBarY2 = ptrChartArea.AxisY2.ScrollBar.Enabled;
            }
            public void Restore()
            {
                Source.ContextMenuStrip = ContextMenuStrip;
                ChartArea ptrChartArea = Source.ChartAreas[0];
                ptrChartArea.CursorX.IsUserEnabled = CursorXUserEnabled;
                ptrChartArea.CursorY.IsUserEnabled = CursorYUserEnabled;
                Source.Cursor = Cursor;
                ptrChartArea.CursorX.Interval = CursorXInterval;
                ptrChartArea.CursorY.Interval = CursorYInterval;
                ptrChartArea.CursorX.AutoScroll = CursorXAutoScroll;
                ptrChartArea.CursorY.AutoScroll = CursorYAutoScroll;
                ptrChartArea.AxisX.ScrollBar.Enabled = ScrollBarX;
                ptrChartArea.AxisX2.ScrollBar.Enabled = ScrollBarX2;
                ptrChartArea.AxisY.ScrollBar.Enabled = ScrollBarY;
                ptrChartArea.AxisY2.ScrollBar.Enabled = ScrollBarY2;
            }

            #region [ Backup Data ]
            public ContextMenuStrip ContextMenuStrip { get; set; }
            private bool CursorXUserEnabled;
            private bool CursorYUserEnabled;
            private System.Windows.Forms.Cursor Cursor;
            private double CursorXInterval, CursorYInterval;
            private bool CursorXAutoScroll, CursorYAutoScroll;
            private bool ScrollBarX, ScrollBarX2, ScrollBarY, ScrollBarY2;
            #endregion
        }
        private static Dictionary<Chart, ChartData> ChartTool = new Dictionary<Chart, ChartData>();
        private static void SetChartControlState(Chart sender, ChartToolState state)
        {
            ChartTool[(Chart)sender].ToolState = state;
            switch (state)
            {
                case ChartToolState.Select:
                    sender.Cursor = Cursors.Cross;
                    sender.ChartAreas[0].CursorX.IsUserEnabled = true;
                    sender.ChartAreas[0].CursorY.IsUserEnabled = true;
                    break;
                case ChartToolState.Zoom:
                    sender.Cursor = Cursors.Cross;
                    sender.ChartAreas[0].CursorX.IsUserEnabled = false;
                    sender.ChartAreas[0].CursorY.IsUserEnabled = false;
                    break;
                case ChartToolState.Pan:
                    sender.Cursor = Cursors.Hand;
                    sender.ChartAreas[0].CursorX.IsUserEnabled = false;
                    sender.ChartAreas[0].CursorY.IsUserEnabled = false;
                    break;
            }
        }
        #endregion

        #region [ Chart - Mouse Events ]
        private static bool MouseDowned;
        private static void ChartControl_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != System.Windows.Forms.MouseButtons.Left) return;

            Chart ptrChart = (Chart)sender;
            ChartArea ptrChartArea = ptrChart.ChartAreas[0];

            MouseDowned = true;

            ptrChartArea.CursorX.SelectionStart = ptrChartArea.AxisX.PixelPositionToValue(e.Location.X);
            ptrChartArea.CursorY.SelectionStart = ptrChartArea.AxisY.PixelPositionToValue(e.Location.Y);
            ptrChartArea.CursorX.SelectionEnd = ptrChartArea.CursorX.SelectionStart;
            ptrChartArea.CursorY.SelectionEnd = ptrChartArea.CursorY.SelectionStart;

            if (ChartTool[(Chart)sender].SelectionChangedCallback != null)
            {
                //ChartTool[(Chart)sender].SelectionChangedCallback(
                //    ptrChartArea.CursorX.SelectionStart,
                //    ptrChartArea.CursorY.SelectionStart);

                ChartTool[(Chart)sender].SelectionChangedCallback(e.Location.X, e.Location.Y,
                                                                  ptrChartArea.CursorX.SelectionStart, ptrChartArea.CursorY.SelectionStart, ptrChartArea.AxisY2.PixelPositionToValue(e.Location.Y));
            }

        }
        private static void ChartControl_MouseMove(object sender, MouseEventArgs e)
        {
            Chart ptrChart = (Chart)sender;
            double selX, selY;
            selX = selY = 0;
            try
            {
                selX = ptrChart.ChartAreas[0].AxisX.PixelPositionToValue(e.Location.X);
                selY = ptrChart.ChartAreas[0].AxisY.PixelPositionToValue(e.Location.Y);

                if (ChartTool[(Chart)sender].MouseMovedCallback != null)
                    ChartTool[(Chart)sender].MouseMovedCallback(selX, selY);
            }
            catch (Exception) { /*ToDo: Set coordinate to 0,0 */ return; } //Handle exception when scrolled out of range.

            switch (ChartTool[ptrChart].ToolState)
            {
                case ChartToolState.Zoom:
                    #region [ Zoom Control ]
                    if (MouseDowned)
                    {
                        ptrChart.ChartAreas[0].CursorX.SelectionEnd = selX;
                        ptrChart.ChartAreas[0].CursorY.SelectionEnd = selY;
                    }
                    #endregion
                    break;

                case ChartToolState.Pan:
                    #region [ Pan Control ]
                    if (MouseDowned)
                    {
                        //Pan Move - Valid only if view is zoomed
                        if (ptrChart.ChartAreas[0].AxisX.ScaleView.IsZoomed ||
                            ptrChart.ChartAreas[0].AxisY.ScaleView.IsZoomed)
                        {
                            double dx = -selX + ptrChart.ChartAreas[0].CursorX.SelectionStart;
                            double dy = -selY + ptrChart.ChartAreas[0].CursorY.SelectionStart;

                            double newX = ptrChart.ChartAreas[0].AxisX.ScaleView.Position + dx;
                            double newY = ptrChart.ChartAreas[0].AxisY.ScaleView.Position + dy;
                            double newY2 = ptrChart.ChartAreas[0].AxisY2.ScaleView.Position + dy;

                            ptrChart.ChartAreas[0].AxisX.ScaleView.Scroll(newX);
                            ptrChart.ChartAreas[0].AxisY.ScaleView.Scroll(newY);
                            ptrChart.ChartAreas[0].AxisY2.ScaleView.Scroll(newY2);
                        }
                    }
                    #endregion
                    break;
            }
        }
        private static void ChartControl_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != System.Windows.Forms.MouseButtons.Left) return;
            MouseDowned = false;

            Chart ptrChart = (Chart)sender;
            ChartArea ptrChartArea = ptrChart.ChartAreas[0];
            switch (ChartTool[ptrChart].ToolState)
            {
                case ChartToolState.Zoom:
                    //Zoom area.
                    double XStart = ptrChartArea.CursorX.SelectionStart;
                    double XEnd = ptrChartArea.CursorX.SelectionEnd;
                    double YStart = ptrChartArea.CursorY.SelectionStart;
                    double YEnd = ptrChartArea.CursorY.SelectionEnd;

                    //Zoom area for Y2 Axis
                    double YMin = ptrChartArea.AxisY.ValueToPosition(Math.Min(YStart, YEnd));
                    double YMax = ptrChartArea.AxisY.ValueToPosition(Math.Max(YStart, YEnd));

                    if ((XStart == XEnd) && (YStart == YEnd)) return;
                    //Zoom operation
                    ptrChartArea.AxisX.ScaleView.Zoom(
                        Math.Min(XStart, XEnd), Math.Max(XStart, XEnd));
                    ptrChartArea.AxisY.ScaleView.Zoom(
                        Math.Min(YStart, YEnd), Math.Max(YStart, YEnd));
                    ptrChartArea.AxisY2.ScaleView.Zoom(
                        ptrChartArea.AxisY2.PositionToValue(YMin),
                        ptrChartArea.AxisY2.PositionToValue(YMax));

                    //Clear selection
                    ptrChartArea.CursorX.SelectionStart = ptrChartArea.CursorX.SelectionEnd;
                    ptrChartArea.CursorY.SelectionStart = ptrChartArea.CursorY.SelectionEnd;
                    break;

                case ChartToolState.Pan:
                    break;
            }
        }
        #endregion
    }
}
