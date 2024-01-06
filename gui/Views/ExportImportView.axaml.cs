using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using dragonrescuegui.ViewModels;
using System;

namespace dragonrescuegui.Views;

public partial class ExportImportView : UserControl
{
    public ExportImportView()
    {
        InitializeComponent();
        this.DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, EventArgs e) {
        var viewModel = (ExportImportViewModel)DataContext;
        viewModel.ScrollRequested += OnScrollRequested;
    }

    private void OnScrollRequested(object sender, EventArgs e) {
        scrollViewer.ScrollToEnd();
    }
}