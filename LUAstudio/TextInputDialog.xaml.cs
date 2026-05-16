using System.Windows;

namespace LUAstudio;

public partial class TextInputDialog : Window
{
    public TextInputDialog(string title, string message, string defaultValue)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        InputBox.Text = defaultValue;
        InputBox.SelectAll();
        Loaded += (_, _) => InputBox.Focus();
    }

    public string? Result { get; private set; }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result = InputBox.Text;
        DialogResult = true;
    }
}
