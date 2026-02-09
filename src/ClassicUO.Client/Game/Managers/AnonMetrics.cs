using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ClassicUO;
using ClassicUO.Configuration;

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
    public static async void TrackLoginFireAndForget(string serverName) 
    {
        if (!MetricsEnabled)
            return;

        await Task.Factory.StartNew(() =>
        {          
            try
            {
                using (var webClient = new WebClient())
                {                
                    string tazUOVersion = CUOEnviroment.Version.ToString();
                    string clientVersion = Settings.GlobalSettings.ClientVersion;

                    // Manually build the JSON string.
                    // We use \" to escape the double quotes required by the JSON spec.
                    string json = "{" +
                        $"\"serverName\":\"{serverName}\"," +
                        $"\"tazUOVersion\":\"{tazUOVersion}\"," +
                        $"\"clientVersion\":\"{clientVersion}\"" +
                    "}";

                    Console.WriteLine($"Sending metrics: {json}");

                    // Set headers for JSON content
                    webClient.Headers[HttpRequestHeader.ContentType] = "application/json";
                    webClient.Encoding = Encoding.UTF8;

                    // Fire and forget - we don't wait for the response
                    webClient.UploadString("http://metrics.tazuo.org:5000/api/metrics/login", json);
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
                // Silently fail
            }
        });
    }
}