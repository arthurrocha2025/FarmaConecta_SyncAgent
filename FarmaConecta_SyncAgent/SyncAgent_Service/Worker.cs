using SyncAgent_Core;

namespace SyncAgent_Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly StateManager _stateManager;
    private readonly ErpExtractor _erpExtractor;
    private readonly SyncApiClient _syncApiClient;

    public Worker(ILogger<Worker> logger, StateManager stateManager, ErpExtractor erpExtractor, SyncApiClient syncApiClient)
    {
        _logger = logger;
        _stateManager = stateManager;
        _erpExtractor = erpExtractor;
        _syncApiClient = syncApiClient;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing State Manager...");
        await _stateManager.InitializeAsync();
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var config = ConfigManager.LoadConfig();
                if (config == null)
                {
                    _logger.LogWarning("Config file not found or invalid. Make sure to run the Setup app first.");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                _logger.LogInformation("Starting sync cycle for ERP {ErpCode}...", config.ErpCode);

                var template = await _erpExtractor.FetchTemplateAsync(config.ErpCode);
                if (template != null)
                {
                    var hashes = await _erpExtractor.ExtractDataAndCalculateHashesAsync(config, template);
                    var deltasToExtract = new List<ProdutoHash>();

                    foreach (var hashObj in hashes)
                    {
                        var localHash = await _stateManager.GetHashAsync(hashObj.IdInterno);
                        if (localHash != hashObj.HashAssinatura)
                        {
                            deltasToExtract.Add(hashObj);
                        }
                    }

                    if (deltasToExtract.Count > 0)
                    {
                        _logger.LogInformation("Found {DeltaCount} deltas. Extracting full data...", deltasToExtract.Count);
                        var payloadData = await _erpExtractor.ExtractDataAndCreateDeltasAsync(config, template, deltasToExtract);

                        var payload = new SyncPayload
                        {
                            TimestampGeracao = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                            ProdutosAlterados = payloadData
                        };

                        bool success = await _syncApiClient.SendDeltaAsync(config, payload);

                        if (success)
                        {
                            _logger.LogInformation("Payload sent successfully. Committing hashes...");
                            await _stateManager.UpdateHashesAsync(deltasToExtract);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("No deltas found in this cycle.");
                    }
                }

                await Task.Delay(TimeSpan.FromMinutes(config.SyncIntervalMinutes), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during the sync cycle.");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Fallback delay on error
            }
        }
    }
}
