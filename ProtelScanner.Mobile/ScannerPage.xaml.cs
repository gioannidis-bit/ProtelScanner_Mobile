using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using ProtelScanner.Mobile.Models;
using ProtelScanner.Mobile.Services;
using Microsoft.Maui.ApplicationModel;

namespace ProtelScanner.Mobile.Views;

public partial class ScannerPage : ContentPage, IQueryAttributable
{
    private readonly IMrzScannerService _scannerService;
    private string _terminalId;

    public ScannerPage(IMrzScannerService scannerService)
    {
        InitializeComponent();
        _scannerService = scannerService;
        _scannerService.MrzDetected += OnMrzDetected;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("TerminalId", out var id))
            _terminalId = id?.ToString();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _scannerService.StartScanningAsync(CameraViewControl);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _scannerService.StopScanning();
    }

    private void OnMrzDetected(object sender, MrzData e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ResultLabel.Text = e.Raw;
        });
    }
}