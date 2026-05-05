using FirebirdSql.Data.FirebirdClient;
using Microsoft.Data.SqlClient;
using SyncAgent_Core;
using System.Windows.Forms;

namespace SyncAgent_Setup;

public partial class Form1 : Form
{
    private TextBox txtTenantId;
    private TextBox txtIntegrationKey;
    private TextBox txtErpCode;
    private TextBox txtDbHost;
    private TextBox txtDbPort;
    private TextBox txtDbName;
    private TextBox txtDbUser;
    private TextBox txtDbPassword;
    private NumericUpDown numSyncInterval;
    private Button btnSalvar;

    public Form1()
    {
        InitializeComponent();
        InitializeCustomComponents();
    }

    private void InitializeCustomComponents()
    {
        this.Text = "Farma Conecta - Setup Agente";
        this.Size = new System.Drawing.Size(400, 500);

        int startY = 20;
        int spacing = 40;

        txtTenantId = AddLabelAndTextBox("Tenant ID:", startY);
        txtIntegrationKey = AddLabelAndTextBox("Integration Key:", startY += spacing);
        txtErpCode = AddLabelAndTextBox("Código ERP:", startY += spacing);
        txtDbHost = AddLabelAndTextBox("DB Host:", startY += spacing);
        txtDbPort = AddLabelAndTextBox("DB Port:", startY += spacing);
        txtDbName = AddLabelAndTextBox("DB Name/Path:", startY += spacing);
        txtDbUser = AddLabelAndTextBox("DB User:", startY += spacing);
        txtDbPassword = AddLabelAndTextBox("DB Password:", startY += spacing, true);

        Label lblInterval = new Label { Text = "Intervalo Sync (min):", Location = new System.Drawing.Point(20, startY += spacing), AutoSize = true };
        numSyncInterval = new NumericUpDown { Location = new System.Drawing.Point(150, startY), Minimum = 1, Maximum = 1440, Value = 1 };
        this.Controls.Add(lblInterval);
        this.Controls.Add(numSyncInterval);

        btnSalvar = new Button { Text = "Salvar e Testar", Location = new System.Drawing.Point(150, startY += spacing + 20), Width = 150 };
        btnSalvar.Click += BtnSalvar_Click;
        this.Controls.Add(btnSalvar);

        LoadExistingConfig();
    }

    private TextBox AddLabelAndTextBox(string labelText, int yPos, bool isPassword = false)
    {
        Label lbl = new Label { Text = labelText, Location = new System.Drawing.Point(20, yPos), AutoSize = true };
        TextBox txt = new TextBox { Location = new System.Drawing.Point(150, yPos), Width = 200, UseSystemPasswordChar = isPassword };
        this.Controls.Add(lbl);
        this.Controls.Add(txt);
        return txt;
    }

    private void LoadExistingConfig()
    {
        var config = ConfigManager.LoadConfig();
        if (config != null)
        {
            txtTenantId.Text = config.TenantId;
            txtIntegrationKey.Text = config.IntegrationKey;
            txtErpCode.Text = config.ErpCode;
            txtDbHost.Text = config.DbHost;
            txtDbPort.Text = config.DbPort;
            txtDbName.Text = config.DbName;
            txtDbUser.Text = config.DbUser;
            txtDbPassword.Text = config.DbPassword;
            numSyncInterval.Value = config.SyncIntervalMinutes;
        }
    }

    private void BtnSalvar_Click(object? sender, EventArgs e)
    {
        try
        {
            var config = new AgentConfig
            {
                TenantId = txtTenantId.Text,
                IntegrationKey = txtIntegrationKey.Text,
                ErpCode = txtErpCode.Text,
                DbHost = txtDbHost.Text,
                DbPort = txtDbPort.Text,
                DbName = txtDbName.Text,
                DbUser = txtDbUser.Text,
                DbPassword = txtDbPassword.Text,
                SyncIntervalMinutes = (int)numSyncInterval.Value
            };

            if (!TestDatabaseConnection(config))
            {
                return;
            }

            ConfigManager.SaveConfig(config);
            MessageBox.Show("Conexão bem-sucedida! Configuração salva com sucesso.", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool TestDatabaseConnection(AgentConfig config)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(config.DbHost) || string.IsNullOrWhiteSpace(config.DbName))
            {
                 MessageBox.Show("Por favor, preencha as credenciais do banco.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                 return false;
            }

            if (config.DbPort == "3050" || config.DbPort.StartsWith("305")) // Default firebird port assumption
            {
                var fbConnectionStr = $"User={config.DbUser};Password={config.DbPassword};Database={config.DbName};DataSource={config.DbHost};Port={config.DbPort};Dialect=3;Charset=NONE;Connection timeout=5";
                using var fbConn = new FbConnection(fbConnectionStr);
                fbConn.Open();
                return true;
            }
            else
            {
                var sqlConnectionStr = $"Server={config.DbHost},{config.DbPort};Database={config.DbName};User Id={config.DbUser};Password={config.DbPassword};TrustServerCertificate=True;Connection Timeout=5";
                using var sqlConn = new SqlConnection(sqlConnectionStr);
                sqlConn.Open();
                return true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Falha ao conectar no banco de dados: {ex.Message}", "Erro de Conexão", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }
}
