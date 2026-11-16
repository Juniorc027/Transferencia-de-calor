using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using MySqlConnector;
using TransferenciaCalor.Persistencia;

namespace TransferenciaCalor.UI
{
    /// <summary>
    /// "Linha do Tempo": mostra a grade GRAVADA andando tick a tick, com play/pause.
    /// Tela isolada e SOMENTE LEITURA: não usa motor, não recalcula nada e não altera o banco.
    /// Se ela falhar (banco fora do ar, execução vazia), só esta janela mostra o erro;
    /// o resto do programa continua igual.
    /// </summary>
    public sealed class FormLinhaDoTempo : Form
    {
        private const string NomeBanco = "simulador_calor";
        private const int IntervaloPlayMs = 400;

        // Mesma escala de cor do MainForm (CalcularCorPorTemperatura), copiada aqui para esta tela
        // não depender de nada privado do formulário principal.
        private const double TemperaturaMinimaEscala = 250.0; // K
        private const double TemperaturaFocoMaxima = 400.0;   // K
        private const double TemperaturaMaximaEscala = 1000.0; // K
        private const double FracaoPaletaFoco = 0.75;
        private static readonly Color CorVerdeFrio = Color.FromArgb(0, 100, 0);
        private static readonly Color CorVerde = Color.FromArgb(0, 200, 0);
        private static readonly Color CorAmarelo = Color.FromArgb(255, 230, 0);
        private static readonly Color CorLaranja = Color.FromArgb(255, 90, 0);
        private static readonly Color CorVermelho = Color.FromArgb(200, 0, 0);

        private readonly ComboBox cmbExecucoes = new ComboBox();
        private readonly Button btnPlay = new Button();
        private readonly TrackBar trkTicks = new TrackBar();
        private readonly Label lblTick = new Label();
        private readonly Label lblStatus = new Label();
        private readonly PainelDuplo pnlGrade = new PainelDuplo();
        private readonly Timer timerPlay = new Timer();

        // Dados carregados da execução escolhida (tudo em memória, lido uma vez do banco)
        private List<long> ticks;                            // ticks distintos, em ordem
        private Dictionary<long, double[,]> temperaturas;    // tick -> matriz [linha, coluna]
        private int tamanho;
        private int versaoCarga;                             // descarta carga antiga se o usuário trocou de execução

        // Panel com double buffering: evita o "piscar" ao redesenhar a cada passo do play
        private sealed class PainelDuplo : Panel
        {
            public PainelDuplo()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
            }
        }

        public FormLinhaDoTempo()
        {
            Text = "Linha do Tempo (dados gravados)";
            ClientSize = new Size(560, 640);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(420, 480);

            cmbExecucoes.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbExecucoes.Location = new Point(10, 10);
            cmbExecucoes.Size = new Size(440, 21);
            cmbExecucoes.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cmbExecucoes.SelectedIndexChanged += cmbExecucoes_SelectedIndexChanged;

            btnPlay.Text = "Play";
            btnPlay.Location = new Point(460, 8);
            btnPlay.Size = new Size(90, 25);
            btnPlay.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnPlay.Enabled = false;
            btnPlay.Click += btnPlay_Click;

            lblStatus.Location = new Point(10, 38);
            lblStatus.Size = new Size(540, 16);
            lblStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblStatus.AutoEllipsis = true;
            lblStatus.Text = "Carregando lista de execuções...";

            pnlGrade.Location = new Point(10, 58);
            pnlGrade.Size = new Size(540, 490);
            pnlGrade.BorderStyle = BorderStyle.FixedSingle;
            pnlGrade.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pnlGrade.Paint += pnlGrade_Paint;

            // O TrackBar percorre o ÍNDICE na lista de ticks (os ticks gravados não são sequenciais)
            trkTicks.Location = new Point(10, 555);
            trkTicks.Size = new Size(540, 45);
            trkTicks.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            trkTicks.TickStyle = TickStyle.None;
            trkTicks.Enabled = false;
            trkTicks.ValueChanged += trkTicks_ValueChanged;

            lblTick.Location = new Point(10, 605);
            lblTick.Size = new Size(540, 20);
            lblTick.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lblTick.Font = new Font(Font, FontStyle.Bold);
            lblTick.Text = "Tick atual: -";

            Controls.Add(cmbExecucoes);
            Controls.Add(btnPlay);
            Controls.Add(lblStatus);
            Controls.Add(pnlGrade);
            Controls.Add(trkTicks);
            Controls.Add(lblTick);

            timerPlay.Interval = IntervaloPlayMs;
            timerPlay.Tick += timerPlay_Tick;

            Load += (s, e) => CarregarListaExecucoes();
            FormClosed += (s, e) => { timerPlay.Stop(); timerPlay.Dispose(); };
        }

