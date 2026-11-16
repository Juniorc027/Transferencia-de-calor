using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using TransferenciaCalor.Data;
using System.Threading.Tasks;
using TransferenciaCalor.Models;
using TransferenciaCalor.Persistencia;
using TransferenciaCalor.Simulation;

namespace TransferenciaCalor.UI
{
    public partial class MainForm : Form
    {
        // Atributos privados — encapsulamento
        // Interface: pode ser o motor sequencial da N1 ou o paralelo da N2 (checkbox "Processamento paralelo")
        private IMotorSimulacao motor;
        private HeatGrid grade;
        private int linhaSelecionada = -1;
        private int colunaSelecionada = -1;

        // Cronômetro de tempo real: conta quanto tempo a simulação ficou
        // efetivamente rodando (entre Iniciar e Pausar/equilíbrio).
        private readonly Stopwatch cronometroSimulacao = new Stopwatch();

        // Total de ticks já aplicados nesta simulação. Guardado à parte porque
        // é o que permite calcular o TEMPO SIMULADO (Δt x totalTicks) — sem
        // isso não tinha como mostrar na tela quanto "tempo de física" já
        // passou, só o tempo real do relógio.
        private long totalTicks = 0;

        // Δt (s) de cada tick, passado ao motor na criação da grade. Extraído
        // pra constante pra não duplicar o mesmo número em dois lugares
        // (aqui e no cálculo do tempo simulado).
        private const double IntervaloTempoSimulacao = 0.01;

        // Temperatura de cada corpo no último refresh da tela — usada só pra
        // comparar e saber se cada célula está esquentando ou esfriando desde
        // a última vez que a interface foi atualizada (não entra em nenhum
        // cálculo físico, é só pra desenhar a seta de tendência).
        private double[,] temperaturasAnteriores;

        // Tendência de cada célula desde o último refresh: -1 caindo, 0
        // estável, +1 subindo. Calculada uma vez por AtualizarInterface() —
        // nunca dentro do Paint, que também dispara ao redimensionar a janela
        // ou clicar numa célula, e recalcular ali apagaria as setas sem a
        // temperatura ter realmente mudado.
        private sbyte[,] tendencias;

        // Guarda o ΔT do mesmo intervalo usado pela tendência para exibir ao
        // lado da seta sem misturar essa informação com o cálculo da física.
        private double[,] variacoesTemperatura;

        // 0,01 K evita setas por ruído numérico e ainda preserva mudanças
        // pequenas que são úteis durante o acompanhamento da simulação.
        private const double ToleranciaTendencia = 0.01; // K

        // Temperaturas de referência para o gradiente de cores (ajustáveis).
        // A maior parte da paleta é usada entre 250 K e 400 K, onde acontecem
        // as trocas de calor do dia a dia; acima disso a escala continua até 1000 K.
        private const double TemperaturaMinimaEscala = 250.0; // K
        private const double TemperaturaFocoMaxima = 400.0; // K
        private const double TemperaturaMaximaEscala = 1000.0; // K
        private const double FracaoPaletaFoco = 0.75; // 75% da paleta para 250–400 K

        public MainForm()
        {
            InitializeComponent();
            ConfigurarComboMateriais();

            // Reduz o "piscar" ao redesenhar o Panel repetidamente
            typeof(Panel).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, pnlGrade, new object[] { true });

            // Mostra os valores iniciais dos controles novos sem esperar o
            // usuário mexer neles (o evento ValueChanged não dispara sozinho
            // só porque o Designer definiu um Value inicial).
            AtualizarConversorCelsius();
            AtualizarLabelVelocidade();
            AtualizarControlesSimulacao();
            ConfigurarBanco();
            ConfigurarReproducao();
            OcultarReproducao();
        }

