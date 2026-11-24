
using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.ViewModels;

public partial class PregnancyHistoryViewModel : ObservableObject
{
    private readonly RecordVersionService _recordVersionService;
    private readonly RegionalSettingsService _regionalSettingsService;
    private readonly int _pregnancyId;

    public PregnancyHistoryViewModel(
        RecordVersionService recordVersionService,
        RegionalSettingsService regionalSettingsService,
        int pregnancyId)
    {
        ArgumentNullException.ThrowIfNull(recordVersionService);
        ArgumentNullException.ThrowIfNull(regionalSettingsService);

        if (pregnancyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pregnancyId));
        }

        _recordVersionService = recordVersionService;
        _regionalSettingsService = regionalSettingsService;
        _pregnancyId = pregnancyId;
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
        History.Clear();

        try
        {
            var versions = await _recordVersionService.GetHistoryAsync(
                "Pregnancy",
                _pregnancyId);

            foreach (var version in versions)
            {
                var displayVersion = new RecordVersionDisplayViewModel
                {
                    Username = version.Username,
                    ChangedAtUtc = _regionalSettingsService.ConvertUtcToLocal(
                        version.ChangedAtUtc)
                };

                using var previousDocument =
                    JsonDocument.Parse(version.PreviousValuesJson);

                using var newDocument =
                    JsonDocument.Parse(version.NewValuesJson);

                foreach (var property in newDocument.RootElement.EnumerateObject())
                {
                    if (!previousDocument.RootElement.TryGetProperty(
                        property.Name,
                        out var previousValue))
                    {
                        continue;
                    }

                    displayVersion.Changes.Add(
                        new RecordVersionFieldChange
                        {
                            FieldName = FormatFieldName(property.Name),
                            PreviousValue = FormatValue(previousValue),
                            NewValue = FormatValue(property.Value)
                        });
                }

                History.Add(displayVersion);
            }

            if (History.Count == 0)
            {
                StatusMessage = "No pregnancy record changes have been recorded.";
            }
        }
        catch (JsonException)
        {
            StatusMessage = "Unable to read some pregnancy history records.";
        }
        catch (Exception)
        {
            StatusMessage = "Unable to load pregnancy record history.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FormatFieldName(string fieldName)
    {
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
