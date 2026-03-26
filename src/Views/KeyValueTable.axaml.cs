using Avalonia.Controls;
using Kx.Resty.ViewModels;

namespace Kx.Resty.Views;

public partial class KeyValueTable : UserControl
{
    public KeyValueTable()
    {
        InitializeComponent();
        DataContext = new KeyValueTableViewModel();
    }
}
