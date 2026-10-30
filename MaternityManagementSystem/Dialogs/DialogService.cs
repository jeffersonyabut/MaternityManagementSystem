
using System.Windows;
using System.Windows.Media;

namespace MaternityManagementSystem.Dialogs;

public class DialogService
{
    public bool ShowConfirmation(
        string title,
        string message,
        string confirmText = "Confirm",
        string cancelText = "Cancel")
    {
        CustomDialog dialog = CreateDialog(
            DialogType.Confirmation,
            title,
            message,
            confirmText,
            cancelText,
            showCancel: true);

        return dialog.ShowDialog() == true;
    }

    public string? ShowInput(
        string title,
        string message,
        string inputPlaceholder = "",
        string initialInput = "",
        string confirmText = "Submit",
        string cancelText = "Cancel")
    {
        CustomDialog dialog = CreateDialog(
            DialogType.Input,
            title,
            message,
            confirmText,
            cancelText,
            showCancel: true,
            showInput: true,
            inputPlaceholder: inputPlaceholder,
            initialInput: initialInput);

        return dialog.ShowDialog() == true
            ? dialog.InputValue
            : null;
    }

    public void ShowSuccess(string title, string message)
    {
        ShowMessage(DialogType.Success, title, message);
    }

    public void ShowError(string title, string message)
    {
        ShowMessage(DialogType.Error, title, message);
    }

    public void ShowWarning(string title, string message)
    {
        ShowMessage(DialogType.Warning, title, message);
    }

    public void ShowInformation(string title, string message)
    {
        ShowMessage(DialogType.Information, title, message);
    }

    private void ShowMessage(
        DialogType type,
        string title,
        string message)
    {
        CustomDialog dialog = CreateDialog(
            type,
            title,
            message,
            confirmText: "OK",
            showCancel: false);

        dialog.ShowDialog();
    }

    private CustomDialog CreateDialog(
        DialogType type,
        string title,
        string message,
        string confirmText,
        string cancelText = "Cancel",
        bool showCancel = true,
        bool showInput = false,
        string inputPlaceholder = "",
        string initialInput = "")
    {
        CustomDialog dialog = new();

        Window? owner = Application.Current?.MainWindow;

        if (owner != null && owner != dialog)
        {
            dialog.Owner = owner;
        }

        dialog.Configure(
            title,
            message,
            confirmText,
            cancelText,
            showCancel,
            showInput,
            inputPlaceholder,
            initialInput);

        ApplyAppearance(dialog, type);

        return dialog;
    }

    private static void ApplyAppearance(
        CustomDialog dialog,
        DialogType type)
    {
        string icon;
        string iconColor;
        string iconBackground;
        Brush confirmBackground;

        switch (type)
        {
            case DialogType.Confirmation:
                icon = "?";
                iconColor = "#2563EB";
                iconBackground = "#DBEAFE";
                confirmBackground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#2563EB"));
                break;

            case DialogType.Input:
                icon = "✎";
                iconColor = "#2563EB";
                iconBackground = "#DBEAFE";
                confirmBackground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#2563EB"));
                break;

            case DialogType.Success:
                icon = "✓";
                iconColor = "#15803D";
                iconBackground = "#DCFCE7";
                confirmBackground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#15803D"));
                break;

            case DialogType.Error:
                icon = "×";
                iconColor = "#DC2626";
                iconBackground = "#FEE2E2";
                confirmBackground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#DC2626"));
                break;

            case DialogType.Warning:
                icon = "!";
                iconColor = "#D97706";
                iconBackground = "#FEF3C7";
                confirmBackground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#D97706"));
                break;

            default:
                icon = "i";
                iconColor = "#2563EB";
                iconBackground = "#DBEAFE";
                confirmBackground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#2563EB"));
                break;
        }

        dialog.SetAppearance(
            icon,
            iconColor,
            iconBackground,
            confirmBackground);
    }
}
