using System.Windows;
using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Dialogs;

public partial class AuditLogDetailsDialog : Window
{
    public AuditLogDetailsDialog(AuditLog auditLog)
    {
        ArgumentNullException.ThrowIfNull(auditLog);

        InitializeComponent();

        ActionText.Text = auditLog.Action;
        TimestampText.Text = auditLog.OccurredAtRegionalDisplay;

        UsernameText.Text = auditLog.Username;
        EntityTypeText.Text = auditLog.EntityType;
        EntityIdText.Text = auditLog.EntityId?.ToString() ?? "Not applicable";
        AuditLogIdText.Text = auditLog.Id.ToString();

        DetailsText.Text = string.IsNullOrWhiteSpace(auditLog.Details)
            ? "No additional details recorded."
            : auditLog.Details;

        bool hasReason = !string.IsNullOrWhiteSpace(auditLog.Reason);

        ReasonSection.Visibility = hasReason
            ? Visibility.Visible
            : Visibility.Collapsed;

        ReasonText.Text = hasReason
            ? auditLog.Reason
            : string.Empty;
    }
}
