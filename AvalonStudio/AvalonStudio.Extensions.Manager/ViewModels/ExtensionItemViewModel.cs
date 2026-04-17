using AvalonStudio.Extensions.OpenVSX.Models;
using AvalonStudio.Extensions.Installer;
using ReactiveUI;

namespace AvalonStudio.Extensions.Manager.ViewModels
{
    public class ExtensionItemViewModel : ReactiveObject
    {
        private bool _isInstalled;
        private bool _isEnabled;
        private bool _isInstalling;
        private double _installProgress;

        public ExtensionItemViewModel(ExtensionEntry entry)
        {
            Namespace = entry.Namespace;
            Name = entry.Name;
            Version = entry.Version;
            DisplayName = entry.DisplayName ?? entry.Name;
            Description = entry.Description ?? string.Empty;
            AverageRating = entry.AverageRating ?? 0;
            DownloadCount = entry.DownloadCount ?? 0;
            IconUrl = entry.Icon;
        }

        public ExtensionItemViewModel(InstalledExtension installed)
        {
            var m = installed.Manifest;
            Namespace = m.Publisher;
            Name = m.Name;
            Version = m.Version;
            DisplayName = m.DisplayName ?? m.Name;
            Description = m.Description ?? string.Empty;
            IsInstalled = true;
            IsEnabled = installed.IsEnabled;
        }

        public string Namespace { get; }
        public string Name { get; }
        public string Version { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public double AverageRating { get; }
        public long DownloadCount { get; }
        public string IconUrl { get; }

        public string UniqueId => $"{Namespace}.{Name}";

        public bool IsInstalled
        {
            get => _isInstalled;
            set => this.RaiseAndSetIfChanged(ref _isInstalled, value);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
        }

        public bool IsInstalling
        {
            get => _isInstalling;
            set => this.RaiseAndSetIfChanged(ref _isInstalling, value);
        }

        public double InstallProgress
        {
            get => _installProgress;
            set => this.RaiseAndSetIfChanged(ref _installProgress, value);
        }
    }
}
