using CommunityToolkit.Maui.Markup;
using InvoiceAI.Core.ViewModels;

namespace InvoiceAI.App.Pages;

public class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel)
    {
        BindingContext = viewModel;
        Content = new VerticalStackLayout
        {
            Children =
            {
                new Label { Text = "InvoiceAI" }
                    .CenterHorizontal()
            }
        };
    }
}
