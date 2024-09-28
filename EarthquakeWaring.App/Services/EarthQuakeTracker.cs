﻿using EarthquakeWaring.App.Infrastructure.Models.EarthQuakeModels;
using EarthquakeWaring.App.Infrastructure.Models.SettingModels;
using EarthquakeWaring.App.Infrastructure.ServiceAbstraction;
using EarthquakeWaring.App.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using EarthquakeWaring.App.Infrastructure.Models.ApiModels;

namespace EarthquakeWaring.App.Services;

public class EarthQuakeTracker : IEarthQuakeTracker
{
    private readonly IEarthQuakeApiWrapper _earthQuakeApi;
    private readonly IEarthQuakeCalculator _earthQuakeCalculator;
    private readonly ISetting<CurrentPosition> _currentPosition;
    private readonly ISetting<AlertLimit> _alertLimit;
    private readonly ISetting<TrackerSetting> _trackerSetting;
    private readonly ILogger<EarthQuakeTracker> _logger;
    private readonly IServiceProvider _service;

    private EarlyWarningWindow? _warningWindow;
    private EarthQuakeTrackingInformation _trackingInformation = new();

    private CancellationTokenSource? _tokenSource;
    private CancellationToken _cancellationToken;

    public EarthQuakeTracker(IEarthQuakeApiWrapper earthQuakeApi, IEarthQuakeCalculator earthQuakeCalculator,
                             ISetting<CurrentPosition> currentPosition, ILogger<EarthQuakeTracker> logger,
                             ISetting<AlertLimit> alertLimit,
                             IServiceProvider service, ISetting<TrackerSetting> trackerSetting)
    {
        _earthQuakeApi = earthQuakeApi;
        _earthQuakeCalculator = earthQuakeCalculator;
        _currentPosition = currentPosition;
        _logger = logger;
        _alertLimit = alertLimit;
        _service = service;
        _trackerSetting = trackerSetting;
    }

    public TimeSpan SimulateTimeSpan { get; set; } = TimeSpan.Zero;
    public List<EarthQuakeInfoBase>? SimulateUpdates { get; set; } = null;

    public async Task StartTrack(EarthQuakeInfoBase earthQuakeInfo, CancellationTokenSource cancellationTokenSource)
    {
        _tokenSource = cancellationTokenSource;
        _cancellationToken = cancellationTokenSource.Token;
        while (!cancellationTokenSource.Token.IsCancellationRequested)
        {
            await CheckEarthQuake(earthQuakeInfo).ConfigureAwait(false);
            await Task.Delay(_trackerSetting?.Setting?.TrackerTimeSpanMillisecond * 100 ?? 500, _cancellationToken)
                      .ConfigureAwait(false);
        }
    }

