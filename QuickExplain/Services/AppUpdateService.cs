using System.Threading;
using Updatum;

namespace QuickExplain.Services
{
    internal sealed class AppUpdateService
    {
        public static AppUpdateService Instance { get; } = new();

        private static readonly TimeSpan CheckCacheDuration = TimeSpan.FromHours(6);

        private readonly SemaphoreSlim _operationLock = new(1, 1);
        private readonly UpdatumManager _updater;
        private DateTimeOffset? _lastCheckedAt;
        private bool _lastCheckResult;

        private AppUpdateService()
        {
            _updater = new UpdatumManager("Rinqer0203", "QuickExplain")
            {
                AllowPreReleases = false,
                FetchOnlyLatestRelease = true,
                AssetExtensionFilter = "exe",
                AssetRegexPattern = @"^QuickExplain-\d+\.\d+\.\d+(?:[-.][0-9A-Za-z.-]+)?-win-x86\.exe$",
                InstallUpdateWindowsExeType = UpdatumWindowsExeType.SingleFileApp,
                InstallUpdateSingleFileExecutableNameStrategy = UpdatumSingleFileExecutableNameStrategy.EntryApplicationName
            };
        }

        public bool IsUpdateAvailable => _updater.IsUpdateAvailable;

        public string CurrentVersion => FormatVersion(_updater.CurrentVersion);

        public string? LatestVersion => _updater.LatestReleaseTagVersionStr;

        public event Action<bool>? UpdateAvailabilityChanged;

        public static bool CanUseUpdater =>
            OperatingSystem.IsWindows();

        public async Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            if (!CanUseUpdater)
                return false;

            await _operationLock.WaitAsync(cancellationToken);
            try
            {
                if (_lastCheckedAt is DateTimeOffset lastCheckedAt
                    && DateTimeOffset.UtcNow - lastCheckedAt < CheckCacheDuration)
                {
                    return _lastCheckResult;
                }

                var previousResult = _lastCheckResult;
                _lastCheckResult = await _updater.CheckForUpdatesAsync();
                _lastCheckedAt = DateTimeOffset.UtcNow;
                if (_lastCheckResult != previousResult)
                {
                    UpdateAvailabilityChanged?.Invoke(_lastCheckResult);
                }

                return _lastCheckResult;
            }
            catch
            {
                return false;
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public async Task DownloadAndInstallUpdateAsync(CancellationToken cancellationToken = default)
        {
            if (!CanUseUpdater)
                return;

            await _operationLock.WaitAsync(cancellationToken);
            try
            {
                if (!_updater.IsUpdateAvailable)
                {
                    var updateFound = await _updater.CheckForUpdatesAsync();
                    if (!updateFound)
                        return;
                }

                await _updater.DownloadAndInstallUpdateAsync(cancellationToken);
            }
            finally
            {
                _operationLock.Release();
            }
        }

        private static string FormatVersion(Version? version)
        {
            if (version == null)
                return "不明";

            var fieldCount = version.Build >= 0 ? 3 : 2;
            if (version.Revision >= 0)
                fieldCount = 4;

            return version.ToString(fieldCount);
        }
    }
}
