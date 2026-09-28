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
            this.lblTamanho = new System.Windows.Forms.Label();
            this.numTamanho = new System.Windows.Forms.NumericUpDown();
            this.lblMaterialInicial = new System.Windows.Forms.Label();
            this.cmbMaterialInicial = new System.Windows.Forms.ComboBox();
            this.lblLadoCubo = new System.Windows.Forms.Label();
            this.numLadoCubo = new System.Windows.Forms.NumericUpDown();
            this.btnCriarGrade = new System.Windows.Forms.Button();
            this.btnIniciar = new System.Windows.Forms.Button();
            this.btnPausar = new System.Windows.Forms.Button();
            this.btnStep = new System.Windows.Forms.Button();
            this.lblTempoDecorrido = new System.Windows.Forms.Label();
            this.lblTempoSimulado = new System.Windows.Forms.Label();
            this.lblTrocarMaterial = new System.Windows.Forms.Label();
            this.cmbTrocarMaterial = new System.Windows.Forms.ComboBox();
            this.lblTemperaturaCorpo = new System.Windows.Forms.Label();
            this.numTemperaturaCorpo = new System.Windows.Forms.NumericUpDown();
            this.btnAplicarTemperatura = new System.Windows.Forms.Button();
            this.lblCorpoSelecionado = new System.Windows.Forms.Label();
            this.lblConversorTitulo = new System.Windows.Forms.Label();
            this.numCelsius = new System.Windows.Forms.NumericUpDown();
            this.lblKelvinResultado = new System.Windows.Forms.Label();
            this.lblVelocidade = new System.Windows.Forms.Label();
            this.trkVelocidade = new System.Windows.Forms.TrackBar();
            this.timerSimulacao = new System.Windows.Forms.Timer(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.numTamanho)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLadoCubo)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numTemperaturaCorpo)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numCelsius)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trkVelocidade)).BeginInit();
            this.SuspendLayout();
            // 
            // pnlGrade
            // 
            this.pnlGrade.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlGrade.Location = new System.Drawing.Point(12, 12);
            this.pnlGrade.Name = "pnlGrade";
            this.pnlGrade.Size = new System.Drawing.Size(500, 500);
            this.pnlGrade.TabIndex = 0;
            this.pnlGrade.Paint += new System.Windows.Forms.PaintEventHandler(this.pnlGrade_Paint);
            this.pnlGrade.MouseClick += new System.Windows.Forms.MouseEventHandler(this.pnlGrade_MouseClick);
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
            // btnStep
            // 
            this.btnStep.Location = new System.Drawing.Point(533, 250);
            this.btnStep.Name = "btnStep";
            this.btnStep.Size = new System.Drawing.Size(180, 30);
            this.btnStep.TabIndex = 10;
            this.btnStep.Text = "Step (100 ticks)";
            this.btnStep.UseVisualStyleBackColor = true;
            this.btnStep.Click += new System.EventHandler(this.btnStep_Click);
            // 
            // lblTempoDecorrido
            // 
            this.lblTempoDecorrido.AutoSize = true;
            this.lblTempoDecorrido.Location = new System.Drawing.Point(530, 288);
            this.lblTempoDecorrido.Name = "lblTempoDecorrido";
            this.lblTempoDecorrido.Size = new System.Drawing.Size(150, 13);
            this.lblTempoDecorrido.TabIndex = 11;
            this.lblTempoDecorrido.Text = "Tempo real rodando: 00:00.00";
            //
            // lblTempoSimulado
            //
            this.lblTempoSimulado.AutoSize = true;
            this.lblTempoSimulado.Location = new System.Drawing.Point(530, 305);
            this.lblTempoSimulado.Name = "lblTempoSimulado";
            this.lblTempoSimulado.Size = new System.Drawing.Size(180, 13);
            this.lblTempoSimulado.TabIndex = 18;
            this.lblTempoSimulado.Text = "Tempo simulado: 0,00 s (0 ticks)";
            //
            // lblTrocarMaterial
            // 
            this.lblTrocarMaterial.AutoSize = true;
            this.lblTrocarMaterial.Location = new System.Drawing.Point(530, 328);
            this.lblTrocarMaterial.Name = "lblTrocarMaterial";
            this.lblTrocarMaterial.Size = new System.Drawing.Size(185, 13);
            this.lblTrocarMaterial.TabIndex = 12;
            this.lblTrocarMaterial.Text = "Trocar material do corpo selecionado:";
            // 
            // cmbTrocarMaterial
            // 
            this.cmbTrocarMaterial.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbTrocarMaterial.FormattingEnabled = true;
            this.cmbTrocarMaterial.Location = new System.Drawing.Point(533, 345);
            this.cmbTrocarMaterial.Name = "cmbTrocarMaterial";
            this.cmbTrocarMaterial.Size = new System.Drawing.Size(180, 21);
            this.cmbTrocarMaterial.TabIndex = 13;
            this.cmbTrocarMaterial.SelectedIndexChanged += new System.EventHandler(this.cmbTrocarMaterial_SelectedIndexChanged);
            // 
            // lblTemperaturaCorpo
            // 
            this.lblTemperaturaCorpo.AutoSize = true;
            this.lblTemperaturaCorpo.Location = new System.Drawing.Point(530, 378);
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
            this.numTemperaturaCorpo.Location = new System.Drawing.Point(533, 395);
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
            // 
            // btnAplicarTemperatura
            // 
            this.btnAplicarTemperatura.Location = new System.Drawing.Point(639, 393);
            this.btnAplicarTemperatura.Name = "btnAplicarTemperatura";
            this.btnAplicarTemperatura.Size = new System.Drawing.Size(74, 24);
            this.btnAplicarTemperatura.TabIndex = 16;
            this.btnAplicarTemperatura.Text = "Aplicar";
            this.btnAplicarTemperatura.UseVisualStyleBackColor = true;
            this.btnAplicarTemperatura.Click += new System.EventHandler(this.btnAplicarTemperatura_Click);
            // 
            // lblCorpoSelecionado
            // 
            this.lblCorpoSelecionado.Location = new System.Drawing.Point(533, 430);
            this.lblCorpoSelecionado.Name = "lblCorpoSelecionado";
            this.lblCorpoSelecionado.Size = new System.Drawing.Size(340, 60);
            this.lblCorpoSelecionado.TabIndex = 17;
            this.lblCorpoSelecionado.Text = "Nenhum corpo selecionado";
            //
            // lblConversorTitulo
            //
            this.lblConversorTitulo.AutoSize = true;
            this.lblConversorTitulo.Location = new System.Drawing.Point(530, 500);
            this.lblConversorTitulo.Name = "lblConversorTitulo";
            this.lblConversorTitulo.Size = new System.Drawing.Size(190, 13);
            this.lblConversorTitulo.TabIndex = 19;
            this.lblConversorTitulo.Text = "Conversor °C para K (só consulta):";
            //
            // numCelsius
            //
            this.numCelsius.DecimalPlaces = 2;
            this.numCelsius.Location = new System.Drawing.Point(533, 517);
            this.numCelsius.Maximum = 10000m;
            this.numCelsius.Minimum = -273.15m;
            this.numCelsius.Name = "numCelsius";
            this.numCelsius.Size = new System.Drawing.Size(80, 20);
            this.numCelsius.TabIndex = 20;
            this.numCelsius.Value = 25m;
            this.numCelsius.ValueChanged += new System.EventHandler(this.numCelsius_ValueChanged);
            //
            // lblKelvinResultado
            //
            this.lblKelvinResultado.AutoSize = true;
            this.lblKelvinResultado.Location = new System.Drawing.Point(625, 519);
            this.lblKelvinResultado.Name = "lblKelvinResultado";
            this.lblKelvinResultado.Size = new System.Drawing.Size(80, 13);
            this.lblKelvinResultado.TabIndex = 21;
            this.lblKelvinResultado.Text = "= 298,15 K";
            //
            // lblVelocidade
            //
            this.lblVelocidade.AutoSize = true;
            this.lblVelocidade.Location = new System.Drawing.Point(530, 550);
            this.lblVelocidade.Name = "lblVelocidade";
            this.lblVelocidade.Size = new System.Drawing.Size(80, 13);
            this.lblVelocidade.TabIndex = 22;
            this.lblVelocidade.Text = "Velocidade: 1x";
            //
            // trkVelocidade
            //
            this.trkVelocidade.Location = new System.Drawing.Point(533, 567);
            this.trkVelocidade.Maximum = 10;
            this.trkVelocidade.Minimum = 1;
            this.trkVelocidade.Name = "trkVelocidade";
            this.trkVelocidade.Size = new System.Drawing.Size(180, 45);
            this.trkVelocidade.TabIndex = 23;
            this.trkVelocidade.TickFrequency = 1;
            this.trkVelocidade.Value = 1;
            this.trkVelocidade.ValueChanged += new System.EventHandler(this.trkVelocidade_ValueChanged);
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
            this.ClientSize = new System.Drawing.Size(884, 640);
            this.Controls.Add(this.trkVelocidade);
            this.Controls.Add(this.lblVelocidade);
            this.Controls.Add(this.lblKelvinResultado);
            this.Controls.Add(this.numCelsius);
            this.Controls.Add(this.lblConversorTitulo);
            this.Controls.Add(this.lblCorpoSelecionado);
            this.Controls.Add(this.btnAplicarTemperatura);
            this.Controls.Add(this.numTemperaturaCorpo);
            this.Controls.Add(this.lblTemperaturaCorpo);
            this.Controls.Add(this.cmbTrocarMaterial);
            this.Controls.Add(this.lblTrocarMaterial);
            this.Controls.Add(this.lblTempoSimulado);
            this.Controls.Add(this.lblTempoDecorrido);
            this.Controls.Add(this.btnStep);
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
            this.Name = "MainForm";
            this.Text = "Simulador de Transferência de Calor";
            this.Load += new System.EventHandler(this.MainForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.numTamanho)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLadoCubo)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numTemperaturaCorpo)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numCelsius)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trkVelocidade)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel pnlGrade;
        private System.Windows.Forms.Label lblTamanho;
        private System.Windows.Forms.NumericUpDown numTamanho;
        private System.Windows.Forms.Label lblMaterialInicial;
        private System.Windows.Forms.ComboBox cmbMaterialInicial;
        private System.Windows.Forms.Label lblLadoCubo;
        private System.Windows.Forms.NumericUpDown numLadoCubo;
        private System.Windows.Forms.Button btnCriarGrade;
        private System.Windows.Forms.Button btnIniciar;
        private System.Windows.Forms.Button btnPausar;
        private System.Windows.Forms.Button btnStep;
        private System.Windows.Forms.Label lblTempoDecorrido;
        private System.Windows.Forms.Label lblTempoSimulado;
        private System.Windows.Forms.Label lblTrocarMaterial;
        private System.Windows.Forms.ComboBox cmbTrocarMaterial;
        private System.Windows.Forms.Label lblTemperaturaCorpo;
        private System.Windows.Forms.NumericUpDown numTemperaturaCorpo;
        private System.Windows.Forms.Button btnAplicarTemperatura;
        private System.Windows.Forms.Label lblCorpoSelecionado;
        private System.Windows.Forms.Label lblConversorTitulo;
        private System.Windows.Forms.NumericUpDown numCelsius;
        private System.Windows.Forms.Label lblKelvinResultado;
        private System.Windows.Forms.Label lblVelocidade;
        private System.Windows.Forms.TrackBar trkVelocidade;
        private System.Windows.Forms.Timer timerSimulacao;
    }
}
