using DdoItemTracker.Presentation.ViewModels;

namespace DdoItemTracker.Views;

public partial class CopyEditorPage : ContentPage, IQueryAttributable
{
    private readonly CopyEditorViewModel _viewModel;

    public CopyEditorPage(CopyEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var key = query.TryGetValue("key", out var k) ? k as string : null;
        var copyId = query.TryGetValue("copyId", out var c) ? c as string : null;
        if (key is not null) _viewModel.Load(key, string.IsNullOrEmpty(copyId) ? null : copyId);
    }
}
