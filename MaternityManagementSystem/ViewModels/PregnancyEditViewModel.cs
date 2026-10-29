
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.ViewModels;

public partial class PregnancyEditViewModel : ObservableObject
{
    private readonly PregnancyService _pregnancyService;
    private readonly int _pregnancyId;
    private readonly int _patientId;

    public PregnancyEditViewModel(
        PregnancyService pregnancyService,
        Pregnancy pregnancy)
    {
        ArgumentNullException.ThrowIfNull(pregnancyService);
        ArgumentNullException.ThrowIfNull(pregnancy);

        if (pregnancy.Id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pregnancy));
        }

        _pregnancyService = pregnancyService;
        _pregnancyId = pregnancy.Id;
        _patientId = pregnancy.PatientId;

        Status = pregnancy.Status;
        LastMenstrualPeriod = ToDateTime(pregnancy.LastMenstrualPeriod);
        EstimatedDueDate = ToDateTime(pregnancy.EstimatedDueDate);
        PregnancyEndDate = ToDateTime(pregnancy.PregnancyEndDate);
        Notes = pregnancy.Notes ?? string.Empty;
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

    public event Action? PregnancyUpdated;

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
            var updatedPregnancy = new Pregnancy
            {
                Id = _pregnancyId,
                PatientId = _patientId,
                Status = Status,
                LastMenstrualPeriod = ToDateOnly(LastMenstrualPeriod),
                EstimatedDueDate = ToDateOnly(EstimatedDueDate),
                PregnancyEndDate = ToDateOnly(PregnancyEndDate),
                Notes = Notes
            };

            await _pregnancyService.UpdatePregnancyAsync(updatedPregnancy);

            StatusMessage = "Pregnancy record updated successfully.";
            PregnancyUpdated?.Invoke();
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
            StatusMessage =
                "Unable to update the pregnancy record. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static DateTime? ToDateTime(DateOnly? date)
        => date.HasValue
            ? date.Value.ToDateTime(TimeOnly.MinValue)
            : null;

    private static DateOnly? ToDateOnly(DateTime? date)
        => date.HasValue
            ? DateOnly.FromDateTime(date.Value)
            : null;
}
