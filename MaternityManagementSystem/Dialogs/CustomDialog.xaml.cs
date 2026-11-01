
using System.Windows;
using System.Windows.Media;

namespace MaternityManagementSystem.Dialogs;

public partial class CustomDialog : Window
{
    public string InputValue => InputTextBox.Text;

    private bool _requireInput;

    public CustomDialog()
    {
        InitializeComponent();

        ConfirmButton.Click += ConfirmButton_Click;

        CancelButton.Click += (_, _) =>
        {
            DialogResult = false;
        };

        InputTextBox.TextChanged += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(InputTextBox.Text))
            {
                InputValidationText.Visibility = Visibility.Collapsed;
            }
        };
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        if (_requireInput &&
            string.IsNullOrWhiteSpace(InputTextBox.Text))
        {
            InputValidationText.Text = "Please provide a reason before continuing.";
            InputValidationText.Visibility = Visibility.Visible;
            InputTextBox.Focus();
            return;
        }

        DialogResult = true;
    }

    public void Configure(
        string title,
        string message,
        string confirmText = "Confirm",
        string cancelText = "Cancel",
        bool showCancel = true,
        bool showInput = false,
        string inputPlaceholder = "",
        string initialInput = "",
        bool requireInput = false)
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

        InputValidationText.Visibility = Visibility.Collapsed;

        InputTextBox.ToolTip = inputPlaceholder;
        InputTextBox.Text = initialInput;

        _requireInput = showInput && requireInput;

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
