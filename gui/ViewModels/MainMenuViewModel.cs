using Avalonia.Input.Raw;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dragonrescuegui.ViewModels {
    public class MainMenuViewModel : ViewModelBase {
        private MainWindowViewModel _mainWindowViewModel;
        public MainMenuViewModel(MainWindowViewModel mainWindow) {
            _mainWindowViewModel = mainWindow;
            mainWindow.Width = 300;
            mainWindow.Height = 100;
        }

        public void ManualMode() {
            _mainWindowViewModel.ContentViewModel = new ImportExportMenuViewModel(_mainWindowViewModel);
        }
    }
}
