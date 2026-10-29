
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.ViewModels;

public partial class PregnancyRegistrationViewModel : ObservableObject
{
    private readonly PregnancyService _pregnancyService;
    private readonly int _patientId;

    public PregnancyRegistrationViewModel(
        PregnancyService pregnancyService,
        int patientId)
    {
        ArgumentNullException.ThrowIfNull(pregnancyService);

        if (patientId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patientId));
        }

        _pregnancyService = pregnancyService;
        _patientId = patientId;
    }

    [ObservableProperty]
    private string status = "Current";

    [ObservableProperty]
    private DateTime? lastMenstrualPeriod;

    [ObservableProperty]
    private DateTime? estimatedDueDate;

    [ObservableProperty]
    private DateTime? pregnancyEndDate;

    [ObservableProperty]
    private string notes = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    public event Action? PregnancySaved;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var pregnancy = new Pregnancy
            {
                PatientId = _patientId,
                Status = Status,
                LastMenstrualPeriod = ToDateOnly(LastMenstrualPeriod),
                EstimatedDueDate = ToDateOnly(EstimatedDueDate),
                PregnancyEndDate = ToDateOnly(PregnancyEndDate),
                Notes = Notes
            };

            await _pregnancyService.CreatePregnancyAsync(pregnancy);

            StatusMessage = "Pregnancy record saved successfully.";
            PregnancySaved?.Invoke();
        }
        catch (ArgumentException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (Exception)
        {
            StatusMessage = "Unable to save the pregnancy record. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static DateOnly? ToDateOnly(DateTime? date)
    {
        return date.HasValue
            ? DateOnly.FromDateTime(date.Value)
            : null;
    }
}
