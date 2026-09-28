using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using TransferenciaCalor.Data;
using TransferenciaCalor.Models;
using TransferenciaCalor.Simulation;

namespace TransferenciaCalor.UI
{
    public partial class MainForm : Form
    {
        // Atributos privados — encapsulamento
        private HeatSimulationEngine motor;
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

        // Quantos ticks o botão "Step" aplica de uma vez. Um tick sozinho
        // (Δt = 0,01 s) muda a temperatura na casa de 1e-6 a 1e-5 K —
        // imperceptível na tela — então agrupamos vários ticks por clique.
        private const int TicksPorPasso = 100;

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

        // Diferença de temperatura (K) considerada "ruído" e não uma tendência
        // real. Sem essa tolerância, o próprio arredondamento de ponto
        // flutuante entre ticks faria a seta ficar piscando pra cima/baixo
        // mesmo com a grade praticamente parada.
        private const double ToleranciaTendencia = 1e-7; // K

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

            int n = (int)numTamanho.Value;
            Material materialInicial = (Material)cmbMaterialInicial.SelectedItem;

            if (materialInicial == null)
            {
                MessageBox.Show("Selecione um material inicial.");
                return;
            }

            double ladoCubo = (double)numLadoCubo.Value; // informado pelo usuário, em metros (SI)
            double temperaturaInicial = 300.0; // 300K (~27°C)

            grade = new HeatGrid(n, materialInicial, ladoCubo, temperaturaInicial);
            motor = new HeatSimulationEngine(grade, intervaloTempo: IntervaloTempoSimulacao, limiarEquilibrio: 1e-4);

            // Sem histórico ainda: nenhuma célula tem seta até o primeiro
            // avanço da simulação (tendencias[,] já nasce zerada — sbyte
            // padrão é 0 — só precisamos guardar a temperatura de partida).
            temperaturasAnteriores = new double[n, n];
            tendencias = new sbyte[n, n];
            for (int linha = 0; linha < n; linha++)
            {
                for (int coluna = 0; coluna < n; coluna++)
                {
                    temperaturasAnteriores[linha, coluna] = grade.ObterCorpo(linha, coluna).TemperaturaAtual;
                }
            }

            linhaSelecionada = -1;
            colunaSelecionada = -1;
            lblCorpoSelecionado.Text = "Nenhum corpo selecionado";

