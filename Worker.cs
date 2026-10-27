using WebSocketSharp.Server;

namespace Axona_WebSocket_Service
{
    public class Worker(ILogger<Worker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var wssv = new WebSocketServer("ws://localhost:4649");
            // To change the logging level.
            wssv.Log.Level = WebSocketSharp.LogLevel.Trace;
            wssv.AddWebSocketService<KL1v>("/KL1v");
            wssv.Start();

            if (wssv.IsListening)
            {
                var fmt = "Listening on port {0}, and providing WebSocket services:";

                logger.LogInformation(fmt, wssv.Port);

                foreach (var path in wssv.WebSocketServices.Paths)
                    logger.LogInformation("- {0}", path);
            }
            
            while (!stoppingToken.IsCancellationRequested)
            {
                if (logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
                {
//                    logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                }

                await Task.Delay(1000, stoppingToken);


                // Stop the server.
                if(stoppingToken.IsCancellationRequested)
                {
                    logger.LogInformation(stoppingToken.IsCancellationRequested ? "Cancellation requested." : "No cancellation requested.");
                    wssv.Stop();
                }
            }
        }
    }
}
