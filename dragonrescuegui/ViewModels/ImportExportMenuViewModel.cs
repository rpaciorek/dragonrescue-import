using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dragonrescuegui.ViewModels {
    public class ImportExportMenuViewModel : ViewModelBase {
        public readonly MainWindowViewModel _mainWindowViewModel;
        public ImportExportMenuViewModel(MainWindowViewModel mainWindow) {
            _mainWindowViewModel = mainWindow;
        }

        public void Export() {
            _mainWindowViewModel.ContentViewModel = new ExportImportViewModel(_mainWindowViewModel, Models.Mode.Export);
        }

        public void Import() {
            _mainWindowViewModel.ContentViewModel = new ExportImportViewModel(_mainWindowViewModel, Models.Mode.Import);
        }
    }
}
