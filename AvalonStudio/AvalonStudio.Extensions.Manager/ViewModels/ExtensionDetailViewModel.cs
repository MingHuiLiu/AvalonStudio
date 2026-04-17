using AvalonStudio.Extensions.OpenVSX;
using AvalonStudio.Extensions.Installer;
using ReactiveUI;
using System;
using System.Reactive;
using System.Threading.Tasks;

namespace AvalonStudio.Extensions.Manager.ViewModels
{
    public class ExtensionDetailViewModel : ReactiveObject
    {
        private string _readme;
        private string _changelog;
        private bool _isLoading;
        private ExtensionItemViewModel _extension;

        private readonly ExtensionDownloadManager _downloadManager;
        private readonly ExtensionInstaller _installer;
        private readonly ExtensionRegistry _registry;

        public ExtensionDetailViewModel(
            ExtensionDownloadManager downloadManager,
            ExtensionInstaller installer,
            ExtensionRegistry registry)
        {
            _downloadManager = downloadManager;
            _installer = installer;
            _registry = registry;

            InstallCommand = ReactiveCommand.CreateFromTask(InstallAsync,
                this.WhenAnyValue(x => x.Extension, e => e != null && !e.IsInstalled && !e.IsInstalling));

            UninstallCommand = ReactiveCommand.Create(Uninstall,
                this.WhenAnyValue(x => x.Extension, e => e != null && e.IsInstalled));

            EnableCommand = ReactiveCommand.CreateFromTask(() => SetEnabledAsync(true),
                this.WhenAnyValue(x => x.Extension, e => e != null && e.IsInstalled && !e.IsEnabled));

            DisableCommand = ReactiveCommand.CreateFromTask(() => SetEnabledAsync(false),
                this.WhenAnyValue(x => x.Extension, e => e != null && e.IsInstalled && e.IsEnabled));
        }

        public ExtensionItemViewModel Extension
        {
            get => _extension;
            set
            {
                this.RaiseAndSetIfChanged(ref _extension, value);
                if (value != null)
                    LoadDetailAsync(value).ConfigureAwait(false);
            }
        }

        public string Readme
        {
            get => _readme;
            set => this.RaiseAndSetIfChanged(ref _readme, value);
        }

        public string Changelog
        {
            get => _changelog;
            set => this.RaiseAndSetIfChanged(ref _changelog, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => this.RaiseAndSetIfChanged(ref _isLoading, value);
        }

        public ReactiveCommand<Unit, Unit> InstallCommand { get; }
        public ReactiveCommand<Unit, Unit> UninstallCommand { get; }
        public ReactiveCommand<Unit, Unit> EnableCommand { get; }
        public ReactiveCommand<Unit, Unit> DisableCommand { get; }

        private async Task LoadDetailAsync(ExtensionItemViewModel ext)
        {
            IsLoading = true;
            Readme = string.Empty;
            Changelog = string.Empty;

            try
            {
                var detail = await _downloadManager.GetExtensionMetadataAsync(
                    ext.Namespace, ext.Name).ConfigureAwait(false);

                Readme = detail?.Readme ?? string.Empty;
                Changelog = detail?.Changelog ?? string.Empty;
            }
            catch
            {
                Readme = "(Failed to load README)";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task InstallAsync()
        {
            var ext = Extension;
            if (ext == null) return;

            ext.IsInstalling = true;
            ext.InstallProgress = 0;

            try
            {
                var progress = new Progress<double>(p => ext.InstallProgress = p);
                var vsixPath = await _downloadManager.GetVsixAsync(
                    ext.Namespace, ext.Name, ext.Version, progress).ConfigureAwait(false);

                await _installer.InstallAsync(vsixPath, progress: progress).ConfigureAwait(false);
                await _registry.RefreshAsync().ConfigureAwait(false);

                ext.IsInstalled = true;
                ext.IsEnabled = true;
            }
            catch
            {
                // Installation failed
            }
            finally
            {
                ext.IsInstalling = false;
                ext.InstallProgress = 0;
            }
        }

        private void Uninstall()
        {
            var ext = Extension;
            if (ext == null) return;

            _installer.Uninstall(ext.UniqueId);
            _registry.RefreshAsync().ConfigureAwait(false);
            ext.IsInstalled = false;
        }

        private async Task SetEnabledAsync(bool enabled)
        {
            var ext = Extension;
            if (ext == null) return;

            await _registry.SetEnabledAsync(ext.UniqueId, enabled).ConfigureAwait(false);
            ext.IsEnabled = enabled;
        }
    }
}
