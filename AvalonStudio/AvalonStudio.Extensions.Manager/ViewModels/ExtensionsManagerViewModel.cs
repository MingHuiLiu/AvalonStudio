using AvalonStudio.Extensions.OpenVSX;
using AvalonStudio.Extensions.Installer;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace AvalonStudio.Extensions.Manager.ViewModels
{
    public class ExtensionsManagerViewModel : ReactiveObject
    {
        private readonly OpenVsxClient _openVsxClient;
        private readonly ExtensionDownloadManager _downloadManager;
        private readonly ExtensionInstaller _installer;
        private readonly ExtensionRegistry _registry;

        private string _searchText = string.Empty;
        private string _selectedCategory = string.Empty;
        private bool _isSearching;
        private int _currentOffset;
        private const int PageSize = 20;
        private ExtensionItemViewModel _selectedExtension;

        public ExtensionsManagerViewModel()
        {
            _openVsxClient = new OpenVsxClient();
            _downloadManager = new ExtensionDownloadManager(client: _openVsxClient);
            _installer = new ExtensionInstaller();
            _registry = new ExtensionRegistry();

            SearchResults = new ObservableCollection<ExtensionItemViewModel>();
            InstalledExtensions = new ObservableCollection<ExtensionItemViewModel>();

            DetailViewModel = new ExtensionDetailViewModel(_downloadManager, _installer, _registry);

            SearchCommand = ReactiveCommand.CreateFromTask(
                () => SearchAsync(reset: true),
                this.WhenAnyValue(x => x.IsSearching, s => !s));

            LoadMoreCommand = ReactiveCommand.CreateFromTask(
                () => SearchAsync(reset: false),
                this.WhenAnyValue(x => x.IsSearching, s => !s));

            RefreshInstalledCommand = ReactiveCommand.CreateFromTask(RefreshInstalledAsync);

            // Debounce search text changes
            this.WhenAnyValue(x => x.SearchText)
                .Throttle(TimeSpan.FromMilliseconds(400))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => SearchCommand.Execute().Subscribe());
        }

        public ObservableCollection<ExtensionItemViewModel> SearchResults { get; }
        public ObservableCollection<ExtensionItemViewModel> InstalledExtensions { get; }
        public ExtensionDetailViewModel DetailViewModel { get; }

        public string SearchText
        {
            get => _searchText;
            set => this.RaiseAndSetIfChanged(ref _searchText, value);
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set => this.RaiseAndSetIfChanged(ref _selectedCategory, value);
        }

        public bool IsSearching
        {
            get => _isSearching;
            set => this.RaiseAndSetIfChanged(ref _isSearching, value);
        }

        public ExtensionItemViewModel SelectedExtension
        {
            get => _selectedExtension;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedExtension, value);
                DetailViewModel.Extension = value;
            }
        }

        public ReactiveCommand<Unit, Unit> SearchCommand { get; }
        public ReactiveCommand<Unit, Unit> LoadMoreCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshInstalledCommand { get; }

        private async Task SearchAsync(bool reset)
        {
            if (reset)
            {
                _currentOffset = 0;
                SearchResults.Clear();
            }

            IsSearching = true;

            try
            {
                var result = await _openVsxClient.SearchAsync(
                    query: SearchText,
                    category: SelectedCategory,
                    offset: _currentOffset,
                    size: PageSize).ConfigureAwait(false);

                if (result?.Extensions != null)
                {
                    foreach (var ext in result.Extensions)
                    {
                        var vm = new ExtensionItemViewModel(ext);
                        vm.IsInstalled = _registry.IsInstalled(vm.UniqueId);
                        SearchResults.Add(vm);
                    }

                    _currentOffset += result.Extensions.Count;
                }
            }
            catch
            {
                // Search failed
            }
            finally
            {
                IsSearching = false;
            }
        }

        private async Task RefreshInstalledAsync()
        {
            await _registry.RefreshAsync().ConfigureAwait(false);

            InstalledExtensions.Clear();

            foreach (var ext in _registry.InstalledExtensions)
            {
                InstalledExtensions.Add(new ExtensionItemViewModel(ext));
            }
        }
    }
}
