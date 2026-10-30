
using System.Windows;
using System.Windows.Media;

namespace MaternityManagementSystem.Dialogs;

public partial class CustomDialog : Window
{
    public string InputValue => InputTextBox.Text;

    public CustomDialog()
    {
        InitializeComponent();

        ConfirmButton.Click += (_, _) =>
        {
            DialogResult = true;
        };

        CancelButton.Click += (_, _) =>
        {
            DialogResult = false;
        };
    }

    public void Configure(
        string title,
        string message,
        string confirmText = "Confirm",
        string cancelText = "Cancel",
        bool showCancel = true,
        bool showInput = false,
        string inputPlaceholder = "",
        string initialInput = "")
    {
        TitleText.Text = title;
        MessageText.Text = message;

        ConfirmButton.Content = confirmText;
        CancelButton.Content = cancelText;

        CancelButton.Visibility = showCancel
            ? Visibility.Visible
            : Visibility.Collapsed;

        InputTextBox.Visibility = showInput
            ? Visibility.Visible
            : Visibility.Collapsed;

        InputTextBox.ToolTip = inputPlaceholder;
        InputTextBox.Text = initialInput;

        if (showInput)
        {
            Loaded += (_, _) =>
            {
                InputTextBox.Focus();
                InputTextBox.CaretIndex = InputTextBox.Text.Length;
            };
        }
    }

    public void SetAppearance(
        string icon,
        string iconColor,
        string iconBackground,
        Brush confirmBackground)
    {
        IconText.Text = icon;
        IconText.Foreground = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(iconColor));

        IconContainer.Background = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(iconBackground));

        ConfirmButton.Background = confirmBackground;
        ConfirmButton.Foreground = Brushes.White;
    }
}