        // A tela "Reproduzir execução" (opcional no enunciado) está OCULTA para não confundir a
        // demonstração. O código continua inteiro: para ligar de volta, basta remover a chamada
        // OcultarReproducao() no construtor acima.
        private void OcultarReproducao()
        {
            btnReproduzir.Visible = false;
            btnVoltarSimulacao.Visible = false;
            trkReproducao.Visible = false;
            lblReproducao.Visible = false;
            ClientSize = new Size(ClientSize.Width, 640); // altura original, sem a faixa da reprodução
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        // Preenche os ComboBox com os materiais do catálogo
        private void ConfigurarComboMateriais()
        {
            foreach (Material material in MaterialCatalog.Todos)
            {
                cmbMaterialInicial.Items.Add(material);
                cmbTrocarMaterial.Items.Add(material);
            }

            if (cmbMaterialInicial.Items.Count > 0)
                cmbMaterialInicial.SelectedIndex = 0;
        }

        // Botão "Criar Grade": monta a HeatGrid e o motor de simulação
        private void btnCriarGrade_Click(object sender, EventArgs e)
        {
            // Se o usuário criar uma grade nova enquanto a simulação anterior
            // ainda está rodando, sem isso o Timer continuaria ativo e
            // começaria a avançar a grade nova sozinho, sem o usuário ter
            // clicado em "Iniciar".
            timerSimulacao.Stop();

            // Encerra a gravação da grade anterior (snapshot final + fim da execução) ANTES de
            // trocá-la: o snapshot lê a grade antiga.
            FinalizarGravacao(false, true);
            ZerarStatusBancoParaNovaGrade();

            int n = (int)numTamanho.Value;
            Material materialInicial = (Material)cmbMaterialInicial.SelectedItem;

            if (materialInicial == null)
            {
                MessageBox.Show("Selecione um material inicial.");
                return;
            }

            double ladoCubo = (double)numLadoCubo.Value; // informado pelo usuário, em metros (SI)
            double temperaturaInicial = 300.0; // 300K (~27°C)

            InstalarGrade(new HeatGrid(n, materialInicial, ladoCubo, temperaturaInicial), 0);
        }

        // Instala uma grade como a grade atual da tela. Compartilhado pelo "Criar Grade" e pelo
        // "Refazer" (que mostra o estado final refeito), para não duplicar essa inicialização.
        private void InstalarGrade(HeatGrid novaGrade, long ticksIniciais)
        {
            grade = novaGrade;
            motor = CriarMotor(grade);
            int n = grade.Tamanho;

            // Sem histórico ainda: nenhuma célula tem seta até o primeiro
            // avanço da simulação (tendencias[,] já nasce zerada — sbyte
            // padrão é 0 — só precisamos guardar a temperatura de partida).
            temperaturasAnteriores = new double[n, n];
            tendencias = new sbyte[n, n];
            variacoesTemperatura = new double[n, n];
            for (int linha = 0; linha < n; linha++)
            {
                for (int coluna = 0; coluna < n; coluna++)
                {
                    temperaturasAnteriores[linha, coluna] = grade.ObterCorpo(linha, coluna).TemperaturaAtual;
                }
            }

            linhaSelecionada = -1;
            colunaSelecionada = -1;
            LimparDicaCelula(); // a grade mudou: o texto antigo não vale mais
            lblCorpoSelecionado.Text = "Nenhum corpo selecionado";
            AtualizarInfoGrade();

            totalTicks = ticksIniciais;
            eventosPendentes = null; // grade nova não herda eventos (o Refazer define os seus depois)
            proximoEventoPendente = 0;
            cronometroCalculo = 0;
            equilibrioAtingido = false;
            sessao = null; // nova grade = nova execução (a anterior já foi finalizada)
            AtualizarControlesBanco();
            cronometroSimulacao.Reset();
            AtualizarLabelTempoDecorrido();
            AtualizarLabelTempoSimulado();
            AtualizarControlesSimulacao();

            pnlGrade.Invalidate(); // força o redesenho do Panel
        }

        // Mostra acima da grade qual material ela está usando. Quando o usuário
        // troca o material de algumas células, a grade deixa de ser homogênea e
        // o rótulo lista os materiais presentes, em vez de mentir um único nome.
        private void AtualizarInfoGrade()
        {
            if (grade == null)
            {
                lblInfoGrade.Text = "Grade: ainda não criada";
                return;
            }

            List<string> materiais = new List<string>();
            for (int linha = 0; linha < grade.Tamanho; linha++)
            {
                for (int coluna = 0; coluna < grade.Tamanho; coluna++)
                {
                    string nome = grade.ObterCorpo(linha, coluna).Material.Nome;
                    if (!materiais.Contains(nome))
                        materiais.Add(nome);
                }
            }

            string textoMateriais = materiais.Count == 1
                ? "Material: " + materiais[0]
                : "Materiais: " + string.Join(", ", materiais.ToArray());

            double ladoCubo = grade.ObterCorpo(0, 0).LadoCubo;
            lblInfoGrade.Text = string.Format("Grade {0}×{0} · {1} · lado {2:F3} m",
                grade.Tamanho, textoMateriais, ladoCubo);
        }

        // Tamanhos mínimos da célula (px) para cada elemento caber sem estourar o desenho
        private const int LadoMinimoTextoDecimal = 45; // "1000,0" com 1 casa decimal
        private const int LadoMinimoTexto = 28;        // só a parte inteira
        private const int LadoMinimoSeta = 24;         // seta de tendência

        // Geometria ÚNICA da grade, usada pelo desenho e pelo mouse (clique/tooltip), para os
        // dois nunca divergirem. Células QUADRADAS (o painel não é quadrado e células
        // retangulares distorciam a grade) e a grade centralizada no painel.
        private void CalcularGeometriaGrade(int n, out int ladoCelula, out int origemX, out int origemY)
        {
            int largura = pnlGrade.ClientSize.Width;
            int altura = pnlGrade.ClientSize.Height;
            ladoCelula = n > 0 ? Math.Min(largura, altura) / n : 0;
            origemX = (largura - ladoCelula * n) / 2;
            origemY = (altura - ladoCelula * n) / 2;
        }

        // Objetos GDI+ reaproveitados durante um Paint. Antes, cada célula criava seu próprio
        // brush/pen (e cada seta criava uma fonte), o que gerava milhares de objetos por segundo
        // em grades maiores e deixava a interface pesada.
        private sealed class RecursosDesenho : IDisposable
        {
            private readonly SolidBrush pincelCelula;
            private readonly Pen bordaCelula;
            private readonly SolidBrush pincelSeta;
            private readonly Pen haste;
            private readonly Font fonteVariacao;

            public RecursosDesenho(Font fonteVariacao)
            {
                pincelCelula = new SolidBrush(Color.Black);
                bordaCelula = new Pen(Color.Black, 1f);
                pincelSeta = new SolidBrush(Color.Black);
                haste = new Pen(Color.Black, 2f);
                this.fonteVariacao = fonteVariacao;
            }

            public SolidBrush PincelCelula { get { return pincelCelula; } }
            public Pen BordaCelula { get { return bordaCelula; } }
            public SolidBrush PincelSeta { get { return pincelSeta; } }
            public Pen Haste { get { return haste; } }
            public Font FonteVariacao { get { return fonteVariacao; } }

            public void Dispose()
            {
                pincelCelula.Dispose();
                bordaCelula.Dispose();
                pincelSeta.Dispose();
                haste.Dispose();
                if (fonteVariacao != null)
                    fonteVariacao.Dispose();
            }
        }

        // Evento Paint do pnlGrade — desenha a grade toda vez que precisa redesenhar
        private void pnlGrade_Paint(object sender, PaintEventArgs e)
        {
            // Fonte dos dados: o motor ao vivo (grade) OU, no modo reprodução, o snapshot lido do
            // banco. O desenho (cores, texto, borda) é o MESMO nos dois casos; só muda de onde
            // vem a temperatura de cada célula.
            bool reproduzindo = emReproducao;
            if (reproduzindo ? snapshotReproducao == null : grade == null)
                return; // ainda não foi criada nenhuma grade (ou o snapshot ainda não chegou)

            int n = reproduzindo ? snapshotReproducao.Tamanho : grade.Tamanho;
            int ladoCelula, origemX, origemY;
            CalcularGeometriaGrade(n, out ladoCelula, out origemX, out origemY);
            if (ladoCelula <= 0)
                return;
            int larguraCelula = ladoCelula;
            int alturaCelula = ladoCelula;
            int menorDimensao = ladoCelula;

            // O texto depende do espaço REAL da célula (não só de N): em 50x50 a célula tem
            // ~10 px, onde nenhum número cabe. Com espaço, 1 casa decimal ajuda a perceber
            // variações pequenas; com pouco espaço, só o inteiro; sem espaço, só a cor (o
            // tooltip continua mostrando o valor exato).
            string formato = ladoCelula >= LadoMinimoTextoDecimal ? "F1"
                           : ladoCelula >= LadoMinimoTexto ? "F0"
                           : null;

            // A fonte do ΔT só é necessária quando a célula é grande o bastante para exibi-lo.
            // Em N=20, por exemplo, ela não é usada e deixa de ser criada a cada célula.
            Font fonteVariacao = null;
            if (menorDimensao >= 70)
            {
                float tamanhoFonte = Math.Max(7f, Math.Min(9f, menorDimensao * 0.09f));
                fonteVariacao = new Font(this.Font.FontFamily, tamanhoFonte, FontStyle.Bold);
            }

            using (RecursosDesenho recursos = new RecursosDesenho(fonteVariacao))
            {
                for (int linha = 0; linha < n; linha++)
                {
                    for (int coluna = 0; coluna < n; coluna++)
                    {
                        // Reprodução: célula ausente no banco (leitura incompleta) fica cinza, sem texto.
                        if (reproduzindo && !snapshotReproducao.TemCelula(linha, coluna))
                        {
                            Rectangle vazio = new Rectangle(origemX + coluna * larguraCelula, origemY + linha * alturaCelula, larguraCelula, alturaCelula);
                            recursos.PincelCelula.Color = Color.LightGray;
                            e.Graphics.FillRectangle(recursos.PincelCelula, vazio);
                            e.Graphics.DrawRectangle(Pens.Black, vazio);
                            continue;
                        }

                        double temperaturaCelula = reproduzindo
                            ? snapshotReproducao.Temperatura(linha, coluna)
                            : grade.ObterCorpo(linha, coluna).TemperaturaAtual;
                        Color cor = CalcularCorPorTemperatura(temperaturaCelula);

                        Rectangle retangulo = new Rectangle(
                            origemX + coluna * larguraCelula,
                            origemY + linha * alturaCelula,
                            larguraCelula,
                            alturaCelula
                        );

                        recursos.PincelCelula.Color = cor;
                        e.Graphics.FillRectangle(recursos.PincelCelula, retangulo);

                        // Destaca o corpo selecionado com borda mais grossa
                        // Mesma variável do tooltip (hover) quando o mouse está na grade; sem hover,
                        // mostra o bloco selecionado. Assim destaque e tooltip nunca divergem.
                        // Na reprodução não há seleção (ela pertence à grade ao vivo, que pode ter outro N).
                        bool destacar = hoverLinha != -1
                            ? (linha == hoverLinha && coluna == hoverColuna)
                            : (!reproduzindo && linha == linhaSelecionada && coluna == colunaSelecionada);
                        // Magenta não aparece na escala verde→vermelho, então a
                        // seleção/hover continua visível em qualquer temperatura.
                        recursos.BordaCelula.Color = destacar ? Color.Magenta : Color.Black;
                        recursos.BordaCelula.Width = destacar ? 3f : 1f;

                        e.Graphics.DrawRectangle(recursos.BordaCelula, retangulo);

                        if (formato != null)
                        {
                            string texto = temperaturaCelula.ToString(formato);
                            e.Graphics.DrawString(texto, this.Font, PincelTextoContraste(cor),
                                retangulo.X + 5, retangulo.Y + 5);
                        }

                        // Reprodução não tem seta: tendência compara dois refreshes consecutivos
                        // da simulação, e aqui só existe o estado de um tick gravado.
                        if (reproduzindo)
                            continue;

                        // Seta de tendência: só lê tendencias[,], nunca recalcula
                        // aqui (quem recalcula é AtualizarTendencias, chamada só
                        // depois de um avanço de verdade da simulação).
                        // Em células pequenas a seta não cabe sem cobrir a célula inteira
                        sbyte tendencia = tendencias[linha, coluna];
                        if (tendencia != 0 && ladoCelula >= LadoMinimoSeta)
                        {
                            DesenharSetaTendencia(
                                e.Graphics,
                                retangulo,
                                tendencia,
                                variacoesTemperatura[linha, coluna],
                                cor,
                                recursos);
                        }
                    }
                }
            }
        }

        private void DesenharSetaTendencia(Graphics g, Rectangle celula, int tendencia, double variacao, Color corFundoCelula, RecursosDesenho recursos)
        {
            // A seta completa fica centralizada; vermelho forte indica aquecimento
            // e azul escuro resfriamento. Sem contorno branco: o desenho fica mais
            // limpo e a seta se destaca pela própria cor.
            int menorDimensao = Math.Min(celula.Width, celula.Height);
            int comprimento = Math.Max(10, Math.Min(28, (int)(menorDimensao * 0.26)));
            int tamanhoPonta = Math.Max(4, comprimento / 3);
            int meiaLarguraPonta = Math.Max(3, tamanhoPonta / 2);
            int espessuraHaste = Math.Max(2, Math.Min(4, menorDimensao / 25));
            Color corTendencia = tendencia > 0
                ? Color.FromArgb(210, 0, 0)   // vermelho forte: aquecendo
                : Color.FromArgb(0, 40, 120); // azul escuro: resfriando

            string textoVariacao = variacao.ToString("+0.00;-0.00;0.00") + " K";
            Font fonteVariacao = recursos.FonteVariacao;

            SizeF dimensoesVariacao = SizeF.Empty;
            if (fonteVariacao != null)
                dimensoesVariacao = g.MeasureString(textoVariacao, fonteVariacao);

            // Em células pequenas, mantém a seta legível e oculta somente
            // o ΔT para que os elementos não se sobreponham.
            bool mostrarVariacao = fonteVariacao != null &&
                menorDimensao >= 70 &&
                dimensoesVariacao.Width <= celula.Width - 6 &&
                comprimento + dimensoesVariacao.Height + 8 <= celula.Height;

            int alturaConjunto = comprimento;
            if (mostrarVariacao)
                alturaConjunto += 4 + (int)Math.Ceiling(dimensoesVariacao.Height);

            int centroX = celula.X + celula.Width / 2;
            int inicioY = celula.Y + (celula.Height - alturaConjunto) / 2;
            int fimY = inicioY + comprimento;
            Point[] ponta;
            Point inicioHaste;
            Point fimHaste;

            if (tendencia > 0)
            {
                ponta = new Point[]
                {
                    new Point(centroX, inicioY),
                    new Point(centroX - meiaLarguraPonta, inicioY + tamanhoPonta),
                    new Point(centroX + meiaLarguraPonta, inicioY + tamanhoPonta)
                };
                inicioHaste = new Point(centroX, inicioY + tamanhoPonta);
                fimHaste = new Point(centroX, fimY);
            }
            else
            {
                ponta = new Point[]
                {
                    new Point(centroX, fimY),
                    new Point(centroX - meiaLarguraPonta, fimY - tamanhoPonta),
                    new Point(centroX + meiaLarguraPonta, fimY - tamanhoPonta)
                };
                inicioHaste = new Point(centroX, inicioY);
                fimHaste = new Point(centroX, fimY - tamanhoPonta);
            }

            // Ajusta os objetos compartilhados para esta célula em vez de criar novos.
            recursos.Haste.Width = espessuraHaste;
            recursos.Haste.Color = corTendencia;
            recursos.PincelSeta.Color = corTendencia;

            g.DrawLine(recursos.Haste, inicioHaste, fimHaste);
            g.FillPolygon(recursos.PincelSeta, ponta);

            if (mostrarVariacao)
            {
                float textoX = celula.X + (celula.Width - dimensoesVariacao.Width) / 2f;
                float textoY = fimY + 4;

                // Texto sem halo branco: usa preto ou branco conforme o brilho
                // do fundo da célula, o mesmo critério do valor de temperatura.
                g.DrawString(textoVariacao, fonteVariacao, PincelTextoContraste(corFundoCelula), textoX, textoY);
            }
        }

        // Recalcula, pra cada corpo, se a temperatura subiu, desceu ou ficou
        // estável desde o último refresh, e guarda o resultado em tendencias[,]
        // pro Paint desenhar a seta. Chamada só por AtualizarInterface() —
        // nunca pelo Paint (ver comentário no campo tendencias).
        private void AtualizarTendencias()
        {
            int n = grade.Tamanho;
            for (int linha = 0; linha < n; linha++)
            {
                for (int coluna = 0; coluna < n; coluna++)
                {
                    double atual = grade.ObterCorpo(linha, coluna).TemperaturaAtual;
                    double diferenca = atual - temperaturasAnteriores[linha, coluna];

                    if (diferenca > ToleranciaTendencia)
                        tendencias[linha, coluna] = 1;
                    else if (diferenca < -ToleranciaTendencia)
                        tendencias[linha, coluna] = -1;
                    else
                        tendencias[linha, coluna] = 0;

                    variacoesTemperatura[linha, coluna] = tendencias[linha, coluna] == 0
                        ? 0.0
                        : diferenca;

                    temperaturasAnteriores[linha, coluna] = atual;
                }
            }
        }

        // Cores-âncora da escala verde -> amarelo -> laranja -> vermelho.
        // Verde = mais frio, vermelho = mais quente.
        private static readonly Color CorVerdeFrio = Color.FromArgb(0, 100, 0);
        private static readonly Color CorVerde = Color.FromArgb(0, 200, 0);
        private static readonly Color CorAmarelo = Color.FromArgb(255, 230, 0);
        private static readonly Color CorLaranja = Color.FromArgb(255, 90, 0);
        private static readonly Color CorVermelho = Color.FromArgb(200, 0, 0);

        // Converte temperatura em cor numa escala verde -> amarelo -> laranja -> vermelho.
        // A faixa 250–400 K ocupa 75% da paleta para que pequenas trocas de calor perto
        // da temperatura ambiente fiquem visíveis; acima de 400 K os 25% restantes
        // ainda diferenciam temperaturas altas.
        private Color CalcularCorPorTemperatura(double temperatura)
        {
            double proporcao;
            if (temperatura <= TemperaturaFocoMaxima)
                proporcao = FracaoPaletaFoco * (temperatura - TemperaturaMinimaEscala) /
                            (TemperaturaFocoMaxima - TemperaturaMinimaEscala);
            else
                proporcao = FracaoPaletaFoco + (1.0 - FracaoPaletaFoco) *
                            (temperatura - TemperaturaFocoMaxima) /
                            (TemperaturaMaximaEscala - TemperaturaFocoMaxima);

            // Garante que a proporção fique entre 0 e 1, mesmo fora da escala
            proporcao = Math.Max(0.0, Math.Min(1.0, proporcao));

            // 300 K cai em p = 0,25 (verde) e 350 K em p = 0,50 (amarelo):
            // é justamente o trecho em que a troca de calor costuma acontecer.
            if (proporcao < 0.25)
                return InterpolarCor(CorVerdeFrio, CorVerde, proporcao / 0.25);
            if (proporcao < 0.50)
                return InterpolarCor(CorVerde, CorAmarelo, (proporcao - 0.25) / 0.25);
            if (proporcao < 0.75)
                return InterpolarCor(CorAmarelo, CorLaranja, (proporcao - 0.50) / 0.25);
            return InterpolarCor(CorLaranja, CorVermelho, (proporcao - 0.75) / 0.25);
        }

        // Interpolação linear entre duas cores, com t entre 0 e 1.
        private static Color InterpolarCor(Color de, Color ate, double t)
        {
            t = Math.Max(0.0, Math.Min(1.0, t));
            return Color.FromArgb(
                (int)Math.Round(de.R + (ate.R - de.R) * t),
                (int)Math.Round(de.G + (ate.G - de.G) * t),
                (int)Math.Round(de.B + (ate.B - de.B) * t));
        }

        // Preto ou branco conforme o brilho do fundo, para o texto da célula
        // continuar legível tanto no verde escuro quanto no vermelho.
        private static Brush PincelTextoContraste(Color fundo)
        {
            int luminancia = (299 * fundo.R + 587 * fundo.G + 114 * fundo.B) / 1000;
            return luminancia >= 100 ? Brushes.Black : Brushes.White;
        }

        // Clique no Panel: descobre em qual célula o usuário clicou
        private void pnlGrade_MouseClick(object sender, MouseEventArgs e)
        {
            // Só o botão esquerdo aplica o pincel; ver o bloco é feito pelo mouse parado (tooltip)
            // Em reprodução nada pode ser editado (só leitura do banco)
            if (grade == null || emReproducao || e.Button != MouseButtons.Left)
                return;

            int linhaClicada, colunaClicada;
            if (!TentarObterCelula(e.X, e.Y, out linhaClicada, out colunaClicada))
                return;

            linhaSelecionada = linhaClicada;
            colunaSelecionada = colunaClicada;

            // O clique APLICA o pincel; não escreve nos controles de seleção.
            // A leitura da célula aparece só no rótulo lblCorpoSelecionado.
            AplicarPincel(linhaSelecionada, colunaSelecionada);

            pnlGrade.Invalidate(); // redesenha pra mostrar a borda amarela de seleção
        }

        // Converte a posição do mouse em (linha, coluna). Compartilhado pelo clique e pelo
        // MouseMove para os dois usarem exatamente a mesma conta. Falso se fora da grade.
        private bool TentarObterCelula(int x, int y, out int linha, out int coluna)
        {
            linha = -1;
            coluna = -1;
            if (emReproducao ? snapshotReproducao == null : grade == null)
                return false;

            int n = emReproducao ? snapshotReproducao.Tamanho : grade.Tamanho;
            int ladoCelula, origemX, origemY;
            CalcularGeometriaGrade(n, out ladoCelula, out origemX, out origemY);
            x -= origemX; // a grade é centralizada: desconta a margem antes de dividir
            y -= origemY;
            if (ladoCelula <= 0 || x < 0 || y < 0)
                return false;

            // Proteção contra a borda (sobra de divisão inteira): fora do índice válido
            int c = x / ladoCelula;
            int l = y / ladoCelula;
            if (l >= n || c >= n)
                return false;

            linha = l;
            coluna = c;
            return true;
        }

        // ---- Inspeção por mouse (tooltip) ----
        // SOMENTE LEITURA: nada aqui escreve na grade, no pincel, nos controles nem em `eventos`.
        // Guardamos a última célula/material/temperatura exibidos para só montar a string
        // (alocação) e só mexer no ToolTip (piscar) quando algo realmente mudou.
        private readonly ToolTip dicaCelula = CriarDicaCelula();
        private int hoverLinha = -1;
        private int hoverColuna = -1;
        private int hoverMouseX;
        private int hoverMouseY;
        private Material hoverMaterial;
        private double hoverTemperaturaArredondada = double.NaN;
        private string hoverTextoReproducao; // último texto do tooltip no modo reprodução

        private static ToolTip CriarDicaCelula()
        {
            ToolTip dica = new ToolTip();
            dica.AutoPopDelay = 32767;   // não some sozinho enquanto o mouse está parado
            dica.InitialDelay = 200;
            dica.UseFading = false;      // sem animação, para não piscar ao atualizar o texto
            dica.UseAnimation = false;
            return dica;
        }

        private void pnlGrade_MouseMove(object sender, MouseEventArgs e)
        {
            int linha, coluna;
            if (!TentarObterCelula(e.X, e.Y, out linha, out coluna))
            {
                LimparDicaCelula();
                return;
            }

            // Posição guardada para reexibir o tooltip no mesmo lugar quando a temperatura mudar
            hoverMouseX = e.X;
            hoverMouseY = e.Y;

            if (linha != hoverLinha || coluna != hoverColuna)
            {
                hoverLinha = linha;
                hoverColuna = coluna;
                hoverMaterial = null;                       // força montar o texto da nova célula
                hoverTemperaturaArredondada = double.NaN;
                hoverTextoReproducao = null;
                pnlGrade.Invalidate();                      // destaque amarelo segue o mesmo bloco do tooltip
            }
            AtualizarDicaCelula();
        }

        private void pnlGrade_MouseLeave(object sender, EventArgs e)
        {
            LimparDicaCelula();
        }

        private void LimparDicaCelula()
        {
            if (hoverLinha == -1)
                return;
            hoverLinha = -1;
            hoverColuna = -1;
            hoverMaterial = null;
            hoverTemperaturaArredondada = double.NaN;
            hoverTextoReproducao = null;
            dicaCelula.Hide(pnlGrade);
            pnlGrade.Invalidate(); // destaque volta a mostrar só o bloco selecionado
        }

        // Relê o estado do motor ativo (sequencial ou paralelo, via IMotorSimulacao) na thread
        // da UI. Também é chamada por AtualizarInterface(), então o texto acompanha a
        // simulação com o mouse parado, sem Timer novo.
        private void AtualizarDicaCelula()
        {
            // Reprodução: o texto vem do snapshot gravado (material + temperatura daquele tick)
            if (emReproducao)
            {
                AtualizarDicaReproducao();
                return;
            }

            if (hoverLinha == -1 || motor == null)
                return;

            Body corpo = motor.Grade.ObterCorpo(hoverLinha, hoverColuna);
            double temperatura = corpo.TemperaturaAtual;
            double arredondada = Math.Round(temperatura, 2); // é o que aparece na tela (F2)

            if (ReferenceEquals(corpo.Material, hoverMaterial) && arredondada == hoverTemperaturaArredondada)
                return; // nada mudou: não aloca string nem toca no ToolTip

            hoverMaterial = corpo.Material;
            hoverTemperaturaArredondada = arredondada;

            // Show() reexibe o texto mesmo com o mouse dentro do mesmo controle. SetToolTip(painel)
            // não fazia isso: o ToolTip só relia o texto ao reaparecer, por isso ficava velho.
            dicaCelula.Show(string.Format(
                "Material: {0}\nTemperatura: {1:F2} K ({2:F2} °C)\nLado do cubo: {3:G} m\nPosição: ({4}, {5})\nCondutividade k: {6:G} W/(m·K)",
                corpo.Material.Nome, temperatura, temperatura - 273.15, corpo.LadoCubo,
                hoverLinha, hoverColuna, corpo.Material.K),
                pnlGrade, hoverMouseX + 16, hoverMouseY + 20);
        }

        // ---- Pincel (material + temperatura "armados") ----
        // Guardados à parte do que a grade mostra: só mudam quando o usuário mexe no
        // ComboBox de material ou no campo de temperatura, nunca por clique/tick/Timer.
        private Material pincelMaterial;
        private const double TemperaturaPadraoPincel = 300.0; // K, mesma da grade nova
        private double pincelTemperatura = TemperaturaPadraoPincel;

        private Material PincelMaterial
        {
            get { return pincelMaterial; }
            set { pincelMaterial = value; }
        }

        private double PincelTemperatura
        {
            get { return pincelTemperatura; }
            set { pincelTemperatura = value; }
        }

        private void numTemperaturaCorpo_ValueChanged(object sender, EventArgs e)
        {
            PincelTemperatura = (double)numTemperaturaCorpo.Value;
            lblAvisoPincel.Text = "";
        }

        // Lê a temperatura digitada. Se o texto estiver vazio/inválido o NumericUpDown mantém
        // o Value antigo sem avisar, então o texto é conferido aqui antes de aplicar.
        private bool TentarLerTemperaturaDigitada(out double temperatura)
        {
            temperatura = 0;
            decimal valor;
            if (!decimal.TryParse(numTemperaturaCorpo.Text, out valor)
                || valor < numTemperaturaCorpo.Minimum || valor > numTemperaturaCorpo.Maximum)
                return false;
            temperatura = (double)valor;
            return true;
        }

        // Aplica o pincel na célula. Usa os mesmos setters de antes (TrocarMaterial e
        // TemperaturaAtual) e registra um evento por aplicação, com o tick do motor.
        private void AplicarPincel(int linha, int coluna)
        {
            double temperatura;
            if (PincelMaterial == null)
            {
                // Aviso em rótulo (não MessageBox) para não incomodar a cada clique
                lblAvisoPincel.Text = "Escolha um material para aplicar.";
                AtualizarLabelCorpoSelecionado(grade.ObterCorpo(linha, coluna));
                return;
            }
            if (!TentarLerTemperaturaDigitada(out temperatura))
            {
                lblAvisoPincel.Text = "Temperatura inválida: nada foi aplicado.";
                AtualizarLabelCorpoSelecionado(grade.ObterCorpo(linha, coluna));
                return;
            }
            PincelTemperatura = temperatura;
            lblAvisoPincel.Text = "";

            Body corpo = grade.ObterCorpo(linha, coluna);
            bool materialMudou = !ReferenceEquals(corpo.Material, PincelMaterial);
            bool mudou = materialMudou || corpo.TemperaturaAtual != temperatura;

            grade.TrocarMaterial(linha, coluna, PincelMaterial);
            if (sessao != null)
                sessao.RegistrarEvento(new EventoSimulacao(totalTicks, TipoEvento.TrocouMaterial,
                    linha, coluna, PincelMaterial, null));

            corpo.TemperaturaAtual = PincelTemperatura;
            if (sessao != null)
                sessao.RegistrarEvento(new EventoSimulacao(totalTicks, TipoEvento.AplicouTemperatura,
                    linha, coluna, null, corpo.TemperaturaAtual));

            // Mudança manual, não é tendência real: zera a seta (mesma razão do botão Aplicar)
            temperaturasAnteriores[linha, coluna] = corpo.TemperaturaAtual;
            tendencias[linha, coluna] = 0;
            variacoesTemperatura[linha, coluna] = 0.0;

            AtualizarLabelCorpoSelecionado(corpo);

            // O material inicial do rótulo acima da grade muda assim que uma
            // célula recebe outro material (a grade fica heterogênea).
            if (materialMudou)
                AtualizarInfoGrade();

            // Editar material/temperatura muda o estado físico: se a simulação já
            // tinha atingido o equilíbrio, ela volta a poder ser iniciada.
            if (mudou)
            {
                equilibrioAtingido = false;
                AtualizarControlesSimulacao();
            }
        }

        private void AtualizarLabelCorpoSelecionado(Body corpo)
        {
            // Sufixo opcional com a tendência da célula selecionada — só texto
            // informativo, não influencia nada da simulação.
            sbyte tendencia = tendencias[linhaSelecionada, colunaSelecionada];
            string tendenciaTexto = tendencia > 0 ? " (subindo)" : tendencia < 0 ? " (caindo)" : " (estável)";

            lblCorpoSelecionado.Text = string.Format(
                "Corpo ({0},{1}) — Material: {2} — Temperatura: {3:F2} K{4}",
                linhaSelecionada, colunaSelecionada, corpo.Material.Nome, corpo.TemperaturaAtual, tendenciaTexto);
        }

        // Aplica a temperatura escolhida no numTemperaturaCorpo ao corpo selecionado.
        // É essencial pra testar a simulação: sem diferença de temperatura entre
        // vizinhos não há fluxo de calor nenhum pra observar.
        private void btnAplicarTemperatura_Click(object sender, EventArgs e)
        {
            if (grade == null || linhaSelecionada == -1)
            {
                MessageBox.Show("Selecione um corpo na grade primeiro.");
                return;
            }

            Body corpo = grade.ObterCorpo(linhaSelecionada, colunaSelecionada);
            double temperaturaAnterior = corpo.TemperaturaAtual;
            corpo.TemperaturaAtual = (double)numTemperaturaCorpo.Value;

            // O tick vem do CONTADOR DO MOTOR (totalTicks), não do frame do Timer
            if (sessao != null)
                sessao.RegistrarEvento(new EventoSimulacao(totalTicks, TipoEvento.AplicouTemperatura,
                    linhaSelecionada, colunaSelecionada, null, corpo.TemperaturaAtual));

            // Mudança manual do usuário, não é troca de calor — não é uma
            // "tendência" real da simulação, então zera a seta dessa célula e
            // atualiza a referência, senão o próximo tick compararia com a
            // temperatura antiga e mostraria uma seta falsa.
            temperaturasAnteriores[linhaSelecionada, colunaSelecionada] = corpo.TemperaturaAtual;
            tendencias[linhaSelecionada, colunaSelecionada] = 0;
            variacoesTemperatura[linhaSelecionada, colunaSelecionada] = 0.0;

            AtualizarLabelCorpoSelecionado(corpo);

            // Se a temperatura realmente mudou, o equilíbrio anterior deixou de valer.
            if (corpo.TemperaturaAtual != temperaturaAnterior)
            {
                equilibrioAtingido = false;
                AtualizarControlesSimulacao();
            }

            pnlGrade.Invalidate();
        }

        // Escolher o material só ARMA o pincel. Antes isto trocava o material da célula
        // selecionada, mas escolher o mesmo item de novo não disparava o evento e o
        // segundo clique não aplicava nada; agora quem aplica é o clique na grade.
        private void cmbTrocarMaterial_SelectedIndexChanged(object sender, EventArgs e)
        {
            PincelMaterial = (Material)cmbTrocarMaterial.SelectedItem;

            // Pedido do usuário: trocar de material (e só isso) devolve a temperatura ao
            // padrão. Cliques na grade, ticks e Criar Grade nunca passam por aqui.
            // Atribuir ao campo dispara numTemperaturaCorpo_ValueChanged, que atualiza o pincel
            // (se já estiver em 300 o evento não dispara, mas o valor já é o padrão).
            numTemperaturaCorpo.Value = (decimal)TemperaturaPadraoPincel;
            PincelTemperatura = TemperaturaPadraoPincel;
            lblAvisoPincel.Text = "";
        }

        // Mantém os botões coerentes com o estado real da simulação: rodando, pausado
        // ou em equilíbrio. Antes, "Iniciar" continuava habilitado depois do equilíbrio
        // (e repetia a mesma mensagem) e "Pausar" continuava habilitado com tudo parado.
        private void AtualizarControlesSimulacao()
        {
            bool rodando = timerSimulacao.Enabled;

            // Em reprodução nada roda nem é editado: só o slider e o botão "Voltar à simulação".
            btnIniciar.Enabled = !emReproducao && !rodando && !equilibrioAtingido;
            btnPausar.Enabled = !emReproducao && rodando;
            btnCriarGrade.Enabled = !emReproducao;
            btnAplicarTemperatura.Enabled = !emReproducao;
            cmbTrocarMaterial.Enabled = !emReproducao;
            numTemperaturaCorpo.Enabled = !emReproducao;
        }

        // Botão "Iniciar": liga o Timer
        private void btnIniciar_Click(object sender, EventArgs e)
        {
            if (motor == null)
            {
                MessageBox.Show("Crie a grade primeiro.");
                return;
            }

            IniciarGravacaoSePedida();

            timerSimulacao.Start();
            cronometroSimulacao.Start(); // Start() retoma de onde parou, sem zerar
            AtualizarControlesSimulacao();
        }

        // Botão "Pausar": desliga o Timer
        private void btnPausar_Click(object sender, EventArgs e)
        {
            timerSimulacao.Stop();
            cronometroSimulacao.Stop();

            // Pausa = tick de gravação garantido + execução finalizada (pode ser retomada)
            FinalizarGravacao(false, false);
            AtualizarControlesSimulacao();
        }

        // Evento Tick do Timer: avança a simulação automaticamente.
        // Roda vários ticks (Δt pequeno e já validado como estável) por frame,
        // em vez de aumentar o Δt, pra não reintroduzir oscilação/overshoot.
        // Esse valor é a base (1x); a velocidade multiplica ele, não o Δt.
        private const int TicksPorFrameBase = 20;

        private void timerSimulacao_Tick(object sender, EventArgs e)
        {
            // Lido a cada Tick do Timer (não só quando o usuário mexe no
            // TrackBar) pra a velocidade valer imediatamente mesmo com a
            // simulação já rodando, sem precisar pausar/retomar.
            //
            // Aumentamos a QUANTIDADE DE TICKS por frame, não o Δt do motor:
            // Δt maior mudaria o próprio resultado numérico da simulação e
            // poderia reintroduzir a instabilidade/oscilação que já foi
            // validada como não ocorrendo no Δt atual (ver auditoria). Rodar
            // mais ticks do mesmo Δt só "avança o filme mais rápido" sem
            // mudar a física de cada passo individual.
            int ticksPorFrame = TicksPorFrameBase * trkVelocidade.Value;

            AvancarTicks(ticksPorFrame);

            // Tempo REAL (relógio do PC); o tempo SIMULADO é atualizado dentro
            // de AvancarTicks/AtualizarInterface, junto com o resto da tela.
            AtualizarLabelTempoDecorrido();
        }

        // Avança "quantidade" ticks de uma vez — usado tanto pelo Step quanto
        // pelo Timer, pra não duplicar essa lógica nos dois lugares.
        private void AvancarTicks(int quantidade)
        {
            // A amostragem do banco é contada em TICKS (não em tempo): o lote é cortado
            // exatamente no próximo tick a gravar. Sem gravação, é um lote só, como na N1.
            long restante = quantidade;
            while (restante > 0)
            {
                // Eventos do Refazer devidos no tick atual: aplicados DEPOIS do snapshot deste tick
                // e ANTES do próximo Tick(), mesma ordem do ExecutorSimulacao.
                AplicarEventosPendentesDevidos();

                bool gravando = sessao != null && sessao.Aberta;
                long lote = gravando ? Math.Min(restante, sessao.TicksAteProximaAmostra(totalTicks)) : restante;
                // Corta o lote no próximo evento pendente, para ele cair no tick exato
                lote = Math.Min(lote, TicksAteProximoEventoPendente());

                // ms_calculo: tempo acumulado SÓ dentro de Tick() (não conta a cópia do snapshot)
                long inicio = Stopwatch.GetTimestamp();
                for (long i = 0; i < lote; i++)
                    motor.Tick();
                cronometroCalculo += Stopwatch.GetTimestamp() - inicio;

                totalTicks += lote;
                restante -= lote;

                // O Tick (inclusive o paralelo, que é fork-join) já terminou por completo aqui
                if (gravando)
                    sessao.AoConcluirTick(grade, totalTicks);
            }

            // AtingiuEquilibrio() percorre TODOS os pares da grade de novo
            // (é tão caro quanto um Tick()); chamar ele a cada tick individual
            // do laço acima ficava caro demais com velocidade alta (até
            // TicksPorFrameBase x 10 = 200 ticks por frame, a cada 50 ms, com
            // N=20). Checar uma única vez ao final do lote só atrasa a
            // detecção do equilíbrio em, no máximo, os ticks desse lote — o
            // motor continua numericamente estável mesmo rodando um pouco além
            // do equilíbrio, então isso não muda o resultado, só quando ele é percebido.
            // Com eventos do Refazer ainda por vir, não para no equilíbrio: na execução original o
            // usuário mexeu na grade depois dele, e a reprodução precisa chegar até esses ticks.
            if (!HaEventosPendentes() && motor.AtingiuEquilibrio())
            {
                timerSimulacao.Stop();
                cronometroSimulacao.Stop();
                AtualizarInterface();
                equilibrioAtingido = true;
                FinalizarGravacao(true, false);
                AtualizarControlesSimulacao();
                MessageBox.Show("Equilíbrio térmico atingido!");
                return;
            }

            AtualizarInterface();
        }

        // Atualiza tudo que depende do estado atual da simulação: as setas de
        // tendência, o label do corpo selecionado (se houver um selecionado),
        // o tempo simulado e o desenho da grade. Chamado após qualquer avanço
        // de tick (Timer ou Step) pra a tela nunca ficar "congelada" mostrando
        // um estado antigo.
        private void AtualizarInterface()
        {
            AtualizarTendencias();

            if (linhaSelecionada != -1)
                AtualizarLabelCorpoSelecionado(grade.ObterCorpo(linhaSelecionada, colunaSelecionada));

            AtualizarLabelTempoSimulado();
            AtualizarDicaCelula(); // tooltip acompanha a simulação com o mouse parado
            pnlGrade.Invalidate();
        }

        // Atualiza o label com o tempo real (relógio do PC) que a simulação
        // já ficou rodando, somando os períodos entre Iniciar e Pausar.
        private void AtualizarLabelTempoDecorrido()
        {
            lblTempoDecorrido.Text = string.Format(
                "Tempo real rodando: {0:mm\\:ss\\.ff}", cronometroSimulacao.Elapsed);
        }

        // Mostra o tempo SIMULADO (Δt x nº de ticks) — diferente do
        // lblTempoDecorrido, que mostra o tempo do relógio. É o que faltava
        // pro usuário saber quanto "tempo de física" já passou, já que um
        // único tick sozinho é um Δt pequeno demais pra perceber.
        private void AtualizarLabelTempoSimulado()
        {
            double tempoSimulado = totalTicks * motor.IntervaloTempo;
            lblTempoSimulado.Text = string.Format(
                "Tempo simulado: {0:0.00} s ({1} ticks)", tempoSimulado, totalTicks);
        }

        // Conversor K -> °C apenas para consulta; a grade continua usando
        // Kelvin internamente e não é alterada por este controle.
        private void numKelvin_ValueChanged(object sender, EventArgs e)
        {
            AtualizarConversorCelsius();
        }

        private void AtualizarConversorCelsius()
        {
            // A entrada começa em 0 K, o zero absoluto, e usa 298,15 K como
            // exemplo inicial para preservar o resultado ambiente de 25 °C.
            decimal celsius = numKelvin.Value - 273.15m;
            lblCelsiusResultado.Text = string.Format("= {0:F2} °C", celsius);
        }

        // Só atualiza o texto "Velocidade: Xx". A multiplicação em si
        // (ticksPorFrame) é lida direto de trkVelocidade.Value dentro de
        // timerSimulacao_Tick a cada tick do Timer — não depende deste evento
        // pra funcionar, ele só mantém o label sincronizado com o TrackBar.
        private void trkVelocidade_ValueChanged(object sender, EventArgs e)
        {
            AtualizarLabelVelocidade();
        }

        private void AtualizarLabelVelocidade()
        {
            lblVelocidade.Text = string.Format("Velocidade: {0}x", trkVelocidade.Value);
        }

        // =====================================================================
        // N2: processamento paralelo, gravação no MySQL e "Refazer"
        // =====================================================================

        private const double LimiarEquilibrio = 1e-4;   // mesmo limiar que a N1 usava
        private const int GravacaoACadaNTicks = 100;    // amostragem do banco, em ticks (100 x 0,01 s = 1 s simulado)

        private IRepositorioSimulacao repositorio;       // null = sem banco (o app funciona normalmente assim)
        private GravadorSimulacao gravador;              // gravação da simulação ao vivo (1 thread, criada uma vez)
        private GravadorSimulacao gravadorRefazer;       // 2º gravador: o Refazer não se mistura com a execução ao vivo
        private SessaoGravacao sessao;                   // regras de amostragem da execução em andamento
        private long cronometroCalculo;                  // ticks do Stopwatch gastos dentro de Tick()
        private bool equilibrioAtingido;
        private bool refazendo;
        private volatile string mensagemBanco = "Banco: desconectado";
        private readonly Timer timerStatusBanco = new Timer();

        private void ConfigurarBanco()
        {
            lblStatusBanco.AutoEllipsis = true;
            lblConferencia.AutoEllipsis = true;

            // O status é atualizado por um Timer próprio (500 ms): a gravação acontece em outra
            // thread, e assim a tela mostra erros e progresso sem nunca abrir MessageBox.
            timerStatusBanco.Interval = 500;
            timerStatusBanco.Tick += (s, e) => AtualizarStatusBanco();
            timerStatusBanco.Start();

            FormClosing += MainForm_FormClosing;
            chkGravarBanco.CheckedChanged += chkGravarBanco_CheckedChanged;

            // Ao abrir: tenta conectar e criar banco/tabelas, em segundo plano. Se não houver
            // MySQL ou arquivo de configuração, o app segue normal, só sem a gravação.
            ConectarBancoEmSegundoPlano();
            AtualizarControlesBanco();
        }

        // Cria o motor escolhido no checkbox. Os dois implementam IMotorSimulacao.
        private IMotorSimulacao CriarMotor(HeatGrid g)
        {
            if (chkParalelo.Checked)
                return new HeatSimulationEngineParalelo(g, IntervaloTempoSimulacao, LimiarEquilibrio);
            return new HeatSimulationEngine(g, IntervaloTempoSimulacao, LimiarEquilibrio);
        }

        private void chkParalelo_CheckedChanged(object sender, EventArgs e)
        {
            // O motor não guarda estado da simulação (só arrays de trabalho): trocar entre
            // um tick e outro é seguro e mantém a mesma grade.
            if (grade != null)
                motor = CriarMotor(grade);
        }

        private void chkGravarBanco_CheckedChanged(object sender, EventArgs e)
        {
            if (chkGravarBanco.Checked && repositorio == null)
                ConectarBancoEmSegundoPlano(); // nova tentativa se o banco estava fora do ar
        }

        // Habilita/desabilita os controles conforme o estado: não dá para começar a gravar no
        // meio de uma simulação (o Refazer parte do tick 0), nem trocar de motor durante uma gravação.
        private void AtualizarControlesBanco()
        {
            bool gravando = sessao != null && sessao.Aberta;
            chkGravarBanco.Enabled = !gravando && totalTicks == 0;
            chkParalelo.Enabled = !gravando;
            btnRefazer.Enabled = !refazendo;
        }

        private void ConectarBancoEmSegundoPlano()
        {
            mensagemBanco = "Banco: conectando...";
            Task.Run(() =>
            {
                try
                {
                    IRepositorioSimulacao repo = new RepositorioMySql(ConfiguracaoBanco.ObterStringConexao());
                    repo.GarantirEsquema();
                    IList<ResumoExecucao> lista = repo.ListarExecucoesOriginais();
                    ExecutarNaTela(() =>
                    {
                        repositorio = repo;
                        PreencherLista(lista);
                        mensagemBanco = "Banco: conectado";
                    });
                }
                catch (Exception ex)
                {
                    mensagemBanco = "Banco: desconectado (" + MensagemCurta(ex) + ")";
                }
            });
        }

        // O gravador é um só e seu contador (LinhasGravadas) acumula todas as execuções. Para o
        // status mostrar só a execução ATUAL, guardamos o valor do contador no início dela e
        // subtraímos. contagemVisivel = false esconde a contagem entre o Criar Grade e o próximo Iniciar.
        private long linhasGravadasAntesDaExecucao;
        private bool contagemVisivel;

        // Criar Grade: a execução anterior já foi fechada (FinalizarGravacao). Espera a fila
        // esvaziar para as linhas que ainda estavam sendo gravadas não serem contadas na próxima
        // execução, e volta o rótulo para "Banco: conectado", sem contagem. Só leitura do gravador.
        private void ZerarStatusBancoParaNovaGrade()
        {
            GravadorSimulacao g = gravador;
            if (g != null)
                g.EsperarEsvaziar(10000); // no máximo 10 s, como no fechamento do formulário

            contagemVisivel = false;
            if (repositorio != null)
                mensagemBanco = "Banco: conectado";
            AtualizarStatusBanco();
        }

        private void AtualizarStatusBanco()
        {
            GravadorSimulacao g = gravador;
            if (g != null && g.Falhou)
            {
                lblStatusBanco.ForeColor = Color.Firebrick;
                lblStatusBanco.Text = "Banco: ERRO - " + g.UltimoErro + " (execução incompleta; a simulação segue sem gravar)";
                return;
            }

            lblStatusBanco.ForeColor = SystemColors.ControlText;
            if (g != null && sessao != null && sessao.Aberta)
                lblStatusBanco.Text = string.Format("Banco: gravando - {0} linhas gravadas - fila: {1}", g.LinhasGravadas - linhasGravadasAntesDaExecucao, g.TamanhoDaFila);
            else if (g != null && repositorio != null && contagemVisivel)
                lblStatusBanco.Text = string.Format("Banco: conectado - {0} linhas gravadas - fila: {1}", g.LinhasGravadas - linhasGravadasAntesDaExecucao, g.TamanhoDaFila);
            else
                lblStatusBanco.Text = mensagemBanco;
        }

        // Primeiro "Iniciar" de uma grade (tick 0): grava o cenário como está AGORA, com
        // materiais, lados e temperaturas já editados pelo usuário.
        private void IniciarGravacaoSePedida()
        {
            if (!chkGravarBanco.Checked || sessao != null || totalTicks != 0)
                return;

            if (repositorio == null)
            {
                mensagemBanco = "Banco indisponível: a simulação roda sem gravar.";
                return;
            }
            if (gravador == null)
                gravador = new GravadorSimulacao(repositorio); // a thread de gravação nasce aqui, uma única vez

            HeatSimulationEngineParalelo paralelo = motor as HeatSimulationEngineParalelo;
            bool usaThreads = paralelo != null && paralelo.ExecutandoEmParalelo;
            string nome = string.Format("Cenário {0}x{0} {1:yyyy-MM-dd HH:mm:ss}", grade.Tamanho, DateTime.Now);
            DadosExecucao dados = new DadosExecucao(null, nome, null,
                usaThreads ? ModoExecucao.Paralelo : ModoExecucao.Sequencial,
                usaThreads ? paralelo.GrauParalelismo : 1,
                IntervaloTempoSimulacao, LimiarEquilibrio, GravacaoACadaNTicks);

            // Contagem do status começa do zero nesta execução (antes do Abrir, que já enfileira itens)
            linhasGravadasAntesDaExecucao = gravador.LinhasGravadas;
            contagemVisivel = true;

            sessao = new SessaoGravacao(gravador, GravacaoACadaNTicks);
            sessao.Abrir(dados, grade);
            AtualizarControlesBanco();
        }

        // Snapshot do tick atual + fim da execução (fim, ticks, ms_calculo, ms_gravacao).
        // encerrar = false no Pausar (pode retomar) e no equilíbrio; true ao criar nova grade/fechar.
        // Não espera o banco: o consumidor termina de gravar em segundo plano.
        private void FinalizarGravacao(bool equilibrio, bool encerrar)
        {
            if (sessao == null || !sessao.Aberta)
                return;

            long msCalculo = cronometroCalculo * 1000 / Stopwatch.Frequency;
            sessao.Finalizar(grade, totalTicks, equilibrioAtingido || equilibrio, msCalculo, encerrar);
            if (encerrar)
                sessao = null;
            AtualizarControlesBanco();
        }

        // Ao fechar: finaliza a execução e ESPERA a fila esvaziar (flush), no máximo 10 s,
        // para não perder as últimas leituras e nem prender o fechamento se o banco travou.
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            timerSimulacao.Stop();
            timerStatusBanco.Stop();
            FinalizarGravacao(false, true);

            if (gravador != null)
            {
                gravador.EsperarEsvaziar(10000);
                gravador.Dispose();
            }
            if (gravadorRefazer != null)
                gravadorRefazer.Dispose();
        }

