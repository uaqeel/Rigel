﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Threading;

namespace Rigel
{
    public partial class Form1 : Form
    {
        private FTXClient ss;
        List<string> selectedContracts;
        System.Threading.Timer tt, tt2;
        bool updateHistoricalData;

        public Form1()
        {
            InitializeComponent();

            chart1.ChartAreas[0].InnerPlotPosition.Auto = true;
            chart1.ChartAreas[0].AxisY.IsStartedFromZero = false;
            chart1.Legends[0].Docking = Docking.Top;
            chart1.ChartAreas[0].AxisX.LabelStyle.Format = "dd-MMM HH:mm:ss";
            chart1.ChartAreas[0].AxisX.LabelStyle.Angle = 45;


            chart2.ChartAreas[0].InnerPlotPosition.Auto = true;
            chart2.ChartAreas[0].AxisY.IsStartedFromZero = false;
            chart2.Legends[0].Docking = Docking.Top;
            chart2.ChartAreas[0].AxisX.LabelStyle.Format = "dd-MMM HH:mm:ss";
            chart2.ChartAreas[0].AxisX.LabelStyle.Angle = 45;
            chart2.Titles.Add("Implied Yields (% pa)");

            chart3.ChartAreas[0].InnerPlotPosition.Auto = true;
            chart3.ChartAreas[0].AxisY.IsStartedFromZero = false;
            chart3.Legends[0].Enabled = false;
            chart3.ChartAreas[0].AxisX.LabelStyle.Angle = 45;
            chart3.Titles.Add("Implied Yield Curve (% pa)");

            chart4.ChartAreas[0].InnerPlotPosition.Auto = true;
            chart4.ChartAreas[0].AxisY.IsStartedFromZero = false;
            chart4.Legends[0].Enabled = false;
            chart4.ChartAreas[0].AxisX.LabelStyle.Angle = 45;
            chart4.ChartAreas[0].AxisX.Interval = 1;
            chart4.Titles.Add("Highest Yielding Tokens (% pa)");

            selectedContracts = new List<string>();
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            ss = new FTXClient(textBox1.Text, textBox2.Text);
            await ss.Initialize();

            comboBox1.DataSource = ss.tokens;
        }

