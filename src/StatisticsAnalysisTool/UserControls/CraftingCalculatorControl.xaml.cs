using StatisticsAnalysisTool.Models;
using StatisticsAnalysisTool.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace StatisticsAnalysisTool.UserControls;

/// <summary>
/// Interaction logic for CraftingCalculatorControl.xaml
/// </summary>
public partial class CraftingCalculatorControl
{
    public CraftingCalculatorControl()
    {
        InitializeComponent();
    }

    #region Ui events

    private void LvItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var item = (Item) ((ListView) sender).SelectedValue;
        MainWindowViewModel.OpenItemWindow(item);
    }

    private void FilterReset_MouseUp(object sender, MouseButtonEventArgs e)
    {
        var vm = (MainWindowViewModel) DataContext;
        vm?.ItemFilterReset();
    }

    #endregion
}