        // ---------------------------------------------------------------- Refazer / conferência

        private void btnAtualizarLista_Click(object sender, EventArgs e)
        {
            IRepositorioSimulacao repo = repositorio;
            if (repo == null)
            {
                ConectarBancoEmSegundoPlano();
                return;
            }

            GravadorSimulacao g = gravador;
            Task.Run(() =>
            {
                try
                {
                    // Garante que o que acabou de ser gravado já aparece na lista
                    if (g != null)
                        g.EsperarEsvaziar(5000);
                    IList<ResumoExecucao> lista = repo.ListarExecucoesOriginais();
                    ExecutarNaTela(() => PreencherLista(lista));
                }
                catch (Exception ex)
                {
                    mensagemBanco = "Banco: erro ao listar (" + MensagemCurta(ex) + ")";
                }
            });
        }

        private void PreencherLista(IList<ResumoExecucao> lista)
        {
            ResumoExecucao anterior = cmbCenariosGravados.SelectedItem as ResumoExecucao;
            cmbCenariosGravados.Items.Clear();
            foreach (ResumoExecucao r in lista)
                cmbCenariosGravados.Items.Add(r);

            int indice = 0;
            if (anterior != null)
                for (int i = 0; i < lista.Count; i++)
                    if (lista[i].ExecucaoId == anterior.ExecucaoId) indice = i;
            if (cmbCenariosGravados.Items.Count > 0)
                cmbCenariosGravados.SelectedIndex = indice;
        }