    private async Task CheckEarthQuake(EarthQuakeInfoBase earthQuakeInfo)
    {
        _logger.LogInformation("Checking earthquake shocking at {Time} at {Position} with Magnitude {DayMagnitude}",
                              earthQuakeInfo.StartAt,
                              earthQuakeInfo.PlaceName,
                               earthQuakeInfo.Magnitude);
        if (SimulateUpdates is null && (DateTime.Now - earthQuakeInfo.StartAt).TotalMinutes > 5)
        {
            _logger.LogWarning("Earthquake Expired 5 minutes, exit");
            _tokenSource?.Cancel();
            return;
        }
            
        var infos = SimulateUpdates ??
                    await _earthQuakeApi.GetEarthQuakeInfo(earthQuakeInfo.Id,
                                                           _cancellationToken);
        EarthQuakeInfoBase latestInfo;
        var timeHandler = _service.GetService<ITimeHandler>();
        if (SimulateTimeSpan == TimeSpan.Zero)
        {
            latestInfo = infos[^1];
        }
        else
        {
            _logger.LogWarning("Simulating with simulatingInfo");
            var simulatingInfo =
                infos.FirstOrDefault(t => t.UpdateAt > DateTime.Now + timeHandler!.Offset - SimulateTimeSpan);
            if (infos.Count <= 0 && simulatingInfo == null) return;
            simulatingInfo ??= infos[^1];
            latestInfo = simulatingInfo;
        }


        Application.Current.Dispatcher.Invoke(() =>
        {
            // Update the tracking information
            _trackingInformation.Position = latestInfo.PlaceName;
            _trackingInformation.StartTime = latestInfo.StartAt;
            _trackingInformation.UpdateTime = latestInfo.UpdateAt;
            _trackingInformation.Depth = latestInfo.Depth;
            _trackingInformation.Latitude = latestInfo.Latitude;
            _trackingInformation.Longitude = latestInfo.Longitude;
            _trackingInformation.Id = latestInfo.Id;
            _trackingInformation.Magnitude = latestInfo.Magnitude;


            if (_currentPosition.Setting != null)
            {
                _trackingInformation.Distance = _earthQuakeCalculator.GetDistance(_currentPosition.Setting.Latitude,
                    _currentPosition.Setting.Longitude, _trackingInformation.Latitude, _trackingInformation.Longitude);
                _trackingInformation.TheoryCountDown =
                    (int)_earthQuakeCalculator.GetCountDownSeconds(_trackingInformation.Depth,
                                                                   _trackingInformation.Distance);
                _trackingInformation.Intensity =
                    _earthQuakeCalculator.GetIntensity(_trackingInformation.Magnitude, _trackingInformation.Distance);
                _trackingInformation.CountDown = (int)(_trackingInformation.TheoryCountDown -
                                                       (DateTime.Now + timeHandler!.Offset - SimulateTimeSpan -
                                                        _trackingInformation.StartTime).TotalSeconds);
                _trackingInformation.Stage = GetEarthQuakeAlertStage(_trackingInformation);

                _logger.LogTrace(
                    "Tracking Information is {Name}:{DayMagnitude}  Distance:{Distance}km  Intensity:{Intensity} CountDown: {CountDown}",
                    _trackingInformation.Position, _trackingInformation.Magnitude, _trackingInformation.Distance,
                    _trackingInformation.Intensity, _trackingInformation.CountDown);

                if (_warningWindow != null)
                {
                    _logger.LogTrace("Already Popped, Refreshing");
                    return;
                }

                if (SimulateTimeSpan == TimeSpan.Zero)
                    if (_alertLimit.Setting == null || !ShouldPopupAlert(_trackingInformation, _alertLimit.Setting))
                    {
                        _logger.LogTrace("The Limitation (M:{DayMagnitude},I:{Intensity}) is NOT reached, Retrying",
                                         _alertLimit.Setting?.DayMagnitude, _alertLimit.Setting?.DayIntensity);
                        return;
                    }

                _logger.LogInformation(
                    "The Limitation (M:{CurMagnitude}/{DayMagnitude},I:{CurIntensity}/{Intensity}) REACHED, POPING",
                    _trackingInformation.Magnitude, _alertLimit.Setting?.DayMagnitude, _trackingInformation.Intensity,
                    _alertLimit.Setting?.DayIntensity);
                _warningWindow = new EarlyWarningWindow(_trackingInformation, _service);
                _warningWindow.Show();
            }
            else
            {
                _logger.LogError("Current Position Not Set, cannot get the distance! Please set your position");
            }
        }, DispatcherPriority.Normal, _cancellationToken);
    }

    public static EarthQuakeStage GetEarthQuakeAlertStage(EarthQuakeTrackingInformation information)
    {
        return information.Intensity switch
               {
                   >= 5         => EarthQuakeStage.Forced,
                   >= 3 and < 5 => EarthQuakeStage.Emergency,
                   >= 1         => EarthQuakeStage.Warning,
                   < 1          => EarthQuakeStage.Record,
                   _            => EarthQuakeStage.Record
               };
    }

    public static bool ShouldPopupAlert(EarthQuakeTrackingInformation information, AlertLimit alertLimit)
    {
        if (information.UpdateTime.Hour is >= 7 and <= 22)
        {
            // 日间
            return information.Intensity >= alertLimit.DayIntensity && information.Magnitude >= alertLimit.DayMagnitude;
        }

        // 夜间
        return information.Intensity >= alertLimit.NightIntensity && information.Magnitude >= alertLimit.NightIntensity;
    }
}