            totalTicks = 0;
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
                    Pen caneta = (linha == linhaSelecionada && coluna == colunaSelecionada)
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
                        SizeF tamanhoTexto = e.Graphics.MeasureString(texto, this.Font);
                        DesenharSetaTendencia(e.Graphics, retangulo, tamanhoTexto, tendencia);
                    }
                }
            }
        }

        // Desenha a seta de tendência como um triângulo preenchido — não uso
        // caractere Unicode de seta (▲/▼) em DrawString porque o glifo depende
        // da fonte instalada e às vezes vira um quadrado.
        private void DesenharSetaTendencia(Graphics g, Rectangle celula, SizeF tamanhoTexto, int tendencia)
        {
            // Tamanho proporcional à célula (~20% da menor dimensão), com um
            // mínimo pra não desaparecer em grades grandes (N alto).
            int menorDimensao = Math.Min(celula.Width, celula.Height);
            int tamanhoSeta = Math.Max(6, (int)(menorDimensao * 0.2));
            int metade = tamanhoSeta / 2;
            const int margem = 5; // mesma margem usada pro texto (retangulo.X/Y + 5)

            int fimTextoX = celula.X + margem + (int)Math.Ceiling(tamanhoTexto.Width);
            Point centro;

            if (fimTextoX + 4 + tamanhoSeta < celula.Right - 2)
            {
                // Cabe ao lado do número, na mesma linha.
                centro = new Point(fimTextoX + 4 + metade, celula.Y + margem + (int)(tamanhoTexto.Height / 2));
            }
            else
            {
                // Não sobra espaço horizontal (grade grande, célula estreita):
                // desenha embaixo do número, sempre dentro da célula.
                int y = celula.Y + margem + (int)tamanhoTexto.Height + 4;
                if (y + metade > celula.Bottom - 2)
                    y = celula.Bottom - 2 - metade; // garante que não sai da célula
                centro = new Point(celula.X + margem + metade, y);
            }

            Point[] pontos = tendencia > 0
                ? new Point[] // aponta pra cima
                {
                    new Point(centro.X, centro.Y - metade),
                    new Point(centro.X - metade, centro.Y + metade),
                    new Point(centro.X + metade, centro.Y + metade)
                }
                : new Point[] // aponta pra baixo
                {
                    new Point(centro.X, centro.Y + metade),
                    new Point(centro.X - metade, centro.Y - metade),
                    new Point(centro.X + metade, centro.Y - metade)
                };

            // Preenchimento branco com contorno preto pra contrastar tanto
            // com o azul quanto com o vermelho do gradiente de temperatura.
            g.FillPolygon(Brushes.White, pontos);
            using (Pen contorno = new Pen(Color.Black, 1))
            {
                g.DrawPolygon(contorno, pontos);
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

                    temperaturasAnteriores[linha, coluna] = atual;
                }
            }
        }

        // Converte temperatura em cor (azul = frio, vermelho = quente),
        // interpolando linearmente entre os dois extremos da escala.
        private Color CalcularCorPorTemperatura(double temperatura)
        {
            double proporcao = (temperatura - TemperaturaMinimaEscala) /
                                (TemperaturaMaximaEscala - TemperaturaMinimaEscala);

            // Garante que a proporção fique entre 0 e 1, mesmo fora da escala
            proporcao = Math.Max(0.0, Math.Min(1.0, proporcao));

            int vermelho = (int)(255 * proporcao);
            int azul = (int)(255 * (1 - proporcao));

            return Color.FromArgb(vermelho, 0, azul);
        }

        // Clique no Panel: descobre em qual célula o usuário clicou
        private void pnlGrade_MouseClick(object sender, MouseEventArgs e)
        {
            if (grade == null)
                return;

            int n = grade.Tamanho;
            int larguraCelula = pnlGrade.Width / n;
            int alturaCelula = pnlGrade.Height / n;

            int colunaClicada = e.X / larguraCelula;
            int linhaClicada = e.Y / alturaCelula;

            // Proteção contra clique na borda (fora do índice válido)
            if (linhaClicada >= n || colunaClicada >= n)
                return;

            linhaSelecionada = linhaClicada;
            colunaSelecionada = colunaClicada;

            Body corpoSelecionado = grade.ObterCorpo(linhaSelecionada, colunaSelecionada);
            AtualizarLabelCorpoSelecionado(corpoSelecionado);
            numTemperaturaCorpo.Value = (decimal)corpoSelecionado.TemperaturaAtual;

            pnlGrade.Invalidate(); // redesenha pra mostrar a borda amarela de seleção
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

            // Mudança manual do usuário, não é troca de calor — não é uma
            // "tendência" real da simulação, então zera a seta dessa célula e
            // atualiza a referência, senão o próximo tick compararia com a
            // temperatura antiga e mostraria uma seta falsa.
            temperaturasAnteriores[linhaSelecionada, colunaSelecionada] = corpo.TemperaturaAtual;
            tendencias[linhaSelecionada, colunaSelecionada] = 0;

            AtualizarLabelCorpoSelecionado(corpo);

            pnlGrade.Invalidate();
        }

        // Troca o material do corpo selecionado
        private void cmbTrocarMaterial_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (grade == null || linhaSelecionada == -1)
                return;

            Material novoMaterial = (Material)cmbTrocarMaterial.SelectedItem;
            if (novoMaterial == null)
                return; // combo ainda sem nenhuma seleção — nada a trocar

            grade.TrocarMaterial(linhaSelecionada, colunaSelecionada, novoMaterial);
            // Sem isso o label ficava mostrando o material antigo até o próximo clique na grade.
            AtualizarLabelCorpoSelecionado(grade.ObterCorpo(linhaSelecionada, colunaSelecionada));

            pnlGrade.Invalidate();
        }

        // Botão "Iniciar": liga o Timer
        private void btnIniciar_Click(object sender, EventArgs e)
        {
            if (motor == null)
            {
                MessageBox.Show("Crie a grade primeiro.");
                return;
            }

            timerSimulacao.Start();
            cronometroSimulacao.Start(); // Start() retoma de onde parou, sem zerar
        }

        // Botão "Pausar": desliga o Timer
        private void btnPausar_Click(object sender, EventArgs e)
        {
            timerSimulacao.Stop();
            cronometroSimulacao.Stop();
        }

        // Botão "Step": avança TicksPorPasso ticks manualmente (útil pra debug).
        // Um tick sozinho mudava a temperatura de forma imperceptível na tela;
        // agrupar 100 ticks torna o efeito de cada clique visível.
        // Não multiplica por trkVelocidade.Value de propósito: a velocidade é
        // um recurso do Timer (simulação automática); o Step é uma ferramenta
        // de depuração à parte, com passo fixo e previsível.
        private void btnStep_Click(object sender, EventArgs e)
        {
            if (motor == null)
                return;

            AvancarTicks(TicksPorPasso);
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
            for (int i = 0; i < quantidade; i++)
            {
                motor.Tick();
                totalTicks++;
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
    }
}
