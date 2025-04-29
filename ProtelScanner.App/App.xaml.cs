using Microsoft.Maui.Controls;

namespace ProtelScanner.App
{
  public partial class App : Application
  {
    public App()
    {
      InitializeComponent();

      // wrap our MainPage in a NavigationPage
      MainPage = new NavigationPage(new MainPage());
    }
  }
}
