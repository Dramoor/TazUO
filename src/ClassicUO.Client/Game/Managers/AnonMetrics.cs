using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ClassicUO;
using ClassicUO.Configuration;
using ClassicUO.Utility.Logging;

public class AnonMetrics
{
    /// <summary>
    /// Controls whether anonymous metrics are collected. Defaults to true.
    /// Can be set to false using the -nometrics command-line argument.
    /// </summary>
    public static bool MetricsEnabled { get; set; } = true;

    /// <summary>
    /// Track a login metric using fire-and-forget approach.
    /// Does not wait for server response - recommended for production use.
    /// This approach won't block your login process if the metrics server is slow or unavailable.
    /// </summary>
    /// <param name="serverName">The name of the server (e.g., "Atlantic", "Pacific")</param>
    public static void TrackLoginFireAndForget(string serverName) => _ = Task.Run(async () =>
        {
            if (!MetricsEnabled)
                return;
            using var _httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://metrics.tazuo.org:5000"),
                Timeout = TimeSpan.FromSeconds(5) // Short timeout to avoid hanging
            };
            try
            {
                string tazUOVersion = CUOEnviroment.Version.ToString();
                string clientVersion = Settings.GlobalSettings.ClientVersion;
                var request = new { serverName, tazUOVersion, clientVersion };
                Log.Info($"Sending metrics: {request}");
                await _httpClient.PostAsJsonAsync("/api/metrics/login", request);
            }
            catch
            {
                // Silently fail - metrics shouldn't break the app
                // For production, you might want to log this to your application's logging system
            }
        });
}