        // ---------------------------------------------------------------- banco (só leitura)

        // Mesma conexão do resto do programa (ConexaoMySql.config), apontando para o banco do simulador
        private static string StringConexao()
        {
            MySqlConnectionStringBuilder b = new MySqlConnectionStringBuilder(ConfiguracaoBanco.ObterStringConexao());
            b.Database = NomeBanco;
            b.ConnectionTimeout = 5; // banco fora do ar: falha rápido
            return b.ConnectionString;
        }

        private void CarregarListaExecucoes()
        {
            Task.Run(() =>
            {
                try
                {
                    // Reaproveita a consulta do repositório (execuções que têm leituras gravadas)
                    IRepositorioSimulacao repo = new RepositorioMySql(ConfiguracaoBanco.ObterStringConexao());
                    IList<ExecucaoGravada> lista = repo.ListarExecucoesComLeituras();
                    NaTela(() =>
                    {
                        cmbExecucoes.Items.Clear();
                        foreach (ExecucaoGravada x in lista)
                            cmbExecucoes.Items.Add(x);
                        if (lista.Count == 0)
                            lblStatus.Text = "Nenhuma execução com leituras gravadas. Rode uma simulação com \"Gravar no banco\".";
                        else
                        {
                            lblStatus.Text = "Escolha uma execução.";
                            cmbExecucoes.SelectedIndex = 0; // já carrega a mais recente
                        }
                    });
                }
                catch (Exception ex)
                {
                    string mensagem = "Erro ao acessar o banco: " + Curta(ex);
                    NaTela(() => lblStatus.Text = mensagem);
                }
            });
        }

        private void cmbExecucoes_SelectedIndexChanged(object sender, EventArgs e)
        {
            ExecucaoGravada x = cmbExecucoes.SelectedItem as ExecucaoGravada;
            if (x == null)
                return;

            PararPlay();
            ticks = null;
            temperaturas = null;
            trkTicks.Enabled = false;
            btnPlay.Enabled = false;
            lblTick.Text = "Tick atual: -";
            lblStatus.Text = "Carregando leituras da execução #" + x.ExecucaoId + "...";
            pnlGrade.Invalidate();

            int versao = ++versaoCarga;
            long execucaoId = x.ExecucaoId;
            int n = x.Tamanho;
            Task.Run(() =>
            {
                try
                {
                    List<long> listaTicks;
                    Dictionary<long, double[,]> mapa;
                    CarregarLeituras(execucaoId, n, out listaTicks, out mapa);
                    NaTela(() =>
                    {
                        if (versao != versaoCarga)
                            return; // o usuário já escolheu outra execução
                        AoCarregar(execucaoId, n, listaTicks, mapa);
                    });
                }
                catch (Exception ex)
                {
                    string mensagem = "Erro ao carregar a execução #" + execucaoId + ": " + Curta(ex);
                    NaTela(() => { if (versao == versaoCarga) lblStatus.Text = mensagem; });
                }
            });
        }