        // Refazer: recarrega o cenário do banco, cria uma NOVA execução apontando para a original
        // (execucao_origem_id), reaplica os eventos nos mesmos ticks e roda até o tick final gravado.
        private void btnRefazer_Click(object sender, EventArgs e)
        {
            if (refazendo)
                return;

            IRepositorioSimulacao repo = repositorio;
            ResumoExecucao escolhida = cmbCenariosGravados.SelectedItem as ResumoExecucao;
            if (repo == null)
            {
                lblConferencia.Text = "Conferência: banco indisponível.";
                return;
            }
            if (escolhida == null)
            {
                lblConferencia.Text = "Conferência: selecione uma execução gravada na lista.";
                return;
            }
            if (!escolhida.Concluida)
            {
                lblConferencia.Text = "Conferência: a execução #" + escolhida.ExecucaoId + " está incompleta (fim NULL) e não pode ser refeita.";
                return;
            }

            refazendo = true;
            AtualizarControlesBanco();
            lblConferencia.Text = "Refazendo a execução #" + escolhida.ExecucaoId + " (" + escolhida.Ticks + " ticks, modo rápido)...";

            bool paralelo = chkParalelo.Checked; // lido na thread da UI, antes de ir para o segundo plano
            if (gravadorRefazer == null)
                gravadorRefazer = new GravadorSimulacao(repo);
            GravadorSimulacao grav = gravadorRefazer;
            Task.Run(() => RefazerEmSegundoPlano(repo, grav, escolhida, paralelo));
        }

