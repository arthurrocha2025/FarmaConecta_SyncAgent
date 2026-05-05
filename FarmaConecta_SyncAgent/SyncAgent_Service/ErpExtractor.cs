using Dapper;
using Microsoft.Data.SqlClient;
using FirebirdSql.Data.FirebirdClient;
using SyncAgent_Core;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SyncAgent_Service;

public class ErpExtractor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ErpExtractor> _logger;

    public ErpExtractor(HttpClient httpClient, ILogger<ErpExtractor> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<TemplateReceita?> FetchTemplateAsync(string erpCode)
    {
        try
        {
            var url = $"https://api.farmaconecta.com.br/api/v1/integracao/templates/{erpCode}/";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<TemplateReceita>(content);
            }
            else
            {
                _logger.LogWarning("Failed to fetch template for ERP code {ErpCode}. Status Code: {StatusCode}", erpCode, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching template for ERP code {ErpCode}", erpCode);
        }

        return null;
    }

    public async Task<List<ProdutoHash>> ExtractDataAndCalculateHashesAsync(AgentConfig config, TemplateReceita template)
    {
        var hashes = new List<ProdutoHash>();
        string connectionString = BuildConnectionString(config, template.BancoDriver);

        try
        {
            using DbConnection connection = template.BancoDriver.Equals("Firebird", StringComparison.OrdinalIgnoreCase)
                ? new FbConnection(connectionString)
                : new SqlConnection(connectionString);

            await connection.OpenAsync();
            using var reader = await connection.ExecuteReaderAsync(template.QueryExtracao);

            while (await reader.ReadAsync())
            {
                var rowData = new StringBuilder();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    rowData.Append(reader.GetValue(i)?.ToString() ?? string.Empty);
                    if (i < reader.FieldCount - 1)
                    {
                        rowData.Append('|');
                    }
                }

                // Assuming the first column is the ID based on the query structure,
                // but ideally we should use the mapping to find the ID column index.
                // For robustness, we will try to find the ID from the mapping.
                string idColumnName = template.Mapeamento.FirstOrDefault(m => m.Key == "id_interno").Value;
                string idInterno = string.Empty;

                if (!string.IsNullOrEmpty(idColumnName))
                {
                    int idOrdinal = reader.GetOrdinal(idColumnName);
                    idInterno = reader.GetValue(idOrdinal)?.ToString() ?? string.Empty;
                }
                else
                {
                    // Fallback if mapping is missing
                    idInterno = reader.GetValue(0)?.ToString() ?? string.Empty;
                }


                string hash = CalculateMd5(rowData.ToString());

                hashes.Add(new ProdutoHash
                {
                    IdInterno = idInterno,
                    HashAssinatura = hash
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing extraction query.");
        }

        return hashes;
    }

    public async Task<List<ProdutoDelta>> ExtractDataAndCreateDeltasAsync(AgentConfig config, TemplateReceita template, List<ProdutoHash> deltasToExtract)
    {
        var deltas = new List<ProdutoDelta>();
        if (deltasToExtract.Count == 0) return deltas;

        string connectionString = BuildConnectionString(config, template.BancoDriver);

        try
        {
            using DbConnection connection = template.BancoDriver.Equals("Firebird", StringComparison.OrdinalIgnoreCase)
                ? new FbConnection(connectionString)
                : new SqlConnection(connectionString);

            await connection.OpenAsync();
            using var reader = await connection.ExecuteReaderAsync(template.QueryExtracao);

            var idColumnName = template.Mapeamento.FirstOrDefault(m => m.Key == "id_interno").Value;
            var eanColumnName = template.Mapeamento.FirstOrDefault(m => m.Key == "ean").Value;
            var estoqueColumnName = template.Mapeamento.FirstOrDefault(m => m.Key == "estoque").Value;
            var valorVendaColumnName = template.Mapeamento.FirstOrDefault(m => m.Key == "valor_venda").Value;
            var precoPromocionalColumnName = template.Mapeamento.FirstOrDefault(m => m.Key == "preco_promocional").Value;
            var nomePromocaoColumnName = template.Mapeamento.FirstOrDefault(m => m.Key == "nome_promocao").Value;

            while (await reader.ReadAsync())
            {
                string idInterno = reader[idColumnName]?.ToString() ?? string.Empty;

                if (deltasToExtract.Any(d => d.IdInterno == idInterno))
                {
                     var delta = new ProdutoDelta
                     {
                         CodInterno = idInterno,
                         CodLoja = "01", // Or get from config if available
                         Ean = eanColumnName != null && !Convert.IsDBNull(reader[eanColumnName]) ? reader[eanColumnName]?.ToString() ?? string.Empty : string.Empty,
                         Estoque = estoqueColumnName != null && !Convert.IsDBNull(reader[estoqueColumnName]) ? Convert.ToDecimal(reader[estoqueColumnName]) : 0,
                         ValorVenda = valorVendaColumnName != null && !Convert.IsDBNull(reader[valorVendaColumnName]) ? Convert.ToDecimal(reader[valorVendaColumnName]) : 0,
                     };

                     if (precoPromocionalColumnName != null && nomePromocaoColumnName != null &&
                         !Convert.IsDBNull(reader[precoPromocionalColumnName]) && !Convert.IsDBNull(reader[nomePromocaoColumnName]))
                     {
                         delta.Promocao = new Promocao
                         {
                             Nome = reader[nomePromocaoColumnName]?.ToString() ?? string.Empty,
                             Preco = Convert.ToDecimal(reader[precoPromocionalColumnName])
                         };
                     }

                     deltas.Add(delta);
                }
            }
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Error creating delta payload objects.");
        }

        return deltas;
    }

    private string BuildConnectionString(AgentConfig config, string driver)
    {
        if (driver.Equals("Firebird", StringComparison.OrdinalIgnoreCase))
        {
            return $"User={config.DbUser};Password={config.DbPassword};Database={config.DbName};DataSource={config.DbHost};Port={config.DbPort};Dialect=3;Charset=NONE;";
        }
        else // Assume SQL Server
        {
            return $"Server={config.DbHost},{config.DbPort};Database={config.DbName};User Id={config.DbUser};Password={config.DbPassword};TrustServerCertificate=True;";
        }
    }

    private string CalculateMd5(string input)
    {
        using var md5 = MD5.Create();
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = md5.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
