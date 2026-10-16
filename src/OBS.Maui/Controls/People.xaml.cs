namespace OBS.Maui.Controls;

public partial class People : ContentView
{
    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(
            nameof(ItemsSource),
            typeof(IEnumerable<PersonRow>),
            typeof(People),
            defaultValue: null);

    public IEnumerable<PersonRow>? ItemsSource
    {
        get => (IEnumerable<PersonRow>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }
    public People()
	{
		InitializeComponent();
	}

    private async void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is PersonRow person && Window?.Page is Page page)
        {
            await page.DisplayAlert("Selected person", person.Name, "OK");
        }
    }
}
