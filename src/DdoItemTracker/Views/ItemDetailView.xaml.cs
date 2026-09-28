using DdoItemTracker.Presentation.ViewModels;

namespace DdoItemTracker.Views;

public partial class ItemDetailView : ContentView
{
    public ItemDetailView()
    {
        InitializeComponent();
    }

    private ItemDetailViewModel? ViewModel => BindingContext as ItemDetailViewModel;

    private async void OnMemberTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel is { } vm && sender is BindableObject { BindingContext: SetMemberRow row })
            await vm.OpenMemberCommand.ExecuteAsync(row);
    }

    private async void OnEditCopyClicked(object? sender, EventArgs e)
    {
        if (ViewModel is { } vm && sender is BindableObject { BindingContext: CopyRow row })
            await vm.EditCopyCommand.ExecuteAsync(row);
    }

    private async void OnDeleteCopyClicked(object? sender, EventArgs e)
    {
        if (ViewModel is { } vm && sender is BindableObject { BindingContext: CopyRow row })
            await vm.DeleteCopyCommand.ExecuteAsync(row);
    }
}
