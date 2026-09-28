namespace DdoItemTracker.Views;

public partial class ItemFilterView : ContentView
{
    public static readonly BindableProperty ShowOwnershipProperty = BindableProperty.Create(
        nameof(ShowOwnership), typeof(bool), typeof(ItemFilterView), true,
        propertyChanged: (view, _, value) =>
        {
            var filter = (ItemFilterView)view;
            filter.OwnershipLabel.IsVisible = (bool)value;
            filter.OwnershipPickers.IsVisible = (bool)value;
        });

    public ItemFilterView()
    {
        InitializeComponent();
    }

    /// <summary>My Items hides the Owned / Not owned filter: everything there is owned.</summary>
    public bool ShowOwnership
    {
        get => (bool)GetValue(ShowOwnershipProperty);
        set => SetValue(ShowOwnershipProperty, value);
    }
}