        private void comboBox1_TextChanged(object sender, EventArgs e)
        {
            try
            {
                listBox1.DataSource = ss.futures.Where(x => x.Value.underlying == comboBox1.Text).Select(x => x.Value.name).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        private void listBox1_SelectedValueChanged(object sender, EventArgs e)
        {
            if (tt != null)
                tt.Dispose();

            selectedContracts = ((ListBox)sender).SelectedItems.Cast<string>().ToList();
            selectedContracts = selectedContracts.Prepend(comboBox1.Text + "/USD").ToList();

            chart1.Series.Clear();
            chart2.Series.Clear();
            chart3.Series.Clear();
            chart4.Series.Clear();

            updateHistoricalData = true;

            tt = new System.Threading.Timer(new System.Threading.TimerCallback(UpdatePriceCharts), null, 0, 5 * 60 * 1000);
            tt2 = new System.Threading.Timer(new System.Threading.TimerCallback(UpdateFundingRates), null, 0, 5 * 60 * 1000);
        }

        private async void UpdatePriceCharts (object state)
        {
            Tuple<DateTime, Market[]> markets = await ss.GetMultipleMarketsAsync(selectedContracts);

            List<Tuple<DateTime, double>[]> historicalPrices = null;
            List<Tuple<DateTime, double>[]> historicalFundingRates = null;
            if (updateHistoricalData)
            {
                historicalPrices = await ss.GetMultipleHistoricalMarketsAsync(selectedContracts, 5 * 60, DateTime.Now.AddDays(-5), DateTime.Now);
                if (selectedContracts.Count == historicalPrices.Count)
                    historicalFundingRates = ss.GetHistoricalFundingRates(selectedContracts, historicalPrices);
            }

            // Just in case changes have been made while retrieving markets.
            if (markets.Item2.Length != selectedContracts.Count)
                return;

            label4.Invoke(new MethodInvoker(delegate
            {
                label4.Text = "";
            }));

            chart3.Invoke(new MethodInvoker(delegate
            {
                foreach (var s in chart3.Series)
                    s.Points.Clear();
            }));

            int i = 0;
            double prevYF = 0, prevRate = 0;
            foreach (var s in selectedContracts)
            {
                if (updateHistoricalData && historicalPrices != null & historicalFundingRates != null)
                {
                    AddManyDataPoints(chart1, s, historicalPrices[i], false);
                    AddManyDataPoints(chart2, s, historicalFundingRates[i], false);
                }

                AddDataPoint(chart1, markets.Item1, s, markets.Item2[i].last, false);
                if (i > 0)
                {
                    double fundingRate = 0;
                    if (ss.futures[s].isPerpetual)
                        fundingRate = ss.fundingRates[s] * 24 * 365.25 * 100;
                    else
                        fundingRate = ss.futures[s].ImpliedFundingRate(markets.Item2[0].last, markets.Item1) * 100;

                    AddDataPoint(chart2, markets.Item1, s, fundingRate, false);

                    double yf = ss.futures[s].YearFraction(markets.Item1);
                    double forwardRate = (fundingRate * yf - prevRate * prevYF) / (yf - prevYF);

                    AddDataPoint(chart3, s + "(" + Math.Round(yf, 2) + "y)", "Forward Rate", forwardRate, true);
                    AddDataPoint(chart3, s  + "(" + Math.Round(yf, 2) + "y)", "Implied Rate", fundingRate, false);

                    prevYF = yf;
                    prevRate = fundingRate;
                }

                label4.Invoke(new MethodInvoker(delegate
                {
                    label4.Text += s + ":".PadRight(10-Math.Min(10, s.Length)) + markets.Item2[i].bid + "  /  " + markets.Item2[i].ask + Environment.NewLine;
                }));

                i++;
            }

            if (historicalFundingRates != null)
                updateHistoricalData = false;
        }

        private async void UpdateFundingRates(object state)
        {
            chart4.Invoke(new MethodInvoker(delegate
            {
                foreach (var s in chart4.Series)
                    s.Points.Clear();
            }));

            await ss.UpdateFundingRates();

            var sorted = (from s in ss.fundingRates
                          orderby s.Value descending
                          select s).Take(20);

            int i = 0;
            foreach (var s in sorted)
            {
                AddDataPoint(chart4, s.Key, "Token", s.Value * 24 * 365.25 * 100, i < 3 ? true : false);
                i++;
            }
        }

        private static void AddDataPoint<T>(Chart chart, T xValue, string seriesName, double yValue, bool isPoint)
        {
            chart.Invoke(new MethodInvoker(delegate
            {
                if (chart.Series.FindByName(seriesName) == null)
                {
                    Series ss = chart.Series.Add(seriesName);
                    ss.ChartArea = chart.ChartAreas[0].Name;

                    if (typeof(T) == typeof(DateTime))
                        ss.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Time;
                    ss.MarkerStyle = MarkerStyle.Circle;
                    ss.ToolTip = seriesName + ": (#VALY2, #VALX)";

                    if (isPoint)
                    {
                        ss.ChartType = SeriesChartType.Column;
                    }
                    else
                    {
                        ss.ChartType = SeriesChartType.Line;
                    }
                }

                DataPoint pp = new DataPoint();
                if (isPoint)
                {
                    pp.Label = "#VALY";
                }

                pp.SetValueXY(xValue, Math.Round(yValue,2));

                chart.Series[seriesName].Points.Add(pp);
            }));
        }

        private static void AddManyDataPoints(Chart chart, string seriesName, IEnumerable<Tuple<DateTime,double>> data, bool isPoint)
        {
            chart.Invoke(new MethodInvoker(delegate
            {
                if (chart.Series.FindByName(seriesName) == null)
                {
                    Series ss = chart.Series.Add(seriesName);
                    ss.ChartArea = chart.ChartAreas[0].Name;

                    ss.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Time;
                    ss.MarkerStyle = MarkerStyle.Circle;
                    ss.ToolTip = seriesName + ": (#VALY2, #VALX)";

                    if (isPoint)
                    {
                        ss.ChartType = SeriesChartType.Column;
                    }
                    else
                    {
                        ss.ChartType = SeriesChartType.Line;
                    }
                }

                foreach (var d in data)
                {
                    DataPoint pp = new DataPoint();
                    if (isPoint)
                    {
                        pp.Label = "#VALY";
                    }

                    pp.SetValueXY(d.Item1, Math.Round(d.Item2, 2));
                    chart.Series[seriesName].Points.Add(pp);
                }
            }));
        }

    }
}