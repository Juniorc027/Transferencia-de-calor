namespace TransferenciaCalor.UI
{
    partial class MainForm
    {
        /// <summary>
        /// Variável de designer necessária.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Limpar os recursos que estão sendo usados.
        /// </summary>
        /// <param name="disposing">true se for necessário descartar os recursos gerenciados; caso contrário, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Código gerado pelo Windows Form Designer

        /// <summary>
        /// Método necessário para suporte ao Designer - não modifique
        /// o conteúdo deste método com o editor de código.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.pnlGrade = new System.Windows.Forms.Panel();
            this.lblInfoGrade = new System.Windows.Forms.Label();
            this.lblTamanho = new System.Windows.Forms.Label();
            this.numTamanho = new System.Windows.Forms.NumericUpDown();
            this.lblMaterialInicial = new System.Windows.Forms.Label();
            this.cmbMaterialInicial = new System.Windows.Forms.ComboBox();
            this.lblLadoCubo = new System.Windows.Forms.Label();
            this.numLadoCubo = new System.Windows.Forms.NumericUpDown();
            this.btnCriarGrade = new System.Windows.Forms.Button();
            this.btnIniciar = new System.Windows.Forms.Button();
            this.btnPausar = new System.Windows.Forms.Button();
            this.lblTempoDecorrido = new System.Windows.Forms.Label();
            this.lblTempoSimulado = new System.Windows.Forms.Label();
            this.lblTrocarMaterial = new System.Windows.Forms.Label();
            this.cmbTrocarMaterial = new System.Windows.Forms.ComboBox();
            this.lblTemperaturaCorpo = new System.Windows.Forms.Label();
            this.numTemperaturaCorpo = new System.Windows.Forms.NumericUpDown();
            this.btnAplicarTemperatura = new System.Windows.Forms.Button();
            this.lblCorpoSelecionado = new System.Windows.Forms.Label();
            this.lblAvisoPincel = new System.Windows.Forms.Label();
            this.lblConversorTitulo = new System.Windows.Forms.Label();
            this.numKelvin = new System.Windows.Forms.NumericUpDown();
            this.lblCelsiusResultado = new System.Windows.Forms.Label();
            this.lblVelocidade = new System.Windows.Forms.Label();
            this.trkVelocidade = new System.Windows.Forms.TrackBar();
            this.timerSimulacao = new System.Windows.Forms.Timer(this.components);
            this.chkGravarBanco = new System.Windows.Forms.CheckBox();
            this.chkParalelo = new System.Windows.Forms.CheckBox();
            this.lblStatusBanco = new System.Windows.Forms.Label();
            this.cmbCenariosGravados = new System.Windows.Forms.ComboBox();
            this.btnAtualizarLista = new System.Windows.Forms.Button();
            this.btnRefazer = new System.Windows.Forms.Button();
            this.lblConferencia = new System.Windows.Forms.Label();
            this.btnReproduzir = new System.Windows.Forms.Button();
            this.btnVoltarSimulacao = new System.Windows.Forms.Button();
            this.trkReproducao = new System.Windows.Forms.TrackBar();
            this.lblReproducao = new System.Windows.Forms.Label();
            this.btnLinhaDoTempo = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.numTamanho)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLadoCubo)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numTemperaturaCorpo)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numKelvin)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trkVelocidade)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trkReproducao)).BeginInit();
            this.SuspendLayout();
            // 
            // lblInfoGrade
            // 
            this.lblInfoGrade.AutoSize = false;
            this.lblInfoGrade.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold);
            this.lblInfoGrade.Location = new System.Drawing.Point(12, 9);
            this.lblInfoGrade.Name = "lblInfoGrade";
            this.lblInfoGrade.Size = new System.Drawing.Size(500, 17);
            this.lblInfoGrade.TabIndex = 31;
            this.lblInfoGrade.Text = "Grade: ainda não criada";
            // 
            // pnlGrade
            // 
            this.pnlGrade.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlGrade.Location = new System.Drawing.Point(12, 30);
            this.pnlGrade.Name = "pnlGrade";
            this.pnlGrade.Size = new System.Drawing.Size(500, 482);
            this.pnlGrade.TabIndex = 0;
            this.pnlGrade.Paint += new System.Windows.Forms.PaintEventHandler(this.pnlGrade_Paint);
            this.pnlGrade.MouseClick += new System.Windows.Forms.MouseEventHandler(this.pnlGrade_MouseClick);
            this.pnlGrade.MouseMove += new System.Windows.Forms.MouseEventHandler(this.pnlGrade_MouseMove);
            this.pnlGrade.MouseLeave += new System.EventHandler(this.pnlGrade_MouseLeave);
            // 
            // lblTamanho
            // 
            this.lblTamanho.AutoSize = true;
            this.lblTamanho.Location = new System.Drawing.Point(530, 15);
            this.lblTamanho.Name = "lblTamanho";
            this.lblTamanho.Size = new System.Drawing.Size(117, 13);
            this.lblTamanho.TabIndex = 1;
            this.lblTamanho.Text = "Tamanho da grade (N):";
            // 
            // numTamanho
            // 
            this.numTamanho.Location = new System.Drawing.Point(533, 32);
            this.numTamanho.Maximum = new decimal(new int[] {
            20,
            0,
            0,
            0});
            this.numTamanho.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numTamanho.Name = "numTamanho";
            this.numTamanho.Size = new System.Drawing.Size(80, 20);
            this.numTamanho.TabIndex = 2;
            this.numTamanho.Value = new decimal(new int[] {
            3,
            0,
            0,
            0});
            // 
            // lblMaterialInicial
            // 
            this.lblMaterialInicial.AutoSize = true;
            this.lblMaterialInicial.Location = new System.Drawing.Point(530, 65);
            this.lblMaterialInicial.Name = "lblMaterialInicial";
            this.lblMaterialInicial.Size = new System.Drawing.Size(76, 13);
            this.lblMaterialInicial.TabIndex = 3;
            this.lblMaterialInicial.Text = "Material inicial:";
            // 
            // cmbMaterialInicial
            // 
            this.cmbMaterialInicial.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbMaterialInicial.FormattingEnabled = true;
            this.cmbMaterialInicial.Location = new System.Drawing.Point(533, 81);
            this.cmbMaterialInicial.Name = "cmbMaterialInicial";
            this.cmbMaterialInicial.Size = new System.Drawing.Size(180, 21);
            this.cmbMaterialInicial.TabIndex = 4;
            // 
            // lblLadoCubo
            // 
            this.lblLadoCubo.AutoSize = true;
            this.lblLadoCubo.Location = new System.Drawing.Point(530, 118);
            this.lblLadoCubo.Name = "lblLadoCubo";
            this.lblLadoCubo.Size = new System.Drawing.Size(93, 13);
            this.lblLadoCubo.TabIndex = 5;
            this.lblLadoCubo.Text = "Lado do cubo (m):";
            // 
            // numLadoCubo
            // 
            this.numLadoCubo.DecimalPlaces = 3;
            this.numLadoCubo.Increment = new decimal(new int[] {
            1,
            0,
            0,
            131072});
            this.numLadoCubo.Location = new System.Drawing.Point(533, 135);
            this.numLadoCubo.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.numLadoCubo.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            196608});
            this.numLadoCubo.Name = "numLadoCubo";
            this.numLadoCubo.Size = new System.Drawing.Size(80, 20);
            this.numLadoCubo.TabIndex = 6;
            this.numLadoCubo.Value = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            // 
            // btnCriarGrade
            // 
            this.btnCriarGrade.Location = new System.Drawing.Point(533, 168);
            this.btnCriarGrade.Name = "btnCriarGrade";
            this.btnCriarGrade.Size = new System.Drawing.Size(180, 30);
            this.btnCriarGrade.TabIndex = 7;
            this.btnCriarGrade.Text = "Criar Grade";
            this.btnCriarGrade.UseVisualStyleBackColor = true;
            this.btnCriarGrade.Click += new System.EventHandler(this.btnCriarGrade_Click);
            // 
            // btnIniciar
            // 
            this.btnIniciar.Location = new System.Drawing.Point(533, 210);
            this.btnIniciar.Name = "btnIniciar";
            this.btnIniciar.Size = new System.Drawing.Size(85, 30);
            this.btnIniciar.TabIndex = 8;
            this.btnIniciar.Text = "Iniciar";
            this.btnIniciar.UseVisualStyleBackColor = true;
            this.btnIniciar.Click += new System.EventHandler(this.btnIniciar_Click);
            // 
            // btnPausar
            // 
            this.btnPausar.Location = new System.Drawing.Point(628, 210);
            this.btnPausar.Name = "btnPausar";
            this.btnPausar.Size = new System.Drawing.Size(85, 30);
            this.btnPausar.TabIndex = 9;
            this.btnPausar.Text = "Pausar";
            this.btnPausar.UseVisualStyleBackColor = true;
            this.btnPausar.Click += new System.EventHandler(this.btnPausar_Click);
            // 
            // lblTempoDecorrido
            //
            this.lblTempoDecorrido.AutoSize = true;
            this.lblTempoDecorrido.Location = new System.Drawing.Point(530, 250);
            this.lblTempoDecorrido.Name = "lblTempoDecorrido";
            this.lblTempoDecorrido.Size = new System.Drawing.Size(150, 13);
            this.lblTempoDecorrido.TabIndex = 11;
            this.lblTempoDecorrido.Text = "Tempo real rodando: 00:00.00";
            //
            // lblTempoSimulado
            //
            this.lblTempoSimulado.AutoSize = true;
            this.lblTempoSimulado.Location = new System.Drawing.Point(530, 267);
            this.lblTempoSimulado.Name = "lblTempoSimulado";
            this.lblTempoSimulado.Size = new System.Drawing.Size(180, 13);
            this.lblTempoSimulado.TabIndex = 18;
            this.lblTempoSimulado.Text = "Tempo simulado: 0,00 s (0 ticks)";
            //
            // lblTrocarMaterial
            //
            this.lblTrocarMaterial.AutoSize = true;
            this.lblTrocarMaterial.Location = new System.Drawing.Point(530, 290);
            this.lblTrocarMaterial.Name = "lblTrocarMaterial";
            this.lblTrocarMaterial.Size = new System.Drawing.Size(185, 13);
            this.lblTrocarMaterial.TabIndex = 12;
            this.lblTrocarMaterial.Text = "Trocar material do corpo selecionado:";
            //
            // cmbTrocarMaterial
            //
            this.cmbTrocarMaterial.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbTrocarMaterial.FormattingEnabled = true;
            this.cmbTrocarMaterial.Location = new System.Drawing.Point(533, 307);
            this.cmbTrocarMaterial.Name = "cmbTrocarMaterial";
            this.cmbTrocarMaterial.Size = new System.Drawing.Size(180, 21);
            this.cmbTrocarMaterial.TabIndex = 13;
            this.cmbTrocarMaterial.SelectedIndexChanged += new System.EventHandler(this.cmbTrocarMaterial_SelectedIndexChanged);
            //
            // lblTemperaturaCorpo
            //
            this.lblTemperaturaCorpo.AutoSize = true;
            this.lblTemperaturaCorpo.Location = new System.Drawing.Point(530, 340);
            this.lblTemperaturaCorpo.Name = "lblTemperaturaCorpo";
            this.lblTemperaturaCorpo.Size = new System.Drawing.Size(191, 13);
            this.lblTemperaturaCorpo.TabIndex = 14;
            this.lblTemperaturaCorpo.Text = "Temperatura do corpo selecionado (K):";
            //
            // numTemperaturaCorpo
            //
            this.numTemperaturaCorpo.DecimalPlaces = 2;
            this.numTemperaturaCorpo.Increment = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.numTemperaturaCorpo.Location = new System.Drawing.Point(533, 357);
            this.numTemperaturaCorpo.Maximum = new decimal(new int[] {
            5000,
            0,
            0,
            0});
            this.numTemperaturaCorpo.Name = "numTemperaturaCorpo";
            this.numTemperaturaCorpo.Size = new System.Drawing.Size(100, 20);
            this.numTemperaturaCorpo.TabIndex = 15;
            this.numTemperaturaCorpo.Value = new decimal(new int[] {
            300,
            0,
            0,
            0});
            this.numTemperaturaCorpo.ValueChanged += new System.EventHandler(this.numTemperaturaCorpo_ValueChanged);
            //
            // btnAplicarTemperatura
            //
            this.btnAplicarTemperatura.Location = new System.Drawing.Point(639, 355);
            this.btnAplicarTemperatura.Name = "btnAplicarTemperatura";
            this.btnAplicarTemperatura.Size = new System.Drawing.Size(74, 24);
            this.btnAplicarTemperatura.TabIndex = 16;
            this.btnAplicarTemperatura.Text = "Aplicar";
            this.btnAplicarTemperatura.UseVisualStyleBackColor = true;
            this.btnAplicarTemperatura.Click += new System.EventHandler(this.btnAplicarTemperatura_Click);
            //
            // lblCorpoSelecionado
            //
            this.lblCorpoSelecionado.Location = new System.Drawing.Point(533, 392);
            this.lblCorpoSelecionado.Name = "lblCorpoSelecionado";
            this.lblCorpoSelecionado.Size = new System.Drawing.Size(340, 60);
            this.lblCorpoSelecionado.TabIndex = 17;
            this.lblCorpoSelecionado.Text = "Nenhum corpo selecionado";
            //
            // lblAvisoPincel (aviso não bloqueante do pincel)
            //
            this.lblAvisoPincel.AutoSize = false;
            this.lblAvisoPincel.ForeColor = System.Drawing.Color.Firebrick;
            this.lblAvisoPincel.Location = new System.Drawing.Point(533, 378);
            this.lblAvisoPincel.Name = "lblAvisoPincel";
            this.lblAvisoPincel.Size = new System.Drawing.Size(340, 14);
            this.lblAvisoPincel.TabIndex = 40;
            //
            // lblConversorTitulo
            //
            this.lblConversorTitulo.AutoSize = true;
            this.lblConversorTitulo.Location = new System.Drawing.Point(530, 462);
            this.lblConversorTitulo.Name = "lblConversorTitulo";
            this.lblConversorTitulo.Size = new System.Drawing.Size(190, 13);
            this.lblConversorTitulo.TabIndex = 19;
            this.lblConversorTitulo.Text = "Conversor K para °C (só consulta):";
            //
            // numKelvin
            //
            this.numKelvin.DecimalPlaces = 2;
            this.numKelvin.Location = new System.Drawing.Point(533, 479);
            this.numKelvin.Maximum = 10000m;
            this.numKelvin.Name = "numKelvin";
            this.numKelvin.Size = new System.Drawing.Size(80, 20);
            this.numKelvin.TabIndex = 20;
            this.numKelvin.Value = 298.15m;
            this.numKelvin.ValueChanged += new System.EventHandler(this.numKelvin_ValueChanged);
            //
            // lblCelsiusResultado
            //
            this.lblCelsiusResultado.AutoSize = true;
            this.lblCelsiusResultado.Location = new System.Drawing.Point(625, 481);
            this.lblCelsiusResultado.Name = "lblCelsiusResultado";
            this.lblCelsiusResultado.Size = new System.Drawing.Size(80, 13);
            this.lblCelsiusResultado.TabIndex = 21;
            this.lblCelsiusResultado.Text = "= 25,00 °C";
            //
            // lblVelocidade
            //
            this.lblVelocidade.AutoSize = true;
            this.lblVelocidade.Location = new System.Drawing.Point(530, 512);
            this.lblVelocidade.Name = "lblVelocidade";
            this.lblVelocidade.Size = new System.Drawing.Size(80, 13);
            this.lblVelocidade.TabIndex = 22;
            this.lblVelocidade.Text = "Velocidade: 1x";
            //
            // trkVelocidade
            //
            this.trkVelocidade.Location = new System.Drawing.Point(533, 529);
            this.trkVelocidade.Maximum = 10;
            this.trkVelocidade.Minimum = 1;
            this.trkVelocidade.Name = "trkVelocidade";
            this.trkVelocidade.Size = new System.Drawing.Size(180, 45);
            this.trkVelocidade.TabIndex = 23;
            this.trkVelocidade.TickFrequency = 1;
            this.trkVelocidade.Value = 1;
            this.trkVelocidade.ValueChanged += new System.EventHandler(this.trkVelocidade_ValueChanged);
            // 
            // chkGravarBanco
            // 
            this.chkGravarBanco.AutoSize = true;
            this.chkGravarBanco.Location = new System.Drawing.Point(12, 520);
            this.chkGravarBanco.Name = "chkGravarBanco";
            this.chkGravarBanco.Size = new System.Drawing.Size(105, 17);
            this.chkGravarBanco.TabIndex = 24;
            this.chkGravarBanco.Text = "Gravar no banco";
            this.chkGravarBanco.UseVisualStyleBackColor = true;
            // 
            // chkParalelo
            // 
            this.chkParalelo.AutoSize = true;
            this.chkParalelo.Location = new System.Drawing.Point(160, 520);
            this.chkParalelo.Name = "chkParalelo";
            this.chkParalelo.Size = new System.Drawing.Size(138, 17);
            this.chkParalelo.TabIndex = 25;
            this.chkParalelo.Text = "Processamento paralelo";
            this.chkParalelo.UseVisualStyleBackColor = true;
            this.chkParalelo.CheckedChanged += new System.EventHandler(this.chkParalelo_CheckedChanged);
            // 
            // lblStatusBanco
            // 
            this.lblStatusBanco.Location = new System.Drawing.Point(12, 546);
            this.lblStatusBanco.Name = "lblStatusBanco";
            this.lblStatusBanco.Size = new System.Drawing.Size(500, 16);
            this.lblStatusBanco.TabIndex = 26;
            this.lblStatusBanco.Text = "Banco: desconectado";
            // 
            // cmbCenariosGravados
            // 
            this.cmbCenariosGravados.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCenariosGravados.FormattingEnabled = true;
            this.cmbCenariosGravados.Location = new System.Drawing.Point(12, 568);
            this.cmbCenariosGravados.Name = "cmbCenariosGravados";
            this.cmbCenariosGravados.Size = new System.Drawing.Size(300, 21);
            this.cmbCenariosGravados.TabIndex = 27;
            // 
            // btnAtualizarLista
            // 
            this.btnAtualizarLista.Location = new System.Drawing.Point(318, 566);
            this.btnAtualizarLista.Name = "btnAtualizarLista";
            this.btnAtualizarLista.Size = new System.Drawing.Size(90, 25);
            this.btnAtualizarLista.TabIndex = 28;
            this.btnAtualizarLista.Text = "Atualizar lista";
            this.btnAtualizarLista.UseVisualStyleBackColor = true;
            this.btnAtualizarLista.Click += new System.EventHandler(this.btnAtualizarLista_Click);
            // 
            // btnRefazer
            // 
            this.btnRefazer.Location = new System.Drawing.Point(414, 566);
            this.btnRefazer.Name = "btnRefazer";
            this.btnRefazer.Size = new System.Drawing.Size(98, 25);
            this.btnRefazer.TabIndex = 29;
            this.btnRefazer.Text = "Refazer";
            this.btnRefazer.UseVisualStyleBackColor = true;
            this.btnRefazer.Click += new System.EventHandler(this.btnRefazer_Click);
            // 
            // lblConferencia
            // 
            this.lblConferencia.Location = new System.Drawing.Point(12, 598);
            this.lblConferencia.Name = "lblConferencia";
            this.lblConferencia.Size = new System.Drawing.Size(500, 34);
            this.lblConferencia.TabIndex = 30;
            this.lblConferencia.Text = "Conferência: (refaça uma execução gravada para comparar)";
            //
            // btnReproduzir (abre a lista de execuções gravadas)
            //
            this.btnReproduzir.Location = new System.Drawing.Point(533, 580);
            this.btnReproduzir.Name = "btnReproduzir";
            this.btnReproduzir.Size = new System.Drawing.Size(170, 25);
            this.btnReproduzir.TabIndex = 41;
            this.btnReproduzir.Text = "Reproduzir execução...";
            this.btnReproduzir.UseVisualStyleBackColor = true;
            this.btnReproduzir.Click += new System.EventHandler(this.btnReproduzir_Click);
            //
            // btnVoltarSimulacao (sai do modo reprodução)
            //
            this.btnVoltarSimulacao.Enabled = false;
            this.btnVoltarSimulacao.Location = new System.Drawing.Point(709, 580);
            this.btnVoltarSimulacao.Name = "btnVoltarSimulacao";
            this.btnVoltarSimulacao.Size = new System.Drawing.Size(164, 25);
            this.btnVoltarSimulacao.TabIndex = 42;
            this.btnVoltarSimulacao.Text = "Voltar à simulação";
            this.btnVoltarSimulacao.UseVisualStyleBackColor = true;
            this.btnVoltarSimulacao.Click += new System.EventHandler(this.btnVoltarSimulacao_Click);
            //
            // trkReproducao (índice dentro da lista de ticks gravados)
            //
            this.trkReproducao.Enabled = false;
            this.trkReproducao.Location = new System.Drawing.Point(533, 610);
            this.trkReproducao.Name = "trkReproducao";
            this.trkReproducao.Size = new System.Drawing.Size(340, 45);
            this.trkReproducao.TabIndex = 43;
            this.trkReproducao.TickStyle = System.Windows.Forms.TickStyle.None;
            this.trkReproducao.ValueChanged += new System.EventHandler(this.trkReproducao_ValueChanged);
            //
            // lblReproducao
            //
            this.lblReproducao.Location = new System.Drawing.Point(533, 656);
            this.lblReproducao.Name = "lblReproducao";
            this.lblReproducao.Size = new System.Drawing.Size(340, 32);
            this.lblReproducao.TabIndex = 44;
            this.lblReproducao.Text = "Reprodução: escolha uma execução gravada.";
            //
            // btnLinhaDoTempo (abre a tela isolada FormLinhaDoTempo)
            //
            this.btnLinhaDoTempo.Location = new System.Drawing.Point(533, 580);
            this.btnLinhaDoTempo.Name = "btnLinhaDoTempo";
            this.btnLinhaDoTempo.Size = new System.Drawing.Size(180, 25);
            this.btnLinhaDoTempo.TabIndex = 45;
            this.btnLinhaDoTempo.Text = "Ver linha do tempo";
            this.btnLinhaDoTempo.UseVisualStyleBackColor = true;
            this.btnLinhaDoTempo.Click += new System.EventHandler(this.btnLinhaDoTempo_Click);
            //
            // timerSimulacao
            // 
            this.timerSimulacao.Interval = 50;
            this.timerSimulacao.Tick += new System.EventHandler(this.timerSimulacao_Tick);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(884, 696);
            this.Controls.Add(this.btnLinhaDoTempo);
            this.Controls.Add(this.lblReproducao);
            this.Controls.Add(this.trkReproducao);
            this.Controls.Add(this.btnVoltarSimulacao);
            this.Controls.Add(this.btnReproduzir);
            this.Controls.Add(this.lblConferencia);
            this.Controls.Add(this.btnRefazer);
            this.Controls.Add(this.btnAtualizarLista);
            this.Controls.Add(this.cmbCenariosGravados);
            this.Controls.Add(this.lblStatusBanco);
            this.Controls.Add(this.chkParalelo);
            this.Controls.Add(this.chkGravarBanco);
            this.Controls.Add(this.trkVelocidade);
            this.Controls.Add(this.lblVelocidade);
            this.Controls.Add(this.lblCelsiusResultado);
            this.Controls.Add(this.numKelvin);
            this.Controls.Add(this.lblConversorTitulo);
            this.Controls.Add(this.lblAvisoPincel);
            this.Controls.Add(this.lblCorpoSelecionado);
            this.Controls.Add(this.btnAplicarTemperatura);
            this.Controls.Add(this.numTemperaturaCorpo);
            this.Controls.Add(this.lblTemperaturaCorpo);
            this.Controls.Add(this.cmbTrocarMaterial);
            this.Controls.Add(this.lblTrocarMaterial);
            this.Controls.Add(this.lblTempoSimulado);
            this.Controls.Add(this.lblTempoDecorrido);
            this.Controls.Add(this.btnPausar);
            this.Controls.Add(this.btnIniciar);
            this.Controls.Add(this.btnCriarGrade);
            this.Controls.Add(this.numLadoCubo);
            this.Controls.Add(this.lblLadoCubo);
            this.Controls.Add(this.cmbMaterialInicial);
            this.Controls.Add(this.lblMaterialInicial);
            this.Controls.Add(this.numTamanho);
            this.Controls.Add(this.lblTamanho);
            this.Controls.Add(this.pnlGrade);
            this.Controls.Add(this.lblInfoGrade);
            this.Name = "MainForm";
            this.Text = "Simulador de Transferência de Calor";
            this.Load += new System.EventHandler(this.MainForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.numTamanho)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLadoCubo)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numTemperaturaCorpo)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numKelvin)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trkVelocidade)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trkReproducao)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel pnlGrade;
        private System.Windows.Forms.Label lblInfoGrade;
        private System.Windows.Forms.Label lblTamanho;
        private System.Windows.Forms.NumericUpDown numTamanho;
        private System.Windows.Forms.Label lblMaterialInicial;
        private System.Windows.Forms.ComboBox cmbMaterialInicial;
        private System.Windows.Forms.Label lblLadoCubo;
        private System.Windows.Forms.NumericUpDown numLadoCubo;
        private System.Windows.Forms.Button btnCriarGrade;
        private System.Windows.Forms.Button btnIniciar;
        private System.Windows.Forms.Button btnPausar;
        private System.Windows.Forms.Label lblTempoDecorrido;
        private System.Windows.Forms.Label lblTempoSimulado;
        private System.Windows.Forms.Label lblTrocarMaterial;
        private System.Windows.Forms.ComboBox cmbTrocarMaterial;
        private System.Windows.Forms.Label lblTemperaturaCorpo;
        private System.Windows.Forms.NumericUpDown numTemperaturaCorpo;
        private System.Windows.Forms.Button btnAplicarTemperatura;
        private System.Windows.Forms.Label lblCorpoSelecionado;
        private System.Windows.Forms.Label lblAvisoPincel;
        private System.Windows.Forms.Label lblConversorTitulo;
        private System.Windows.Forms.NumericUpDown numKelvin;
        private System.Windows.Forms.Label lblCelsiusResultado;
        private System.Windows.Forms.Label lblVelocidade;
        private System.Windows.Forms.TrackBar trkVelocidade;
        private System.Windows.Forms.Timer timerSimulacao;
        private System.Windows.Forms.CheckBox chkGravarBanco;
        private System.Windows.Forms.CheckBox chkParalelo;
        private System.Windows.Forms.Label lblStatusBanco;
        private System.Windows.Forms.ComboBox cmbCenariosGravados;
        private System.Windows.Forms.Button btnAtualizarLista;
        private System.Windows.Forms.Button btnRefazer;
        private System.Windows.Forms.Label lblConferencia;
        private System.Windows.Forms.Button btnReproduzir;
        private System.Windows.Forms.Button btnVoltarSimulacao;
        private System.Windows.Forms.TrackBar trkReproducao;
        private System.Windows.Forms.Label lblReproducao;
        private System.Windows.Forms.Button btnLinhaDoTempo;
    }
}