        // Roda fora da thread da UI: o Refazer usa a PRÓPRIA grade e não toca nos controles
        // (só a conclusão volta para a UI, via ExecutarNaTela).
        private void RefazerEmSegundoPlano(IRepositorioSimulacao repo, GravadorSimulacao grav, ResumoExecucao escolhida, bool paralelo)
        {
            try
            {
                ExecucaoCarregada exec = repo.CarregarExecucao(escolhida.ExecucaoId);
                CenarioCarregado cenario = repo.CarregarCenario(exec.CenarioId);
                IList<EventoSimulacao> eventos = repo.CarregarEventos(exec.ExecucaoId);

                // Tela principal: uma grade PRÓPRIA (outro objeto, a conferência abaixo não é
                // afetada) no tick 0, com os eventos pendentes para o usuário ver rodar ao vivo.
                HeatGrid gradeTela = cenario.MontarGrade();
                List<EventoSimulacao> eventosTela = new List<EventoSimulacao>(eventos);
                long origemId = exec.ExecucaoId;
                ExecutarNaTela(() => InstalarGradeDoRefazer(gradeTela, eventosTela, origemId));

                HeatGrid gradeRefeita = cenario.MontarGrade();
                // Mesmo Δt e mesmo limiar da execução original (gravados no banco)
                IMotorSimulacao motorRefeito = paralelo
                    ? (IMotorSimulacao)new HeatSimulationEngineParalelo(gradeRefeita, exec.DeltaT, exec.LimiarEquilibrio)
                    : new HeatSimulationEngine(gradeRefeita, exec.DeltaT, exec.LimiarEquilibrio);
                HeatSimulationEngineParalelo p = motorRefeito as HeatSimulationEngineParalelo;
                bool usaThreads = p != null && p.ExecutandoEmParalelo;

                SessaoGravacao sessaoRefeita = new SessaoGravacao(grav, exec.GravACadaN);
                sessaoRefeita.Abrir(new DadosExecucao(cenario.CenarioId, cenario.Nome, exec.ExecucaoId,
                    usaThreads ? ModoExecucao.Paralelo : ModoExecucao.Sequencial, usaThreads ? p.GrauParalelismo : 1,
                    exec.DeltaT, exec.LimiarEquilibrio, exec.GravACadaN), gradeRefeita);

                // Roda até o tick FINAL GRAVADO, sem consultar equilíbrio: o tick em que a detecção
                // dispararia ao vivo depende da velocidade (tamanho do lote por frame).
                ResultadoExecucao resultado = ExecutorSimulacao.Executar(gradeRefeita, motorRefeito, exec.Ticks, sessaoRefeita, eventos);

                bool esvaziou = grav.EsperarEsvaziar(120000);
                if (grav.Falhou)
                    throw new InvalidOperationException("falha ao gravar a execução refeita: " + grav.UltimoErro);
                if (!esvaziou)
                    throw new InvalidOperationException("a gravação da execução refeita não terminou em 120 s");

                long novaId = grav.ExecucaoAtualId;
                ResultadoConferencia conf = repo.Conferir(novaId);
                IList<ResumoExecucao> lista = repo.ListarExecucoesOriginais();

                string texto = conf.PontosComparados == 0
                    ? "Conferência: nenhum ponto comparado (execução #" + novaId + ")."
                    : string.Format("Conferência: diferença máxima = {0:G6} K sobre {1} pontos (execução #{2} refeita a partir da #{3}, {4} ticks, {5:F0} ms de cálculo)",
                        conf.DiferencaMaximaK, conf.PontosComparados, novaId, exec.ExecucaoId, resultado.Ticks, resultado.MsCalculo);

                ExecutarNaTela(() =>
                {
                    lblConferencia.Text = texto;
                    PreencherLista(lista);
                    // A grade da tela já foi instalada no tick 0 (InstalarGradeDoRefazer) e pode até
                    // estar rodando: não é substituída pelo estado final aqui.
                    refazendo = false;
                    AtualizarControlesBanco();
                });
            }
            catch (Exception ex)
            {
                string mensagem = "Refazer falhou: " + MensagemCurta(ex);
                ExecutarNaTela(() =>
                {
                    lblConferencia.Text = mensagem;
                    refazendo = false;
                    AtualizarControlesBanco();
                });
            }
        }

