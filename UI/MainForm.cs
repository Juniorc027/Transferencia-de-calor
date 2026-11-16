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

        // Temperaturas de referência para o gradiente de cores (ajustáveis)
        private const double TemperaturaMinimaEscala = 250.0; // K
        private const double TemperaturaMaximaEscala = 1000.0; // K

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
            ConfigurarBanco();
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

            totalTicks = ticksIniciais;
            cronometroCalculo = 0;
            equilibrioAtingido = false;
            sessao = null; // nova grade = nova execução (a anterior já foi finalizada)
            AtualizarControlesBanco();
            cronometroSimulacao.Reset();
            AtualizarLabelTempoDecorrido();
            AtualizarLabelTempoSimulado();

            pnlGrade.Invalidate(); // força o redesenho do Panel
        }

        // Evento Paint do pnlGrade — desenha a grade toda vez que precisa redesenhar
        private void pnlGrade_Paint(object sender, PaintEventArgs e)
        {
            if (grade == null)
                return; // ainda não foi criada nenhuma grade

            int n = grade.Tamanho;
            int larguraCelula = pnlGrade.Width / n;
            int alturaCelula = pnlGrade.Height / n;

            for (int linha = 0; linha < n; linha++)
            {
                for (int coluna = 0; coluna < n; coluna++)
                {
                    Body corpo = grade.ObterCorpo(linha, coluna);
                    Color cor = CalcularCorPorTemperatura(corpo.TemperaturaAtual);

                    Rectangle retangulo = new Rectangle(
                        coluna * larguraCelula,
                        linha * alturaCelula,
                        larguraCelula,
                        alturaCelula
                    );

                    using (Brush pincel = new SolidBrush(cor))
                    {
                        e.Graphics.FillRectangle(pincel, retangulo);
                    }

                    // Destaca o corpo selecionado com borda mais grossa
                    // Mesma variável do tooltip (hover) quando o mouse está na grade; sem hover,
                    // mostra o bloco selecionado. Assim destaque e tooltip nunca divergem.
                    bool destacar = hoverLinha != -1
                        ? (linha == hoverLinha && coluna == hoverColuna)
                        : (linha == linhaSelecionada && coluna == colunaSelecionada);
                    Pen caneta = destacar
                        ? new Pen(Color.Yellow, 3)
                        : new Pen(Color.Black, 1);

                    e.Graphics.DrawRectangle(caneta, retangulo);
                    caneta.Dispose();

                    // Mostra a temperatura dentro da célula. Com grades pequenas (N<=10)
                    // cabe 1 casa decimal, o que ajuda a perceber variações pequenas de
                    // temperatura; em grades grandes (N>10) a célula fica estreita demais
                    // e uma casa decimal a mais estouraria o desenho, então usamos F0.
                    string formato = n <= 10 ? "F1" : "F0";
                    string texto = corpo.TemperaturaAtual.ToString(formato);
                    e.Graphics.DrawString(texto, this.Font, Brushes.Black,
                        retangulo.X + 5, retangulo.Y + 5);

                    // Seta de tendência: só lê tendencias[,], nunca recalcula
                    // aqui (quem recalcula é AtualizarTendencias, chamada só
                    // depois de um avanço de verdade da simulação).
                    sbyte tendencia = tendencias[linha, coluna];
                    if (tendencia != 0)
                    {
                        DesenharSetaTendencia(
                            e.Graphics,
                            retangulo,
                            tendencia,
                            variacoesTemperatura[linha, coluna]);
                    }
                }
            }
        }

        private void DesenharSetaTendencia(Graphics g, Rectangle celula, int tendencia, double variacao)
        {
            // A seta completa fica centralizada; laranja indica aquecimento e
            // ciano resfriamento, com contorno branco para contrastar com a grade.
            int menorDimensao = Math.Min(celula.Width, celula.Height);
            int comprimento = Math.Max(10, Math.Min(28, (int)(menorDimensao * 0.26)));
            int tamanhoPonta = Math.Max(4, comprimento / 3);
            int meiaLarguraPonta = Math.Max(3, tamanhoPonta / 2);
            int espessuraHaste = Math.Max(2, Math.Min(4, menorDimensao / 25));
            Color corTendencia = tendencia > 0
                ? Color.FromArgb(255, 140, 0)
                : Color.FromArgb(0, 220, 255);

            string textoVariacao = variacao.ToString("+0.00;-0.00;0.00") + " K";
            float tamanhoFonte = Math.Max(7f, Math.Min(9f, menorDimensao * 0.09f));

            using (Font fonteVariacao = new Font(this.Font.FontFamily, tamanhoFonte, FontStyle.Bold))
            {
                SizeF dimensoesVariacao = g.MeasureString(textoVariacao, fonteVariacao);

                // Em células pequenas, mantém a seta legível e oculta somente
                // o ΔT para que os elementos não se sobreponham.
                bool mostrarVariacao = menorDimensao >= 70 &&
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

                using (Pen contornoHaste = new Pen(Color.White, espessuraHaste + 3))
                using (Pen haste = new Pen(corTendencia, espessuraHaste))
                using (Brush preenchimento = new SolidBrush(corTendencia))
                using (Pen contornoPonta = new Pen(Color.White, 2))
                {
                    g.DrawLine(contornoHaste, inicioHaste, fimHaste);
                    g.DrawLine(haste, inicioHaste, fimHaste);
                    g.FillPolygon(preenchimento, ponta);
                    g.DrawPolygon(contornoPonta, ponta);
                }

                if (mostrarVariacao)
                {
                    float textoX = celula.X + (celula.Width - dimensoesVariacao.Width) / 2f;
                    float textoY = fimY + 4;

                    // Pequeno contorno branco mantém o valor legível sobre as
                    // duas extremidades do gradiente térmico.
                    g.DrawString(textoVariacao, fonteVariacao, Brushes.White, textoX - 1, textoY);
                    g.DrawString(textoVariacao, fonteVariacao, Brushes.White, textoX + 1, textoY);
                    g.DrawString(textoVariacao, fonteVariacao, Brushes.White, textoX, textoY - 1);
                    g.DrawString(textoVariacao, fonteVariacao, Brushes.White, textoX, textoY + 1);
                    using (Brush pincelTexto = new SolidBrush(corTendencia))
                    {
                        g.DrawString(textoVariacao, fonteVariacao, pincelTexto, textoX, textoY);
                    }
                }
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

        // Converte temperatura em cor no estilo do mapa "jet" do MATLAB:
        // azul escuro -> azul -> ciano -> verde -> amarelo -> vermelho -> vermelho escuro.
        // Cada canal é uma função linear por trechos ("triângulo") da proporção p na escala,
        // por isso não precisa de tabela: R, G e B sobem e descem em faixas deslocadas.
        private Color CalcularCorPorTemperatura(double temperatura)
        {
            double proporcao = (temperatura - TemperaturaMinimaEscala) /
                                (TemperaturaMaximaEscala - TemperaturaMinimaEscala);

            // Garante que a proporção fique entre 0 e 1, mesmo fora da escala
            proporcao = Math.Max(0.0, Math.Min(1.0, proporcao));

            return Color.FromArgb(
                CanalJet(proporcao, 3.0),   // vermelho: pico em p = 0,75
                CanalJet(proporcao, 2.0),   // verde: pico em p = 0,50
                CanalJet(proporcao, 1.0));  // azul: pico em p = 0,25
        }

        // Triângulo de largura 0,75 centrado em centro/4, limitado a [0, 1], convertido para 0..255
        private static int CanalJet(double proporcao, double centro)
        {
            double valor = 1.5 - Math.Abs(4.0 * proporcao - centro);
            valor = Math.Max(0.0, Math.Min(1.0, valor));
            return (int)Math.Round(255 * valor);
        }

        // Clique no Panel: descobre em qual célula o usuário clicou
        private void pnlGrade_MouseClick(object sender, MouseEventArgs e)
        {
            // Só o botão esquerdo aplica o pincel; ver o bloco é feito pelo mouse parado (tooltip)
            if (grade == null || e.Button != MouseButtons.Left)
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
            if (grade == null)
                return false;

            int n = grade.Tamanho;
            int larguraCelula = pnlGrade.Width / n;
            int alturaCelula = pnlGrade.Height / n;
            if (larguraCelula <= 0 || alturaCelula <= 0 || x < 0 || y < 0)
                return false;

            // Proteção contra a borda (sobra de divisão inteira): fora do índice válido
            int c = x / larguraCelula;
            int l = y / alturaCelula;
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
            dicaCelula.Hide(pnlGrade);
            pnlGrade.Invalidate(); // destaque volta a mostrar só o bloco selecionado
        }

        // Relê o estado do motor ativo (sequencial ou paralelo, via IMotorSimulacao) na thread
        // da UI. Também é chamada por AtualizarInterface(), então o texto acompanha a
        // simulação com o mouse parado, sem Timer novo.
        private void AtualizarDicaCelula()
        {
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
        }

        // Botão "Pausar": desliga o Timer
        private void btnPausar_Click(object sender, EventArgs e)
        {
            timerSimulacao.Stop();
            cronometroSimulacao.Stop();

            // Pausa = tick de gravação garantido + execução finalizada (pode ser retomada)
            FinalizarGravacao(false, false);
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
                bool gravando = sessao != null && sessao.Aberta;
                long lote = gravando ? Math.Min(restante, sessao.TicksAteProximaAmostra(totalTicks)) : restante;

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
            if (motor.AtingiuEquilibrio())
            {
                timerSimulacao.Stop();
                cronometroSimulacao.Stop();
                AtualizarInterface();
                equilibrioAtingido = true;
                FinalizarGravacao(true, false);
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
                lblStatusBanco.Text = string.Format("Banco: gravando - {0} linhas gravadas - fila: {1}", g.LinhasGravadas, g.TamanhoDaFila);
            else if (g != null && repositorio != null)
                lblStatusBanco.Text = string.Format("Banco: conectado - {0} linhas gravadas - fila: {1}", g.LinhasGravadas, g.TamanhoDaFila);
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
                    // Mostra o estado final refeito, se a tela não estiver ocupada com uma execução ao vivo
                    if ((sessao == null || !sessao.Aberta) && !timerSimulacao.Enabled)
                        InstalarGrade(gradeRefeita, resultado.Ticks);
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
