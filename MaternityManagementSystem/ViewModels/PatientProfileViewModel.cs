
using CommunityToolkit.Mvvm.ComponentModel;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;
using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

namespace MaternityManagementSystem.ViewModels;

public partial class PatientProfileViewModel : ObservableObject
{
    private readonly PatientService _patientService;
    private readonly PregnancyService _pregnancyService;
    private readonly RecordVersionService _recordVersionService;
    private readonly RegionalSettingsService _regionalSettingsService;
    private readonly int _patientId;
    private readonly ObstetricHistoryService _obstetricHistoryService = new();

    public PatientProfileViewModel(
        PatientService patientService,
        PregnancyService pregnancyService,
        RecordVersionService recordVersionService,
        RegionalSettingsService regionalSettingsService,
        int patientId)
    {
        ArgumentNullException.ThrowIfNull(patientService);
        ArgumentNullException.ThrowIfNull(pregnancyService);
        ArgumentNullException.ThrowIfNull(recordVersionService);
        ArgumentNullException.ThrowIfNull(regionalSettingsService);

        if (patientId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patientId));
        }

        _patientService = patientService;
        _pregnancyService = pregnancyService;
        _recordVersionService = recordVersionService;
        _regionalSettingsService = regionalSettingsService;
        _patientId = patientId;
    }

    [ObservableProperty]
    private Patient? patient;

    [ObservableProperty]
    private ObservableCollection<PregnancyCardDisplayViewModel> pregnancies = new();

    [ObservableProperty]
    private ObservableCollection<RecordVersion> recordVersions = new();

    [ObservableProperty]
    private ObservableCollection<RecordVersionDisplayViewModel> recordHistory = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private int gravida;

    [ObservableProperty]
    private int term;

    [ObservableProperty]
    private int preterm;

    [ObservableProperty]
    private int abortion;

    [ObservableProperty]
    private int unclassifiedPregnancies;

    [ObservableProperty]
    private string livingChildrenDisplay = "Not recorded";

    public async Task LoadPatientAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            Patient = await _patientService.GetPatientByIdAsync(_patientId);

            if (Patient is null)
            {
                StatusMessage = "This patient record could not be found.";
                Pregnancies.Clear();
                RecordVersions.Clear();
                RecordHistory.Clear();
                return;
            }

            await LoadPregnanciesAsync();

            CalculateObstetricHistory();

            await LoadRecordHistoryAsync();
        }
        catch (Exception)
        {
            StatusMessage = "Unable to load the patient profile.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadPregnanciesAsync()
    {
        var records = await _pregnancyService.GetByPatientIdAsync(_patientId);

        Pregnancies.Clear();

        foreach (var pregnancy in records)
        {
            var history = await _recordVersionService.GetHistoryAsync(
                "Pregnancy",
                pregnancy.Id);

            Pregnancies.Add(new PregnancyCardDisplayViewModel
            {
                Pregnancy = pregnancy,
                HasHistory = history.Count > 0
            });
        }
    }

    private async Task LoadRecordHistoryAsync()
    {
        var history = await _recordVersionService.GetHistoryAsync(
            "Patient",
            _patientId);

        RecordVersions.Clear();
        RecordHistory.Clear();

        foreach (var version in history)
        {
            RecordVersions.Add(version);

            var displayVersion = new RecordVersionDisplayViewModel
            {
                Username = version.Username,
                ChangedAtUtc = _regionalSettingsService.ConvertUtcToLocal(
                    version.ChangedAtUtc)
            };

            try
            {
                using var previousDocument =
                    JsonDocument.Parse(version.PreviousValuesJson);

                using var newDocument =
                    JsonDocument.Parse(version.NewValuesJson);

                foreach (var property in newDocument.RootElement.EnumerateObject())
                {
                    string fieldName = property.Name;

                    if (!previousDocument.RootElement.TryGetProperty(
                        fieldName,
                        out var previousValue))
                    {
                        continue;
                    }

                    displayVersion.Changes.Add(
                        new RecordVersionFieldChange
                        {
                            FieldName = FormatFieldName(fieldName),
                            PreviousValue = FormatValue(previousValue),
                            NewValue = FormatValue(property.Value)
                        });
                }
            }
            catch (JsonException)
            {
                // Skip malformed snapshots rather than breaking the profile.
                continue;
            }

            RecordHistory.Add(displayVersion);
        }
    }

    private static string FormatFieldName(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return fieldName;
        }

        var result = new System.Text.StringBuilder();

        for (int i = 0; i < fieldName.Length; i++)
        {
            if (i > 0 && char.IsUpper(fieldName[i]))
            {
                result.Append(' ');
            }

            result.Append(fieldName[i]);
        }

        return result.ToString();
    }

    private static string FormatValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null => "Not provided",
            JsonValueKind.String => value.GetString() ?? "Not provided",
            JsonValueKind.True => "Yes",
            JsonValueKind.False => "No",
            JsonValueKind.Undefined => "Not provided",
            _ => value.ToString()
        };
    }

    private void CalculateObstetricHistory()
    {
        var summary = _obstetricHistoryService.Calculate(
            Pregnancies.Select(card => card.Pregnancy).ToList());

        Gravida = summary.Gravida;
        Term = summary.Term;
        Preterm = summary.Preterm;
        Abortion = summary.Abortion;
        UnclassifiedPregnancies = summary.UnclassifiedPregnancies;
        LivingChildrenDisplay = summary.LivingChildrenDisplay;
    }

    private void CalculateLivingChildren()
    {
        int totalLivingChildren = 0;
        bool hasIncompleteData = false;

        foreach (var card in Pregnancies)
        {
            var pregnancy = card.Pregnancy;

            if (pregnancy.Status == "Current")
            {
                continue;
            }

            if (pregnancy.Status != "Completed")
            {
                hasIncompleteData = true;
                continue;
            }

            if (!pregnancy.LivingChildrenCount.HasValue)
            {
                hasIncompleteData = true;
                continue;
            }

            totalLivingChildren += pregnancy.LivingChildrenCount.Value;
        }

        LivingChildrenDisplay = hasIncompleteData
            ? "Incomplete data"
            : totalLivingChildren.ToString();
    }
}
