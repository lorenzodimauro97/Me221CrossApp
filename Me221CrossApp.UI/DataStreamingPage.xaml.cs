using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ME221CrossApp.Models;
using ME221CrossApp.Services;

namespace Me221CrossApp.UI;

public partial class DataStreamingPage : ContentPage
{
    private readonly IEcuInteractionService _ecuInteractionService;
    private CancellationTokenSource? _cts;
    private readonly Dictionary<ushort, RealtimeDataPointViewModel> _dataPointMap = new();
    public ObservableCollection<RealtimeDataPointViewModel> DataPoints { get; } = new();

    public DataStreamingPage(IEcuInteractionService ecuInteractionService)
    {
        InitializeComponent();
        _ecuInteractionService = ecuInteractionService;
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _cts = new CancellationTokenSource();
        _ = StartStreamingAsync(_cts.Token);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task StartStreamingAsync(CancellationToken token)
    {
        try
        {
            await foreach (var dataPointBatch in _ecuInteractionService.StreamRealtimeDataAsync(token))
            {
                if (token.IsCancellationRequested) break;
                
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    foreach (var dp in dataPointBatch)
                    {
                        if (_dataPointMap.TryGetValue(dp.Id, out var vm))
                        {
                            vm.Value = dp.Value;
                        }
                        else
                        {
                            var newVm = new RealtimeDataPointViewModel { Id = dp.Id, Name = dp.Name, Value = dp.Value };
                            _dataPointMap[dp.Id] = newVm;
                            DataPoints.Add(newVm);
                        }
                    }
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (this.IsLoaded)
                {
                    await DisplayAlert("Streaming Error", $"An error occurred during data streaming: {ex.Message}", "OK");
                    await Shell.Current.GoToAsync("..");
                }
            });
        }
    }

    public class RealtimeDataPointViewModel : INotifyPropertyChanged
    {
        public ushort Id { get; init; }
        public required string Name { get; init; }

        private float _value;
        public float Value
        {
            get => _value;
            set
            {
                if (Math.Abs(_value - value) > 0.001f)
                {
                    _value = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}