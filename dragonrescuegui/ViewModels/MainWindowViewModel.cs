using ReactiveUI;

namespace dragonrescuegui.ViewModels {
    public class MainWindowViewModel : ViewModelBase {
        private ViewModelBase _contentViewModel;
        private double _width = 300;
        private double _height = 100;
        public ViewModelBase ContentViewModel {
            get => _contentViewModel;
            set => this.RaiseAndSetIfChanged(ref _contentViewModel, value);
        }
        public double Width {
            get => _width;
            set => this.RaiseAndSetIfChanged(ref _width, value);
        }

        public double Height {
            get => _height;
            set => this.RaiseAndSetIfChanged(ref _height, value);
        }


        public MainWindowViewModel() {
            ContentViewModel = new ImportExportMenuViewModel(this);
        }
    }
}