        // =====================================================================
        // Reprodução (SOMENTE LEITURA do banco): mostra o estado gravado em cada tick, sem rodar
        // o motor e sem recalcular nada. Prova que as leituras bastam para reproduzir a simulação.
        // A grade ao vivo (grade/motor) NÃO é tocada: ao voltar, ela continua exatamente como estava.
        // =====================================================================

        private const int AtrasoCarregarSnapshotMs = 150;  // espera o slider parar antes de ir ao banco

        private bool emReproducao;
        private SnapshotGravado snapshotReproducao;        // fonte de dados do Paint no modo reprodução
        private ExecucaoGravada execucaoReproducao;
        private IList<long> ticksReproducao;               // ticks gravados; o slider guarda o ÍNDICE nesta lista
        private bool buscandoReproducao;                   // evita abrir duas listas ao mesmo tempo
        private int versaoReproducao;                      // descarta respostas antigas se o slider já andou
        private readonly Timer timerReproducao = new Timer();

        private void ConfigurarReproducao()
        {
            // Debounce: arrastar o slider dispara dezenas de ValueChanged por segundo; só o último
            // valor (depois de uma pausa curta) vai ao banco.
            timerReproducao.Interval = AtrasoCarregarSnapshotMs;
            timerReproducao.Tick += (s, e) =>
            {
                timerReproducao.Stop();
                CarregarSnapshotDoSlider();
            };
        }

