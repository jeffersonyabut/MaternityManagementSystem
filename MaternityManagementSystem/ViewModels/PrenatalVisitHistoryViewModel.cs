
using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.ViewModels;

public partial class PrenatalVisitHistoryViewModel : ObservableObject
{
    private readonly RecordVersionService _recordVersionService;
    private readonly RegionalSettingsService _regionalSettingsService;
    private readonly int _visitId;

    public PrenatalVisitHistoryViewModel(
        RecordVersionService recordVersionService,
        RegionalSettingsService regionalSettingsService,
        int visitId)
    {
        ArgumentNullException.ThrowIfNull(recordVersionService);
        ArgumentNullException.ThrowIfNull(regionalSettingsService);

        if (visitId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visitId));
        }

        _recordVersionService = recordVersionService;
        _regionalSettingsService = regionalSettingsService;
        _visitId = visitId;
    }

    [ObservableProperty]
    private ObservableCollection<RecordVersionDisplayViewModel> history = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public async Task LoadHistoryAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var versions = await _recordVersionService.GetHistoryAsync(
                "PrenatalVisit",
                _visitId);

            History.Clear();

            foreach (var version in versions)
            {
                var displayVersion = new RecordVersionDisplayViewModel
                {
                    Username = version.Username,
                    ChangedAtUtc = version.ChangedAtUtc,
                    ChangedAtDisplay = _regionalSettingsService.FormatDateTime(
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
                    continue;
                }

                History.Add(displayVersion);
            }
        }
        catch (Exception)
        {
            StatusMessage = "Unable to load prenatal visit history.";
        }
        finally
        {
            IsBusy = false;
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
}
