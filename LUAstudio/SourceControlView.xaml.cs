using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace LUAstudio;

public partial class SourceControlView : UserControl
{
    public SourceControlView() => InitializeComponent();
}

public sealed class StageButtonTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? "−" : "+";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