        private void btnReproduzir_Click(object sender, EventArgs e)
        {
            if (buscandoReproducao)
                return;

            IRepositorioSimulacao repo = repositorio;
            if (repo == null)
            {
                lblReproducao.Text = "Reprodução: banco indisponível (use \"Atualizar lista\" para reconectar).";
                return;
            }

            buscandoReproducao = true;
            btnReproduzir.Enabled = false;
            lblReproducao.Text = "Reprodução: buscando execuções gravadas...";

            // Consulta em segundo plano: se o MySQL estiver fora do ar a tela não trava esperando
            Task.Run(() =>
            {
                try
                {
                    IList<ExecucaoGravada> lista = repo.ListarExecucoesComLeituras();
                    ExecutarNaTela(() => EscolherExecucaoParaReproduzir(repo, lista));
                }
                catch (Exception ex)
                {
                    string mensagem = "Reprodução: erro ao listar execuções (" + MensagemCurta(ex) + ")";
                    ExecutarNaTela(() => { lblReproducao.Text = mensagem; FimBuscaReproducao(); });
                }
            });
        }

        private void FimBuscaReproducao()
        {
            buscandoReproducao = false;
            btnReproduzir.Enabled = true;
        }

        // Mostra o diálogo com a lista; se o usuário escolher uma, carrega os ticks dela.
        private void EscolherExecucaoParaReproduzir(IRepositorioSimulacao repo, IList<ExecucaoGravada> lista)
        {
            if (lista.Count == 0)
            {
                lblReproducao.Text = "Reprodução: nenhuma execução com leituras gravadas. Marque \"Gravar no banco\" e rode uma simulação.";
                FimBuscaReproducao();
                return;
            }

            ExecucaoGravada escolhida = MostrarListaExecucoes(lista);
            if (escolhida == null)
            {
                lblReproducao.Text = emReproducao ? lblReproducao.Text : "Reprodução: escolha uma execução gravada.";
                FimBuscaReproducao();
                return;
            }

            lblReproducao.Text = "Reprodução: carregando a execução #" + escolhida.ExecucaoId + "...";
            Task.Run(() =>
            {
                try
                {
                    IList<long> ticks = repo.ListarTicksGravados(escolhida.ExecucaoId);
                    if (ticks.Count == 0)
                        throw new InvalidOperationException("a execução #" + escolhida.ExecucaoId + " não tem leituras");
                    // Já traz o primeiro snapshot, para a grade aparecer junto com o slider
                    SnapshotGravado primeiro = repo.CarregarSnapshot(escolhida.ExecucaoId, ticks[0], escolhida.Tamanho);
                    ExecutarNaTela(() => { EntrarModoReproducao(escolhida, ticks, primeiro); FimBuscaReproducao(); });
                }
                catch (Exception ex)
                {
                    string mensagem = "Reprodução: erro ao carregar (" + MensagemCurta(ex) + ")";
                    ExecutarNaTela(() => { lblReproducao.Text = mensagem; FimBuscaReproducao(); });
                }
            });
        }

        // Diálogo simples (montado em código, sem arquivo de Designer) com a lista de execuções.
        private ExecucaoGravada MostrarListaExecucoes(IList<ExecucaoGravada> lista)
        {
            using (Form dialogo = new Form())
            using (ListBox caixa = new ListBox())
            using (Button ok = new Button())
            using (Button cancelar = new Button())
            {
                dialogo.Text = "Reproduzir execução gravada";
                dialogo.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialogo.StartPosition = FormStartPosition.CenterParent;
                dialogo.MaximizeBox = false;
                dialogo.MinimizeBox = false;
                dialogo.ClientSize = new Size(560, 330);

                caixa.Location = new Point(10, 10);
                caixa.Size = new Size(540, 275);
                foreach (ExecucaoGravada x in lista)
                    caixa.Items.Add(x);
                caixa.SelectedIndex = 0;
                caixa.DoubleClick += (s, e) =>
                {
                    if (caixa.SelectedItem != null)
                        dialogo.DialogResult = DialogResult.OK;
                };

                ok.Text = "Reproduzir";
                ok.Location = new Point(380, 295);
                ok.Size = new Size(80, 26);
                ok.DialogResult = DialogResult.OK;

                cancelar.Text = "Cancelar";
                cancelar.Location = new Point(470, 295);
                cancelar.Size = new Size(80, 26);
                cancelar.DialogResult = DialogResult.Cancel;

                dialogo.Controls.Add(caixa);
                dialogo.Controls.Add(ok);
                dialogo.Controls.Add(cancelar);
                dialogo.AcceptButton = ok;
                dialogo.CancelButton = cancelar;

                return dialogo.ShowDialog(this) == DialogResult.OK ? caixa.SelectedItem as ExecucaoGravada : null;
            }
        }

        private void EntrarModoReproducao(ExecucaoGravada execucao, IList<long> ticks, SnapshotGravado primeiro)
        {
            // Se a simulação ao vivo estava rodando, pausa do mesmo jeito que o botão Pausar
            // (para fechar a gravação em curso e não deixar o Timer mexendo na grade escondida).
            if (timerSimulacao.Enabled)
                btnPausar_Click(this, EventArgs.Empty);

            timerReproducao.Stop();
            versaoReproducao++;
            emReproducao = true;
            execucaoReproducao = execucao;
            ticksReproducao = ticks;
            snapshotReproducao = primeiro;

            // O slider guarda o índice na lista de ticks gravados (não o tick em si: os ticks
            // gravados não são consecutivos). Mudar Maximum/Value dispara ValueChanged, que só
            // reagenda o carregamento; o snapshot do índice 0 já está em mãos.
            trkReproducao.Minimum = 0;
            trkReproducao.Maximum = Math.Max(0, ticks.Count - 1);
            trkReproducao.TickFrequency = Math.Max(1, ticks.Count / 20);
            trkReproducao.Value = 0;
            trkReproducao.Enabled = ticks.Count > 1;
            timerReproducao.Stop(); // o ValueChanged acima pode ter agendado um carregamento desnecessário

            linhaSelecionada = -1;
            colunaSelecionada = -1;
            LimparDicaCelula();
            lblCorpoSelecionado.Text = "Modo reprodução: somente leitura";
            lblInfoGrade.Text = string.Format("REPRODUÇÃO da execução #{0} ({1}x{1}) — dados gravados no banco",
                execucao.ExecucaoId, execucao.Tamanho);
            btnVoltarSimulacao.Enabled = true;
            AtualizarControlesSimulacao();
            AtualizarLabelReproducao(primeiro.CelulasPreenchidas < primeiro.Tamanho * primeiro.Tamanho);
            pnlGrade.Invalidate();
        }

