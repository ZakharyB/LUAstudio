using System.Windows;
using System.Windows.Controls;

namespace LUAstudio;

public class InputDialog : Window
{
    public string? Result { get; private set; }

    public InputDialog(string title, string prompt)
    {
        Title = title;
        Width = 300;
        Height = 150;
        var txtInput = new TextBox { Name = "txtInput", Margin = new Thickness(10) };
        var okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(5), IsDefault = true };
        var cancelButton = new Button { Content = "Cancel", Width = 75, Margin = new Thickness(5), IsCancel = true };

        okButton.Click += (s, e) => { Result = txtInput.Text; DialogResult = true; };
        cancelButton.Click += (s, e) => DialogResult = false;

        Content = new StackPanel
        {
            Children =
            {
                new Label { Content = prompt },
                txtInput,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children = { okButton, cancelButton }
                }
            }
        };

        Loaded += (s, e) => txtInput.Focus();
    }
}