        // UMA consulta traz todas as leituras da execução, já ordenadas por tick (a chave primária
        // começa por execucao_id, tick: a leitura segue o índice). Um SELECT por tick seria
        // milhares de idas ao banco em execuções longas.
        private static void CarregarLeituras(long execucaoId, int n, out List<long> listaTicks, out Dictionary<long, double[,]> mapa)
        {
            listaTicks = new List<long>();
            mapa = new Dictionary<long, double[,]>();
            using (MySqlConnection conexao = new MySqlConnection(StringConexao()))
            {
                conexao.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT tick, linha, coluna, temperatura FROM leituras WHERE execucao_id = @id ORDER BY tick, linha, coluna", conexao))
                {
                    cmd.Parameters.AddWithValue("@id", execucaoId);
                    using (MySqlDataReader r = cmd.ExecuteReader())
                    {
                        double[,] atual = null;
                        long tickAtual = -1;
                        while (r.Read())
                        {
                            long tick = Convert.ToInt64(r.GetValue(0));
                            if (atual == null || tick != tickAtual)
                            {
                                atual = new double[n, n];
                                // Célula sem leitura fica NaN e é pintada de cinza (dado incompleto)
                                for (int l = 0; l < n; l++)
                                    for (int c = 0; c < n; c++)
                                        atual[l, c] = double.NaN;
                                tickAtual = tick;
                                listaTicks.Add(tick);
                                mapa[tick] = atual;
                            }
                            int linha = Convert.ToInt32(r.GetValue(1));
                            int coluna = Convert.ToInt32(r.GetValue(2));
                            if (linha >= 0 && coluna >= 0 && linha < n && coluna < n)
                                atual[linha, coluna] = r.GetDouble(3);
                        }
                    }
                }
            }
        }

        private void AoCarregar(long execucaoId, int n, List<long> listaTicks, Dictionary<long, double[,]> mapa)
        {
            if (listaTicks.Count == 0)
            {
                lblStatus.Text = "A execução #" + execucaoId + " não tem leituras gravadas.";
                return;
            }

            tamanho = n;
            ticks = listaTicks;
            temperaturas = mapa;

            trkTicks.Minimum = 0;
            trkTicks.Maximum = listaTicks.Count - 1;
            trkTicks.Value = 0;
            trkTicks.Enabled = listaTicks.Count > 1;
            btnPlay.Enabled = listaTicks.Count > 1;
            lblStatus.Text = string.Format("Execução #{0}: {1} ticks gravados carregados ({2}x{2}).", execucaoId, listaTicks.Count, n);
            AtualizarTick();
        }

        // ---------------------------------------------------------------- navegação / play

        private void trkTicks_ValueChanged(object sender, EventArgs e)
        {
            AtualizarTick();
        }

        private void AtualizarTick()
        {
            if (ticks == null || ticks.Count == 0)
                return;
            int i = Math.Max(0, Math.Min(trkTicks.Value, ticks.Count - 1));
            lblTick.Text = string.Format("Tick atual: {0:N0}   ({1}/{2})", ticks[i], i + 1, ticks.Count);
            pnlGrade.Invalidate();
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            if (timerPlay.Enabled)
            {
                PararPlay();
                return;
            }
            if (ticks == null || ticks.Count < 2)
                return;

            // Se já está no fim, o Play recomeça do primeiro tick
            if (trkTicks.Value >= trkTicks.Maximum)
                trkTicks.Value = 0;
            timerPlay.Start();
            btnPlay.Text = "Pause";
        }

        private void timerPlay_Tick(object sender, EventArgs e)
        {
            if (ticks == null || trkTicks.Value >= trkTicks.Maximum)
            {
                PararPlay(); // chegou ao último tick gravado
                return;
            }
            trkTicks.Value++;
            if (trkTicks.Value >= trkTicks.Maximum)
                PararPlay();
        }

        private void PararPlay()
        {
            timerPlay.Stop();
            btnPlay.Text = "Play";
        }

        // ---------------------------------------------------------------- desenho

