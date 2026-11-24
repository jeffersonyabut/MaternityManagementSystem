using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.ViewModels;

public partial class StaffManagementViewModel : ObservableObject
{
    private readonly StaffManagementService _staffManagementService;
    private readonly RegionalSettingsService _regionalSettingsService;

    private readonly ObservableCollection<StaffDisplayModel> _allStaff = new();

    public StaffManagementViewModel(
        StaffManagementService staffManagementService,
        RegionalSettingsService regionalSettingsService)
    {
        ArgumentNullException.ThrowIfNull(staffManagementService);
        ArgumentNullException.ThrowIfNull(regionalSettingsService);

        _staffManagementService = staffManagementService;
        _regionalSettingsService = regionalSettingsService;
    }

    // STAFF COLLECTIONS

    [ObservableProperty]
    private ObservableCollection<StaffDisplayModel> staffMembers = new();

    [ObservableProperty]
    private StaffDisplayModel? selectedStaff;

    // SEARCH AND FILTERS

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string selectedRole = "All";

    [ObservableProperty]
    private string selectedAccountStatus = "All";

    public ObservableCollection<string> RoleOptions { get; } =
    [
        "All",
        StaffRoles.Admin,
        StaffRoles.Staff
    ];

    public ObservableCollection<string> AccountStatusOptions { get; } =
    [
        "All",
        "Active",
        "Inactive"
    ];

    // SUMMARY COUNTS

    [ObservableProperty]
    private int totalStaffCount;

    [ObservableProperty]
    private int activeStaffCount;

    [ObservableProperty]
    private int inactiveStaffCount;

    [ObservableProperty]
    private int adminCount;

    // GENERAL STATE

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    // PROPERTY CHANGE HANDLERS

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedRoleChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedAccountStatusChanged(string value)
    {
        ApplyFilters();
    }

    // LOAD STAFF

    public async Task LoadStaffAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var records =
                await _staffManagementService.GetAllStaffAsync();

            _allStaff.Clear();

            foreach (var staff in records)
            {
                staff.CreatedAtRegionalDisplay =
                    _regionalSettingsService.FormatDateTime(
                        staff.CreatedAtUtc);

                _allStaff.Add(staff);
            }

            UpdateSummaryCounts();
            ApplyFilters();

            StatusMessage = _allStaff.Count == 0
                ? "No staff accounts have been created yet."
                : $"{_allStaff.Count} staff account(s) loaded.";
        }
        catch (Exception)
        {
            StatusMessage = "Unable to load staff accounts.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // SUMMARY CALCULATIONS

    private void UpdateSummaryCounts()
    {
        TotalStaffCount = _allStaff.Count;

        ActiveStaffCount = _allStaff.Count(staff =>
            staff.IsActive);

        InactiveStaffCount = _allStaff.Count(staff =>
            !staff.IsActive);

        AdminCount = _allStaff.Count(staff =>
            staff.Role == StaffRoles.Admin);
    }

    // SEARCH AND FILTERING

    private void ApplyFilters()
    {
        string search = SearchText.Trim();

        var filtered = _allStaff
            .Where(staff =>
            {
                bool matchesSearch =
                    string.IsNullOrWhiteSpace(search) ||
                    staff.Username.Contains(
                        search,
                        StringComparison.OrdinalIgnoreCase);

                bool matchesRole =
                    SelectedRole == "All" ||
                    string.Equals(
                        staff.Role,
                        SelectedRole,
                        StringComparison.OrdinalIgnoreCase);

                bool matchesStatus =
                    SelectedAccountStatus == "All" ||
                    (SelectedAccountStatus == "Active" && staff.IsActive) ||
                    (SelectedAccountStatus == "Inactive" && !staff.IsActive);

                return matchesSearch &&
                       matchesRole &&
                       matchesStatus;
            })
            .OrderBy(staff => staff.Username)
            .ToList();

        StaffMembers.Clear();

        foreach (var staff in filtered)
        {
            StaffMembers.Add(staff);
        }

        SelectedStaff = null;
    }

    // CREATE STAFF ACCOUNT

    public async Task CreateStaffAsync(
        string username,
        string password,
        string role)
    {
        await _staffManagementService.CreateStaffAsync(
            username,
            password,
            role);

        await LoadStaffAsync();

        StatusMessage = "Staff account created successfully.";
    }

    // UPDATE STAFF ACCOUNT

    public async Task UpdateStaffAsync(
        int staffId,
        string username,
        string role)
    {
        await _staffManagementService.UpdateStaffAsync(
            staffId,
            username,
            role);

        await LoadStaffAsync();

        StatusMessage = "Staff account updated successfully.";
    }

    // CHANGE STAFF ACCOUNT STATUS

    public async Task SetStaffStatusAsync(
        int staffId,
        bool isActive)
    {
        await _staffManagementService.SetStaffStatusAsync(
            staffId,
            isActive);

        await LoadStaffAsync();

        StatusMessage = isActive
            ? "Staff account activated successfully."
            : "Staff account deactivated successfully.";
    }

    // RESET STAFF PASSWORD

    public async Task ResetStaffPasswordAsync(
        int staffId,
        string newPassword)
    {
        await _staffManagementService.ResetStaffPasswordAsync(
            staffId,
            newPassword);

        await LoadStaffAsync();

        StatusMessage = "Staff password reset successfully.";
    }

    // CLEAR FILTERS

    public void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedRole = "All";
        SelectedAccountStatus = "All";
    }
}
