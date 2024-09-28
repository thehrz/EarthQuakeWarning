using EarthquakeWaring.App.Infrastructure.Models.SettingModels;
using EarthquakeWaring.App.Infrastructure.Models.ViewModels;
using EarthquakeWaring.App.Infrastructure.ServiceAbstraction;
using EarthquakeWaring.App.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Windows;
using Wpf.Ui.Mvvm.Interfaces;

namespace EarthquakeWaring.App.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IServiceProvider _services;

        public MainWindow(IServiceProvider services)
        {
            _services = services;
            InitializeComponent();

            this.DataContext = new MainWindowViewModel(_services.GetService<ISetting<UpdaterSetting>>());
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            App.RootFrame = RootFrame;
            App.MainWindowOpened = true;
            RootFrame.Navigate(_services.GetService<WelcomePage>());
        }

        private void OnNavigatingWelcome(object sender, RoutedEventArgs e)
        {
            RootFrame.Navigate(_services.GetService<WelcomePage>());
        }

        private void OnNavigatingSettings(object sender, RoutedEventArgs e)
        {
            RootFrame.Navigate(_services.GetService<SettingsPage>());
        }

        private void OnNavigatingEarthQuakesList(object sender, RoutedEventArgs e)
        {
            RootFrame.Navigate(_services.GetService<EarthQuakesListPage>());
        }

        private void MainWindow_OnClosed(object? sender, EventArgs e)
        {
            App.MainWindowOpened = false;
            App.RootFrame = null;
        }
    }
}