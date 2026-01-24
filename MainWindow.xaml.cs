using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

using NOAA_Model1;
using NOAA_Model2;

namespace NOAA;

/// <summary>
/// Our main <see cref="System.Windows.Window"/> for <see cref="System.Windows.Controls.ScrollViewer"/> 
/// weather report and the <see cref="NOAA.Controls.CartesianChart"/> precipitation display.
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    #region [Properties]
    bool _expandHourlyPrecip = false;
    double _latitude = 0;
    double _longitude = 0;
    double _windowLeft = 0;
    double _windowTop = 0;
    double _windowWidth = 0;
    double _windowHeight = 0;
    double _windBrushOpacity = 0;
    string _windBrushColor = "";
    ApiUrls? _apiUrls;
    RadialGradientBrush? _windRadialBrush;
    CancellationTokenSource _cts = new CancellationTokenSource();
    readonly WeatherService _weatherService = new WeatherService();
    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (string.IsNullOrEmpty(propertyName)) { return; }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    bool isAnimated = false;
    public bool IsAnimated
    {
        get => isAnimated;
        set
        {
            if (isAnimated != value)
            {
                isAnimated = value;
                OnPropertyChanged();
            }
        }
    }

    string status = "";
    public string Status
    {
        get => status;
        set
        {
            if (status != value)
            {
                status = value;
                OnPropertyChanged();
            }
        }
    }

    string status2 = "";
    public string Status2
    {
        get => status2;
        set
        {
            if (status2 != value)
            {
                status2 = value;
                OnPropertyChanged();
            }
        }
    }

    List<ChartSeries> precipSeries = new List<ChartSeries>();
    public List<ChartSeries> PrecipSeries
    {
        get => precipSeries;
        set
        {
            precipSeries = value;
            OnPropertyChanged();
        }
    }

    #endregion

    public MainWindow()
    {
        // additional weather emojis ⇒ ☀ ⛅ ☁ ⛈ ☂ ☔ ☃ ⛄ ⛇ ⛆ ⛱ ☄ ♨
        InitializeComponent();
        this.DataContext = this; // ⇦ must set context for INotifyPropertyChanged
        Debug.WriteLine($"[INFO] Application version {App.GetCurrentAssemblyVersion()}");
        var isodt = Extensions.ConvertToLocalTime("2026-01-11T10:06:35+00:00"); // 01/11/2026 05:06:35 AM
    }

    #region [Events]
    /// <summary>
    /// <see cref="System.Windows.Window"/> event
    /// </summary>
    async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            this.Title = $"NOAA Weather Forecast - v{App.GetCurrentAssemblyVersion()}";
            chart.Visibility = spProgress.Visibility = Visibility.Hidden;
            btnGet.Content = Constants.MainButtonText;

            #region [Load config]
            _expandHourlyPrecip = ConfigManager.Get("ExpandHourlyPrecipitation", defaultValue: false);
            _latitude = ConfigManager.Get("Latitude", defaultValue: 40.539d);
            _longitude = ConfigManager.Get("Longitude", defaultValue: -75.496d);
            _windowTop = ConfigManager.Get("WindowTop", defaultValue: 200d);
            _windowLeft = ConfigManager.Get("WindowLeft", defaultValue: 250d);
            _windowWidth = ConfigManager.Get("WindowWidth", defaultValue: 1000d);
            _windowHeight = ConfigManager.Get("WindowHeight", defaultValue: 800d);
            _windBrushColor = ConfigManager.Get("WindBrushColor", defaultValue: "#1E90FF");
            _windBrushOpacity = ConfigManager.Get("WindBrushOpacity", defaultValue: 0.5d);
            _windRadialBrush = Extensions.CreateRadialBrush(_windBrushColor, _windBrushOpacity);
            if (_windRadialBrush != null)
                spBackground.DotBrush = _windRadialBrush;
            #endregion

            // Check if position is on any screen
            this.RestorePosition(_windowLeft, _windowTop, _windowWidth, _windowHeight);

            Status = $"🔔 Testing internet access";
            if (!await Extensions.PingHostAsync("8.8.8.8"))
            {
                if (!await Extensions.PingHostAsync("1.1.1.1"))
                    App.ShowDialog($"🚨 You might not have internet access. Any subsequent API calls will probably fail.", "Warning", assetName: "assets/warning.png");
            }
            else
            {
                Status = $"Latitude is {_latitude}, longitude is {_longitude}. This can be adjusted in \"Settings.xml\"";
            }

            #region [Set the wind speed background effect]
            var url = await _weatherService.GetForecastUrlAsync(_latitude, _longitude);
            if (url != null)
            {
                var wind = await LoadWindSpeed(url.WeeklyForecast);
                await Dispatcher.InvokeAsync(() =>
                {
                    msgBar.BarText = $"🔔 Setting background wind speed to {wind}";
                    spBackground.WindBaseSpeed += wind;
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
            #endregion
            
            //TestChartPoints();

            // Start the background loop
            //_ = Extensions.RunEveryMidnightAsync(GetWeatherClick(null, new RoutedEventArgs()), _cts.Token);
            _ = Extensions.RunEveryMidnight(() => { GetWeatherClick(null, new RoutedEventArgs()); }, _cts.Token);
        }
        catch (Exception ex)
        {
            Extensions.WriteToLog($"Window_Loaded: {ex.Message}", level: LogLevel.ERROR);
        }
    }

    /// <summary>
    /// <see cref="System.Windows.Window"/> event
    /// </summary>
    void Window_Closing(object sender, CancelEventArgs e)
    {
        ConfigManager.Set("Latitude", _latitude);
        ConfigManager.Set("Longitude", _longitude);
        ConfigManager.Set("WindowTop", value: this.Top.IsInvalid() ? 200d : this.Top);
        ConfigManager.Set("WindowLeft", value: this.Left.IsInvalid() ? 250d : this.Left);
        if (!this.Width.IsInvalid() && this.Width >= 800) { ConfigManager.Set("WindowWidth", value: this.Width); }
        else { ConfigManager.Set("WindowWidth", value: 1000); } // restore default
        if (!this.Height.IsInvalid() && this.Height >= 600) { ConfigManager.Set("WindowHeight", value: this.Height); }
        else { ConfigManager.Set("WindowHeight", value: 800); } // restore default
        ConfigManager.Set("WindBrushColor", _windBrushColor);
        ConfigManager.Set("WindBrushOpacity", _windBrushOpacity);
        ConfigManager.Set("ExpandHourlyPrecipitation", _expandHourlyPrecip);
        _weatherService?.Dispose();
        _cts?.Cancel(); // Signal any loops/timers that it's time to shut it down.
    }

    /// <summary>
    /// <see cref="System.Windows.Controls.Button"/> event
    /// </summary>
    async void GetWeatherClick(object sender, RoutedEventArgs e)
    {
        btnGet.Content = "•••";
        btnGet.IsEnabled = false;
        spProgress.Visibility = Visibility.Visible;
        Status = "🔔 Fetching forecast data…";

        // We use the lat/long to get the ZONE/GRID URL from https://api.weather.gov
        _apiUrls = await _weatherService.GetForecastUrlAsync(_latitude, _longitude);

        if (_apiUrls is null)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                msgBar.BarText = Status = $"🚨 Failed to get forecast URL, try again later.";
                await Task.Delay(500); // prevent spamming
                spProgress.Visibility = Visibility.Hidden;
                btnGet.IsEnabled = true;
                btnGet.Content = Constants.MainButtonText;
            }, System.Windows.Threading.DispatcherPriority.Background);
            return;
        }

        // Then we'll use that to fetch the detailed forecast for the week.
        await LoadForecast(_apiUrls);
        
        // Re-enable UI elements and update status once complete (back on the UI thread)
        await Dispatcher.InvokeAsync(async () =>
        {
            msgBar.BarText = $"🔔 Attempt completed ({DateTime.Now.ToLongTimeString()})";
            await Task.Delay(500); // prevent spamming
            spProgress.Visibility = Visibility.Hidden;
            btnGet.IsEnabled = true;
            btnGet.Content = Constants.MainButtonText;
            if (chart.Visibility == Visibility.Visible)
            {
                chart.Visibility = Visibility.Hidden;
                viewer.Visibility = Visibility.Visible;
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    async void GetSnowfallClick(object sender, RoutedEventArgs e)
    {
        btnSnowfall.Content = "•••";
        btnSnowfall.IsEnabled = false;
        spProgress.Visibility = Visibility.Visible;
        Status = "🔔 Fetching precipitation data…";

        // We use the lat/long to get the ZONE/GRID URL from https://api.weather.gov
        _apiUrls = await _weatherService.GetForecastUrlAsync(_latitude, _longitude);

        if (_apiUrls is null)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                msgBar.BarText = Status = $"🚨 Failed to get forecast URL, try again later.";
                await Task.Delay(500); // prevent spamming
                spProgress.Visibility = Visibility.Hidden;
                btnSnowfall.IsEnabled = true;
                btnSnowfall.Content = Constants.SecondaryButtonText;
            }, System.Windows.Threading.DispatcherPriority.Background);
            return;
        }

        // Then we'll use that to fetch the detailed precipitation for the chart.
        await LoadSnowfallAndPrecipitation(_apiUrls);

        // Re-enable UI elements and update status once complete (back on the UI thread)
        await Dispatcher.InvokeAsync(async () =>
        {
            msgBar.BarText = $"🔔 Attempt completed ({DateTime.Now.ToLongTimeString()})";
            await Task.Delay(500); // prevent spamming
            spProgress.Visibility = Visibility.Hidden;
            btnSnowfall.IsEnabled = true;
            btnSnowfall.Content = Constants.SecondaryButtonText;
            if (chart.Visibility == Visibility.Hidden)
            {
                ShowChart();
            }
            else
            {
                chart.Redraw();
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
    }


    /// <summary>
    /// <see cref="NOAA.Controls.CartesianChart"/> event
    /// </summary>
    void ShowChart()
    {
        if (PrecipSeries == null || PrecipSeries.Count == 0)
        {
            Status2 = $"⛅ No chart data available, try getting the weather first.";
            return;
        }
        chart.Visibility = chart.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
        //btnChart.Content = chart.Visibility == Visibility.Visible ? Constants.ChartButtonTextHide : Constants.ChartButtonTextShow;
        if (chart.Visibility == Visibility.Visible) 
        {
            viewer.Visibility = Visibility.Hidden;
            chart.Redraw(); 
        }
        else
        {
            viewer.Visibility = Visibility.Visible;
        }
    }

    /// <summary>
    /// <see cref="System.Windows.Window"/> event
    /// </summary>
    void Window_Activated(object sender, EventArgs e) => IsAnimated = true;

    /// <summary>
    /// <see cref="System.Windows.Window"/> event
    /// </summary>
    void Window_Deactivated(object sender, EventArgs e) => IsAnimated = false;

    /// <summary>
    /// <see cref="System.Windows.Window"/> event
    /// </summary>
    void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.IsInvalidOrZero())
            return;
        //Debug.WriteLine($"[INFO] New size: {e.NewSize.Width:N0},{e.NewSize.Height:N0}");
        // Add in some margin
        spBackground.Width = e.NewSize.Width - 10;
        spBackground.Height = e.NewSize.Height - 10;
    }
    #endregion

    #region [Service Methods]
    async Task LoadForecast(ApiUrls url)
    {
        bool getProbabilityOfPrecipitation = false;
        List<PrecipitationValue> precipAmounts = new List<PrecipitationValue>();

        try
        {
            WeatherForecastResponse? forecast = null;

            // Weekly forecast details
            if (url is null)
                forecast = await _weatherService.GetWeeklyPHIForecastAsync("34,100"); // PHI grid default
            else
                forecast = await _weatherService.GetWeeklyForecastAsync(url.WeeklyForecast, logResponse: false);

            if (forecast == null || forecast.Properties == null)
            {
                Status = $"No data to work with";
                App.ShowDialog($"🚨 Could not get data from the API.", "Warning", assetName: "assets/warning.png");
                return;
            }

            // Precipitation forecast details
            if (url is null)
                precipAmounts = await _weatherService.GetWeeklyQuantitativePrecipitationAsync("PHI", "34,100");
            else
                precipAmounts = await _weatherService.GetWeeklyQuantitativePrecipitationAsync(url.GridData);

            Debug.WriteLine($"━━◖QuantitativePrecipitation◗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            foreach (var item in precipAmounts)
            {
                Debug.WriteLine($"[INFO] {item.Time}  {item.Value} ");
            }
            /* [EXAMPLE]
               2026-01-14T00:00:00+00:00/PT6H  0.0 inches 
               2026-01-14T06:00:00+00:00/PT6H  0.0 inches 
               2026-01-14T12:00:00+00:00/PT6H  0.0 inches 
               2026-01-14T18:00:00+00:00/PT6H  0.0 inches 
               2026-01-15T00:00:00+00:00/PT6H  0.0 inches 
               2026-01-15T06:00:00+00:00/PT6H  0.1 inches 
            */

            #region [Extras]
            if (getProbabilityOfPrecipitation)
            {
                var probabilities = await _weatherService.GetWeeklyProbabilityOfPrecipitationAsync("PHI", "34,100");
                Debug.WriteLine($"━━◖ProbabilityOfPrecipitation◗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                foreach (var item in probabilities)
                {
                    Debug.WriteLine($"[INFO] {item.Time}  {item.Value} ");
                }
                /* [EXAMPLE]
                   2026-01-12T03:00:00+00:00/PT14H  0 % 
                */
            }
            #endregion

            bool tryPrecipitationValues = false;
            List<ChartPoint> points = new List<ChartPoint>();
            if (tryPrecipitationValues)
            {
                // Confirm our collections are in sync
                if (forecast.Properties.Periods.Count == precipAmounts.Count)
                {
                    for (int i = 0; i < forecast.Properties.Periods.Count; i++)
                    {
                        forecast.Properties.Periods[i].PrecipitationAmount = precipAmounts[i].Value;
                        points.Add(new ChartPoint(forecast.Properties.Periods[i].StartTime, double.Parse(precipAmounts[i].Value), precipAmounts[i].UnitOfMeasure));
                    }
                    PrecipSeries = new List<ChartSeries> { new ChartSeries { Points = points } };
                }
                else // We're not in sync, so we need to merge by closest time
                {
                    try
                    {
                        var merged = MergeClosest(forecast.Properties.Periods, precipAmounts);
                        for (int i = 0; i < forecast.Properties.Periods.Count; i++)
                        {
                            forecast.Properties.Periods[i].PrecipitationAmount = merged[i].DetailPrecip;
                            points.Add(new ChartPoint(forecast.Properties.Periods[i].StartTime, double.Parse(merged[i].DetailPrecip), merged[i].UnitOfMeasure));
                        }
                        PrecipSeries = new List<ChartSeries> { new ChartSeries { Points = points } };
                    }
                    catch (Exception ex)
                    {
                        App.ShowDialog($"🚨 Error merging precipitation data:{Environment.NewLine}{ex.Message}", "Warning", assetName: "assets/warning.png");
                    }
                }
            }
            else // Our fallback is to simply extract from detailed forecast text
            {
                string uom = "";
                if (precipAmounts.Count != 0)
                {
                    uom = precipAmounts[0].UnitOfMeasure;
                    for (int i = 0; i < forecast.Properties.Periods.Count; i++)
                    {
                        var parsed = _weatherService.ExtractAmount(forecast.Properties.Periods[i].DetailedForecast, uom);
                        forecast.Properties.Periods[i].PrecipitationAmount = string.IsNullOrEmpty(parsed) ? $"0 {uom}" : parsed;
                        points.Add(new ChartPoint(forecast.Properties.Periods[i].StartTime, _weatherService.ExtractAmountToDouble(forecast.Properties.Periods[i].DetailedForecast), uom));
                    }
                    PrecipSeries = new List<ChartSeries> { new ChartSeries { Points = points } };
                }
                else
                {
                    // No unit of measure available
                    for (int i = 0; i < forecast.Properties.Periods.Count; i++)
                    {
                        var parsed = _weatherService.ExtractAmount(forecast.Properties.Periods[i].DetailedForecast);
                        forecast.Properties.Periods[i].PrecipitationAmount = string.IsNullOrEmpty(parsed) ? $"0" : parsed;
                        points.Add(new ChartPoint(forecast.Properties.Periods[i].StartTime, _weatherService.ExtractAmountToDouble(forecast.Properties.Periods[i].DetailedForecast), uom));
                    }
                    PrecipSeries = new List<ChartSeries> { new ChartSeries { Points = points } };
                }
            }

            // Set the data source for the ItemsControl
            ForecastList.ItemsSource = forecast.Properties.Periods;

            Status = $"Forecast updated {forecast.Properties.UpdateTime} ";
            var ucode = _weatherService.GetUnitCode(forecast.Properties.Elevation.UnitCode);
            if (ucode.Equals("m", StringComparison.OrdinalIgnoreCase))
            {
                var elevation = UnitConverter.MetersToFeet(forecast.Properties.Elevation.Value);
                Status2 = $"Probability of precipitation: {forecast.Properties.Periods[0].ProbabilityOfPrecipitation.Value}% (elevation {elevation:N0} feet)";
            }
            else
            {
                Status2 = $"Probability of precipitation: {forecast.Properties.Periods[0].ProbabilityOfPrecipitation.Value}% (elevation {forecast.Properties.Elevation.Value:N0} {ucode})";
            }
        }
        catch (Exception ex)
        {
            App.ShowDialog($"Error loading forecast:{Environment.NewLine}{ex.Message}", "Warning", assetName: "assets/error.png");
        }
    }

    async Task LoadSnowfallAndPrecipitation(ApiUrls url)
    {
        List<PrecipitationValue> precipAmounts = new List<PrecipitationValue>();
        List<PrecipitationValue> snowAmounts = new List<PrecipitationValue>();
        try
        {
            WeatherForecastResponse? forecast = null;

            // Weekly forecast details
            if (url is null)
                forecast = await _weatherService.GetWeeklyPHIForecastAsync("34,100"); // PHI grid default
            else
                forecast = await _weatherService.GetWeeklyForecastAsync(url.WeeklyForecast, logResponse: false);

            if (forecast == null || forecast.Properties == null)
            {
                Status = $"No data to work with";
                App.ShowDialog($"🚨 Could not get data from the API.", "Warning", assetName: "assets/warning.png");
                return;
            }

            // Precipitation forecast details
            if (url is null)
                precipAmounts = await _weatherService.GetWeeklyQuantitativePrecipitationAsync("PHI", "34,100");
            else
                precipAmounts = await _weatherService.GetWeeklyQuantitativePrecipitationAsync(url.GridData);

            Debug.WriteLine($"━━◖QuantitativePrecipitation◗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            foreach (var item in precipAmounts)
            {
                Debug.WriteLine($"[RAIN] {item.Time}  {item.Value} ");
            }
            /* [EXAMPLE]
               2026-01-14T00:00:00+00:00/PT6H  0.0 inches 
               2026-01-14T06:00:00+00:00/PT6H  0.0 inches 
               2026-01-14T12:00:00+00:00/PT6H  0.0 inches 
               2026-01-14T18:00:00+00:00/PT6H  0.0 inches 
               2026-01-15T00:00:00+00:00/PT6H  0.0 inches 
               2026-01-15T06:00:00+00:00/PT6H  0.1 inches 
            */

            if (url is null)
                snowAmounts = await _weatherService.GetWeeklySnowfallAmountAsync("PHI", "34,100");
            else
                snowAmounts = await _weatherService.GetWeeklySnowfallAmountAsync(url.GridData);

            Debug.WriteLine($"━━◖SnowfallAmounts◗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            foreach (var item in snowAmounts)
            {
                Debug.WriteLine($"[SNOW] {item.Time}  {item.Value} ");
            }
            /*
            2026-01-25T06:00:00+00:00/PT6H  1.30 inches 
            2026-01-25T12:00:00+00:00/PT6H  5.40 inches 
            2026-01-25T18:00:00+00:00/PT6H  4.30 inches 
            2026-01-26T00:00:00+00:00/PT6H  1.30 inches 
            2026-01-26T06:00:00+00:00/PT6H  0.60 inches 
            2026-01-26T12:00:00+00:00/PT6H  0.20 inches 
            2026-01-26T18:00:00+00:00/PT6H  0.20 inches 
            */
            List<ChartPoint> points = new List<ChartPoint>();
            if (snowAmounts.Count == precipAmounts.Count)
            {
                for (int i = 0; i < snowAmounts.Count; i++)
                {
                    var snowAmount = Extensions.GetMaximumDouble(snowAmounts[i].Value);
                    var precipAmount = Extensions.GetMaximumDouble(precipAmounts[i].Value);
                    // Take the larger of the two and hydrate the chart points
                    if (snowAmount > precipAmount)
                    {
                        if (_expandHourlyPrecip)
                        {
                            //var expanded = _weatherService.ExpandNoaaPrecipHourly(snowAmounts[i].Time, snowAmount);
                            //var expanded = _weatherService.ExpandNoaaPrecipHourlyCatmullRom(snowAmounts[i].Time, snowAmount);
                            var expanded = _weatherService.ExpandNoaaPrecipHourlyFlat(snowAmounts[i].Time, snowAmount);
                            foreach (var item in expanded)
                            {
                                points.Add(new ChartPoint(item.Time, item.Value, snowAmounts[i].UnitOfMeasure));
                            }
                        }
                        else
                        {
                            var startTime = _weatherService.ParseNoaaStartTime(snowAmounts[i].Time);
                            var midPoint = _weatherService.ParseNoaaValidTimeMidpoint(snowAmounts[i].Time);
                            var rangeTime = _weatherService.ParseNoaaValidTime(snowAmounts[i].Time);
                            points.Add(new ChartPoint(midPoint, snowAmount, snowAmounts[i].UnitOfMeasure));
                        }
                    }
                    else
                    {
                        if (_expandHourlyPrecip)
                        {
                            //var expanded = _weatherService.ExpandNoaaPrecipHourly(precipAmounts[i].Time, precipAmount);
                            //var expanded = _weatherService.ExpandNoaaPrecipHourlyCatmullRom(precipAmounts[i].Time, precipAmount);
                            var expanded = _weatherService.ExpandNoaaPrecipHourlyFlat(precipAmounts[i].Time, precipAmount);
                            foreach (var item in expanded)
                            {
                                points.Add(new ChartPoint(item.Time, item.Value, precipAmounts[i].UnitOfMeasure));
                            }
                        }
                        else
                        {
                            var startTime = _weatherService.ParseNoaaStartTime(precipAmounts[i].Time);
                            var midPoint = _weatherService.ParseNoaaValidTimeMidpoint(precipAmounts[i].Time);
                            var rangeTime = _weatherService.ParseNoaaValidTime(precipAmounts[i].Time);
                            points.Add(new ChartPoint(midPoint, precipAmount, precipAmounts[i].UnitOfMeasure));
                        }
                    }
                }
                PrecipSeries = new List<ChartSeries> { new ChartSeries { Points = points } };
            }

            Status = $"Snowfall data gathered {forecast.Properties.UpdateTime} ";
        }
        catch (Exception ex)
        {
            App.ShowDialog($"Error loading snowfall:{Environment.NewLine}{ex.Message}", "Warning", assetName: "assets/error.png");
        }
    }

    async Task<double> LoadWindSpeed(string url, double factor = 4d)
    {
        double result = 0d;
        try
        {
            WeatherForecastResponse? forecast = null;

            if (string.IsNullOrEmpty(url))
                forecast = await _weatherService.GetWeeklyPHIForecastAsync("34,100"); // PHI grid default
            else
                forecast = await _weatherService.GetWeeklyForecastAsync(url, logResponse: false);

            if (forecast == null || forecast.Properties == null)
                return result;

            var wind = forecast.Properties.Periods[0].WindSpeed;
            if (string.IsNullOrEmpty(wind))
                return result;

            var max = Extensions.GetMaximumNumber(wind); // "10 to 15 mph"
            if (max > 0)
                result = (double)max;

        }
        catch (Exception ex)
        {
            Extensions.WriteToLog($"Error loading WindSpeed: {ex.Message}", level: LogLevel.ERROR);
        }
        return result * factor;
    }
    #endregion

    #region [Helpers]
    public List<MergedWeather> MergeClosest(List<ForecastPeriod> basic, List<PrecipitationValue> precip)
    {
        var result = new List<MergedWeather>();
        var sortedPrecip = precip.OrderBy(p => _weatherService.ParseNoaaStartTime(p.Time)).ToList();
        foreach (var b in basic)
        {
            // Find the precipitation entry with the smallest time difference
            var closest = sortedPrecip
                .OrderBy(p => Math.Abs((_weatherService.ParseNoaaStartTime(p.Time) - b.StartTime).TotalMinutes))
                .FirstOrDefault();

            result.Add(new MergedWeather
            {
                Time = b.StartTime,
                ForcastPrecip = b.PrecipitationAmount,
                DetailPrecip = string.IsNullOrEmpty(closest?.Value) ? "0" : closest.Value,
                UnitOfMeasure = closest?.UnitOfMeasure ?? "inches"
            });
        }
        return result;
    }

    public void TestChartPoints()
    {
        List<ChartPoint> points = new List<ChartPoint>()
        {
            new ChartPoint(DateTime.Now.AddHours(-6), 0.0, "inches"),
            new ChartPoint(DateTime.Now.AddHours(-3), 0.01, "inches"),
            new ChartPoint(DateTime.Now, 0.1, "inches"),
            new ChartPoint(DateTime.Now.AddHours(3), 0.26, "inches"),
            new ChartPoint(DateTime.Now.AddHours(6), 0.2, "inches"),
            new ChartPoint(DateTime.Now.AddHours(9), 0.1, "inches"),
            new ChartPoint(DateTime.Now.AddHours(12), 0.00, "inches"),
            new ChartPoint(DateTime.Now.AddHours(15), 0.00, "inches"),
            new ChartPoint(DateTime.Now.AddHours(18),0.25, "inches"),
            new ChartPoint(DateTime.Now.AddHours(21),0.48, "inches"),
            new ChartPoint(DateTime.Now.AddHours(24), 0.36, "inches"),
            new ChartPoint(DateTime.Now.AddHours(27), 0.31, "inches"),
            new ChartPoint(DateTime.Now.AddHours(30), 0.25, "inches"),
            new ChartPoint(DateTime.Now.AddHours(33), 0.19, "inches"),
            new ChartPoint(DateTime.Now.AddHours(35), 0.31, "inches"),
        };
        PrecipSeries = new List<ChartSeries> { new ChartSeries { Points = points } };
    }
    #endregion
}