using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Nodes;
using LPS.Infrastructure.Distributed;
using LPS.UI.Common;
using LPS.UI.Common.Options;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace LPS.UI.Core.Host
{

    internal class DashboardService : IDashboardService
    {
        readonly ILogger _logger;
        readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        readonly IFinalizationDisplay _finalizationDisplay;
        readonly ClusterRunSettings? _runSettings;

        IOptions<DashboardConfigurationOptions> _dashboardConfig;
        IClusterConfiguration _clusterConfiguration;
        public DashboardService(
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IOptions<DashboardConfigurationOptions> dashboardConfig,
            IClusterConfiguration clusterConfiguration,
            IFinalizationDisplay finalizationDisplay,
            ClusterRunSettings? runSettings = null)
        {
            _dashboardConfig = dashboardConfig;
            _clusterConfiguration = clusterConfiguration;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _finalizationDisplay = finalizationDisplay;
            _runSettings = runSettings;
        }
        public string Url => $"http://{(_runSettings != null ? "127.0.0.1" : _clusterConfiguration?.MasterNodeIP ?? "127.0.0.1")}:{_dashboardConfig.Value.Port ?? GlobalSettings.DefaultDashboardPort}";

        public void Start()
        {
            if (_runSettings?.Role == NodeType.Master) Console.WriteLine($"Dashboard: {Url}");
            if (_dashboardConfig.Value.BuiltInDashboard.HasValue && _dashboardConfig.Value.BuiltInDashboard.Value)
            {
                OpenBrowser(Url);
            }
        }
        public async Task EnsureDashboardUpdateBeforeExitAsync()
        {
            var refreshInterval = Math.Max(_dashboardConfig.Value.RefreshRate ?? 5, 5) * 2;
            var finalizationDuration = TimeSpan.FromSeconds(refreshInterval);
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Test complete. Finalizing results...", LPSLoggingLevel.Information);
            _finalizationDisplay.Start(finalizationDuration);
            await Task.Delay(finalizationDuration);
        }

        internal static void OpenBrowser(string url)
        {
            try
            {
                // Use platform-specific code to open the default web browser
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw new PlatformNotSupportedException("Unsupported platform.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening web browser: {ex.Message}");
            }
        }
    }
}
