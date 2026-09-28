using DdoItemTracker.Presentation.ViewModels;

namespace DdoItemTracker.Views;

public partial class CopyGroupsView : ContentView
{
    public CopyGroupsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Raised when a copy is tapped. When nothing handles it the item page opens, as on phones;
    /// a page showing a side pane handles it itself.
    /// </summary>
    public event EventHandler<MyItemRow>? CopySelected;

    public void ClearSelection() => List.SelectedItem = null;

    private async void OnSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not MyItemRow row || BindingContext is not MyItemsViewModel vm) return;
        if (CopySelected is not null)
        {
            CopySelected(this, row);
            return;
        }
        List.SelectedItem = null;
        await vm.OpenCopyCommand.ExecuteAsync(row);
    }
}
