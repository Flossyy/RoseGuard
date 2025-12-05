using Microsoft.Extensions.DependencyInjection;
using RoseGuard.Pages;

namespace RoseGuard;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		MainPage = new AppShell();
	}
}