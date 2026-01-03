using System.Windows;

namespace DicomEditor.Controls;

/// <summary>
/// A simple input dialog for entering single values.
/// </summary>
public partial class InputDialog : Window
{
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(InputDialog), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty InputValueProperty =
        DependencyProperty.Register(nameof(InputValue), typeof(string), typeof(InputDialog), new PropertyMetadata(string.Empty));

    /// <summary>
    /// Gets or sets the message displayed to the user.
    /// </summary>
    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>
    /// Gets or sets the input value.
    /// </summary>
    public string InputValue
    {
        get => (string)GetValue(InputValueProperty);
        set => SetValue(InputValueProperty, value);
    }

    public InputDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        };
    }

    /// <summary>
    /// Creates and shows the input dialog.
    /// </summary>
    /// <param name="title">Dialog title</param>
    /// <param name="message">Prompt message</param>
    /// <param name="defaultValue">Default value</param>
    /// <param name="owner">Owner window</param>
    /// <returns>The entered value or null if cancelled</returns>
    public static string? Show(string title, string message, string? defaultValue = null, Window? owner = null)
    {
        var dialog = new InputDialog
        {
            Title = title,
            Message = message,
            InputValue = defaultValue ?? string.Empty,
            Owner = owner ?? Application.Current.MainWindow
        };

        return dialog.ShowDialog() == true ? dialog.InputValue : null;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
