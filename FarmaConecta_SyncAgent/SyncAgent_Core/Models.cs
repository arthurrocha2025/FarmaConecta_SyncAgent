using System.Text.Json.Serialization;

namespace SyncAgent_Core;

public class AgentConfig
{
    public string TenantId { get; set; } = string.Empty;
    public string IntegrationKey { get; set; } = string.Empty;
    public string ErpCode { get; set; } = string.Empty;
    public string DbHost { get; set; } = string.Empty;
    public string DbPort { get; set; } = string.Empty;
    public string DbName { get; set; } = string.Empty;
    public string DbUser { get; set; } = string.Empty;
    public string DbPassword { get; set; } = string.Empty;
    public int SyncIntervalMinutes { get; set; } = 1;
}

public class ProdutoDelta
{
    [JsonPropertyName("cod_interno")]
    public string CodInterno { get; set; } = string.Empty;
    [JsonPropertyName("cod_loja")]
    public string CodLoja { get; set; } = string.Empty;
    [JsonPropertyName("estoque")]
    public decimal Estoque { get; set; }
    [JsonPropertyName("valor_venda")]
    public decimal ValorVenda { get; set; }
    [JsonPropertyName("ean")]
    public string Ean { get; set; } = string.Empty;
    [JsonPropertyName("promocao")]
    public Promocao? Promocao { get; set; }
}

public class Promocao
{
    [JsonPropertyName("nome")]
    public string Nome { get; set; } = string.Empty;
    [JsonPropertyName("preco")]
    public decimal Preco { get; set; }
}

public class TemplateReceita
{
    [JsonPropertyName("versao")]
    public string Versao { get; set; } = string.Empty;
    [JsonPropertyName("banco_driver")]
    public string BancoDriver { get; set; } = string.Empty;
    [JsonPropertyName("query_extracao")]
    public string QueryExtracao { get; set; } = string.Empty;
    [JsonPropertyName("mapeamento")]
    public Dictionary<string, string> Mapeamento { get; set; } = new();
}

public class SyncPayload
{
    [JsonPropertyName("timestamp_geracao")]
    public string TimestampGeracao { get; set; } = string.Empty;
    [JsonPropertyName("produtos_alterados")]
    public List<ProdutoDelta> ProdutosAlterados { get; set; } = new();
}