        private void btnVoltarSimulacao_Click(object sender, EventArgs e)
        {
            SairModoReproducao();
        }

        // Também chamada pelo Refazer: a grade refeita precisa da tela no modo normal (editável),
        // senão o Paint continuaria desenhando o snapshot antigo e os botões ficariam travados.
        private void SairModoReproducao()
        {
            if (!emReproducao)
                return;

            timerReproducao.Stop();
            versaoReproducao++; // qualquer carregamento em andamento passa a ser ignorado
            emReproducao = false;
            snapshotReproducao = null;
            ticksReproducao = null;
            execucaoReproducao = null;

            trkReproducao.Enabled = false;
            btnVoltarSimulacao.Enabled = false;
            lblReproducao.Text = "Reprodução: escolha uma execução gravada.";
            LimparDicaCelula();

            // A grade e o motor ao vivo nunca foram alterados: basta voltar a desenhá-los
            if (linhaSelecionada == -1)
                lblCorpoSelecionado.Text = "Nenhum corpo selecionado";
            AtualizarInfoGrade();
            AtualizarControlesSimulacao();
            pnlGrade.Invalidate();
        }

        private void trkReproducao_ValueChanged(object sender, EventArgs e)
        {
            if (!emReproducao || ticksReproducao == null || ticksReproducao.Count == 0)
                return;

            AtualizarLabelReproducao(false);   // o rótulo acompanha o slider na hora
            timerReproducao.Stop();            // o banco só é consultado quando o slider para
            timerReproducao.Start();
        }

        private void AtualizarLabelReproducao(bool leituraIncompleta)
        {
            if (ticksReproducao == null || execucaoReproducao == null)
                return;

            int indice = Math.Max(0, Math.Min(trkReproducao.Value, ticksReproducao.Count - 1));
            long tick = ticksReproducao[indice];
            lblReproducao.Text = string.Format("Tick: {0:N0} — {1:0.00} s simulado (gravado) [{2}/{3}]{4}",
                tick, tick * execucaoReproducao.DeltaT, indice + 1, ticksReproducao.Count,
                leituraIncompleta ? " — leitura incompleta no banco" : "");
        }

        // Busca no banco o snapshot do tick escolhido no slider (em segundo plano) e redesenha.
        private void CarregarSnapshotDoSlider()
        {
            IRepositorioSimulacao repo = repositorio;
            if (!emReproducao || repo == null || ticksReproducao == null || execucaoReproducao == null)
                return;

            int indice = Math.Max(0, Math.Min(trkReproducao.Value, ticksReproducao.Count - 1));
            long tick = ticksReproducao[indice];
            long execucaoId = execucaoReproducao.ExecucaoId;
            int tamanho = execucaoReproducao.Tamanho;
            int versao = ++versaoReproducao;

            Task.Run(() =>
            {
                try
                {
                    SnapshotGravado s = repo.CarregarSnapshot(execucaoId, tick, tamanho);
                    ExecutarNaTela(() =>
                    {
                        // O usuário pode ter movido o slider de novo ou voltado à simulação enquanto isso
                        if (!emReproducao || versao != versaoReproducao)
                            return;
                        snapshotReproducao = s;
                        AtualizarLabelReproducao(s.CelulasPreenchidas < tamanho * tamanho);
                        AtualizarDicaCelula();
                        pnlGrade.Invalidate();
                    });
                }
                catch (Exception ex)
                {
                    string mensagem = "Reprodução: erro ao ler o tick " + tick + " (" + MensagemCurta(ex) + ")";
                    ExecutarNaTela(() =>
                    {
                        if (emReproducao && versao == versaoReproducao)
                            lblReproducao.Text = mensagem;
                    });
                }
            });
        }

        // Tooltip do modo reprodução: material e temperatura GRAVADOS naquele tick.
        private void AtualizarDicaReproducao()
        {
            SnapshotGravado s = snapshotReproducao;
            if (hoverLinha == -1 || s == null || hoverLinha >= s.Tamanho || hoverColuna >= s.Tamanho
                || !s.TemCelula(hoverLinha, hoverColuna))
                return;

            double temperatura = s.Temperatura(hoverLinha, hoverColuna);
            string texto = string.Format(
                "Material: {0}\nTemperatura: {1:F2} K ({2:F2} °C)\nPosição: ({3}, {4})\nTick gravado: {5:N0}",
                s.NomeMaterial(hoverLinha, hoverColuna), temperatura, temperatura - 273.15,
                hoverLinha, hoverColuna, s.Tick);

            if (texto == hoverTextoReproducao)
                return; // nada mudou: não toca no ToolTip (evita piscar)

            hoverTextoReproducao = texto;
            dicaCelula.Show(texto, pnlGrade, hoverMouseX + 16, hoverMouseY + 20);
        }

        // ---------------------------------------------------------------- Refazer ao vivo na tela

        // Eventos gravados da execução original, aplicados pelo AvancarTicks nos MESMOS ticks.
        // null = nenhum (grade criada pelo "Criar Grade").
        private IList<EventoSimulacao> eventosPendentes;
        private int proximoEventoPendente;

        // Deixa a tela como se o usuário tivesse acabado de clicar em "Criar Grade" com os valores
        // do cenário gravado: tick 0, tudo editável, Iniciar pronto para rodar com o motor escolhido.
        private void InstalarGradeDoRefazer(HeatGrid gradeCenario, IList<EventoSimulacao> eventos, long execucaoOrigemId)
        {
            SairModoReproducao();

            // Mesma sequência do btnCriarGrade_Click: para o Timer e fecha a gravação anterior
            timerSimulacao.Stop();
            cronometroSimulacao.Stop();
            FinalizarGravacao(false, true);
            ZerarStatusBancoParaNovaGrade();

            // Controles de criação refletem o cenário (só se o valor couber no controle)
            decimal n = gradeCenario.Tamanho;
            if (n >= numTamanho.Minimum && n <= numTamanho.Maximum)
                numTamanho.Value = n;
            decimal lado = (decimal)gradeCenario.ObterCorpo(0, 0).LadoCubo;
            if (lado >= numLadoCubo.Minimum && lado <= numLadoCubo.Maximum)
                numLadoCubo.Value = Math.Round(lado, numLadoCubo.DecimalPlaces);

            InstalarGrade(gradeCenario, 0); // zera eventosPendentes; definidos logo abaixo
            eventosPendentes = eventos;
            proximoEventoPendente = 0;

            lblCorpoSelecionado.Text = string.Format(
                "Refazer da execução #{0}: grade no tick 0 com {1} evento(s) gravado(s) para reaplicar. Clique em Iniciar.",
                execucaoOrigemId, eventos.Count);
        }

        private bool HaEventosPendentes()
        {
            return eventosPendentes != null && proximoEventoPendente < eventosPendentes.Count;
        }

        // Quantos ticks faltam até o próximo evento (long.MaxValue se não houver nenhum)
        private long TicksAteProximoEventoPendente()
        {
            if (!HaEventosPendentes())
                return long.MaxValue;
            return Math.Max(1, eventosPendentes[proximoEventoPendente].Tick - totalTicks);
        }

        // Aplica os eventos com tick <= tick atual, do mesmo jeito que o ExecutorSimulacao
        // (TrocarMaterial / TemperaturaAtual), e os registra na gravação ao vivo, se houver.
        private void AplicarEventosPendentesDevidos()
        {
            bool materialMudou = false;
            while (HaEventosPendentes() && eventosPendentes[proximoEventoPendente].Tick <= totalTicks)
            {
                EventoSimulacao ev = eventosPendentes[proximoEventoPendente];
                proximoEventoPendente++;

                if (ev.Tipo == TipoEvento.TrocouMaterial)
                {
                    grade.TrocarMaterial(ev.Linha, ev.Coluna, ev.Material);
                    materialMudou = true;
                }
                else
                {
                    grade.ObterCorpo(ev.Linha, ev.Coluna).TemperaturaAtual = ev.Temperatura.Value;
                }

                if (sessao != null)
                    sessao.RegistrarEvento(new EventoSimulacao(totalTicks, ev.Tipo, ev.Linha, ev.Coluna, ev.Material, ev.Temperatura));

                // Mudança imposta, não troca de calor: zera a seta da célula (igual ao clique manual)
                temperaturasAnteriores[ev.Linha, ev.Coluna] = grade.ObterCorpo(ev.Linha, ev.Coluna).TemperaturaAtual;
                tendencias[ev.Linha, ev.Coluna] = 0;
                variacoesTemperatura[ev.Linha, ev.Coluna] = 0.0;
                equilibrioAtingido = false;
            }

            if (materialMudou)
                AtualizarInfoGrade();
        }

        // Abre a tela isolada "Linha do Tempo" (só leitura do banco). Qualquer falha fica nela;
        // o try/catch garante que o formulário principal nunca é afetado.
        private void btnLinhaDoTempo_Click(object sender, EventArgs e)
        {
            try
            {
                new FormLinhaDoTempo().Show(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível abrir a linha do tempo: " + MensagemCurta(ex));
            }
        }

        // Volta para a thread da UI. Se o formulário já foi fechado, ignora (sem exceção).
        private void ExecutarNaTela(Action acao)
        {
            try
            {
                if (IsHandleCreated && !IsDisposed)
                    BeginInvoke(acao);
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        private static string MensagemCurta(Exception ex)
        {
            string m = ex is InvalidOperationException ? ex.Message : ex.GetBaseException().Message;
            m = m.Replace("\r", " ").Replace("\n", " ");
            return m.Length > 110 ? m.Substring(0, 110) + "..." : m;
        }
    }
}
