
using System;
using System.Collections.ObjectModel;

namespace MaternityManagementSystem.ViewModels;

public class RecordVersionDisplayViewModel
{
    public string Username { get; set; } = string.Empty;

    public DateTime ChangedAtUtc { get; set; }

    public string ChangedAtDisplay { get; set; } = string.Empty;

    public ObservableCollection<RecordVersionFieldChange> Changes { get; set; } = new();
}

public class RecordVersionFieldChange
{
    public string FieldName { get; set; } = string.Empty;

    public string PreviousValue { get; set; } = string.Empty;

    public string NewValue { get; set; } = string.Empty;
}
