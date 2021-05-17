﻿using FluentStore.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace FluentStore.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SearchResultsView : Page
    {
        public SearchResultsView()
        {
            this.InitializeComponent();
        }
        public SearchResultsViewModel ViewModel
        {
            get => (SearchResultsViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(ViewModel), typeof(SearchResultsViewModel), typeof(SearchResultsView), new PropertyMetadata(new SearchResultsViewModel()));

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            switch (e.Parameter)
            {
                case string query:
                    ViewModel.Query = query;
                    break;
            }
        }
    }
}