        private void pnlGrade_Paint(object sender, PaintEventArgs e)
        {
            if (ticks == null || temperaturas == null || ticks.Count == 0 || tamanho <= 0)
                return;

            double[,] matriz;
            int i = Math.Max(0, Math.Min(trkTicks.Value, ticks.Count - 1));
            if (!temperaturas.TryGetValue(ticks[i], out matriz))
                return;

            int n = tamanho;
            int largura = pnlGrade.ClientSize.Width / n;
            int altura = pnlGrade.ClientSize.Height / n;
            if (largura <= 0 || altura <= 0)
                return;
            string formato = n <= 10 ? "F1" : "F0"; // mesmo critério do grid principal

            using (SolidBrush pincel = new SolidBrush(Color.Black))
            {
                for (int l = 0; l < n; l++)
                {
                    for (int c = 0; c < n; c++)
                    {
                        Rectangle r = new Rectangle(c * largura, l * altura, largura, altura);
                        double t = matriz[l, c];
                        if (double.IsNaN(t))
                        {
                            pincel.Color = Color.LightGray; // sem leitura gravada nesta célula
                            e.Graphics.FillRectangle(pincel, r);
                            e.Graphics.DrawRectangle(Pens.Black, r);
                            continue;
                        }

                        Color cor = CorPorTemperatura(t);
                        pincel.Color = cor;
                        e.Graphics.FillRectangle(pincel, r);
                        e.Graphics.DrawRectangle(Pens.Black, r);
                        e.Graphics.DrawString(t.ToString(formato), Font, PincelTextoContraste(cor), r.X + 5, r.Y + 5);
                    }
                }
            }
        }

        // Cópia fiel de MainForm.CalcularCorPorTemperatura (verde -> amarelo -> laranja -> vermelho)
        private static Color CorPorTemperatura(double temperatura)
        {
            double proporcao;
            if (temperatura <= TemperaturaFocoMaxima)
                proporcao = FracaoPaletaFoco * (temperatura - TemperaturaMinimaEscala) /
                            (TemperaturaFocoMaxima - TemperaturaMinimaEscala);
            else
                proporcao = FracaoPaletaFoco + (1.0 - FracaoPaletaFoco) *
                            (temperatura - TemperaturaFocoMaxima) /
                            (TemperaturaMaximaEscala - TemperaturaFocoMaxima);

            proporcao = Math.Max(0.0, Math.Min(1.0, proporcao));

            if (proporcao < 0.25)
                return InterpolarCor(CorVerdeFrio, CorVerde, proporcao / 0.25);
            if (proporcao < 0.50)
                return InterpolarCor(CorVerde, CorAmarelo, (proporcao - 0.25) / 0.25);
            if (proporcao < 0.75)
                return InterpolarCor(CorAmarelo, CorLaranja, (proporcao - 0.50) / 0.25);
            return InterpolarCor(CorLaranja, CorVermelho, (proporcao - 0.75) / 0.25);
        }

        private static Color InterpolarCor(Color de, Color ate, double t)
        {
            t = Math.Max(0.0, Math.Min(1.0, t));
            return Color.FromArgb(
                (int)Math.Round(de.R + (ate.R - de.R) * t),
                (int)Math.Round(de.G + (ate.G - de.G) * t),
                (int)Math.Round(de.B + (ate.B - de.B) * t));
        }

        private static Brush PincelTextoContraste(Color fundo)
        {
            int luminancia = (299 * fundo.R + 587 * fundo.G + 114 * fundo.B) / 1000;
            return luminancia >= 100 ? Brushes.Black : Brushes.White;
        }

        // ---------------------------------------------------------------- auxiliares

        // Volta para a thread da UI; se a janela já foi fechada, ignora
        private void NaTela(Action acao)
        {
            try
            {
                if (IsHandleCreated && !IsDisposed)
                    BeginInvoke(acao);
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        private static string Curta(Exception ex)
        {
            string m = ex.GetBaseException().Message.Replace("\r", " ").Replace("\n", " ");
            return m.Length > 140 ? m.Substring(0, 140) + "..." : m;
        }
    }
}
