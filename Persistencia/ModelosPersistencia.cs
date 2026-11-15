using System;
using System.Collections.Generic;
using TransferenciaCalor.Models;
using TransferenciaCalor.Simulation;

namespace TransferenciaCalor.Persistencia
{
    /// <summary>Como a execução calculou: motor sequencial (N1) ou paralelo (N2).</summary>
    public enum ModoExecucao
    {
        Sequencial,
        Paralelo
    }

    /// <summary>Ação do usuário durante a simulação, guardada para o "Refazer" repetir.</summary>
    public enum TipoEvento
    {
        TrocouMaterial,
        AplicouTemperatura
    }

    /// <summary>Um corpo da grade no tick 0 (é o que forma um "cenário").</summary>
    public sealed class CelulaCenario
    {
        private readonly int linha;
        private readonly int coluna;
        private readonly Material material;
        private readonly double ladoCubo;
        private readonly double temperaturaInicial;

        public CelulaCenario(int linha, int coluna, Material material, double ladoCubo, double temperaturaInicial)
        {
            this.linha = linha;
            this.coluna = coluna;
            this.material = material;
            this.ladoCubo = ladoCubo;
            this.temperaturaInicial = temperaturaInicial;
        }

        public int Linha { get { return linha; } }
        public int Coluna { get { return coluna; } }
        public Material Material { get { return material; } }
        public double LadoCubo { get { return ladoCubo; } }
        public double TemperaturaInicial { get { return temperaturaInicial; } }
    }

    /// <summary>Parâmetros de uma execução (uma rodada de um cenário).</summary>
    public sealed class DadosExecucao
    {
        private readonly long? cenarioExistenteId;
        private readonly string nomeCenario;
        private readonly long? execucaoOrigemId;
        private readonly ModoExecucao modo;
        private readonly int threads;
        private readonly double deltaT;
        private readonly double limiarEquilibrio;
        private readonly int gravACadaN;

        /// <param name="cenarioExistenteId">null = salvar a grade como um cenário novo; senão reaproveita (Refazer).</param>
        public DadosExecucao(long? cenarioExistenteId, string nomeCenario, long? execucaoOrigemId, ModoExecucao modo,
            int threads, double deltaT, double limiarEquilibrio, int gravACadaN)
        {
            this.cenarioExistenteId = cenarioExistenteId;
            this.nomeCenario = nomeCenario;
            this.execucaoOrigemId = execucaoOrigemId;
            this.modo = modo;
            this.threads = threads;
            this.deltaT = deltaT;
            this.limiarEquilibrio = limiarEquilibrio;
            this.gravACadaN = gravACadaN;
        }

        public long? CenarioExistenteId { get { return cenarioExistenteId; } }
        public string NomeCenario { get { return nomeCenario; } }
        public long? ExecucaoOrigemId { get { return execucaoOrigemId; } }
        public ModoExecucao Modo { get { return modo; } }
        public int Threads { get { return threads; } }
        public double DeltaT { get { return deltaT; } }
        public double LimiarEquilibrio { get { return limiarEquilibrio; } }
        public int GravACadaN { get { return gravACadaN; } }
    }

    /// <summary>Ação do usuário em um tick exato (lido do contador do motor, não do frame).</summary>
    public sealed class EventoSimulacao
    {
        private readonly long tick;
        private readonly TipoEvento tipo;
        private readonly int linha;
        private readonly int coluna;
        private readonly Material material;
        private readonly double? temperatura;

        public EventoSimulacao(long tick, TipoEvento tipo, int linha, int coluna, Material material, double? temperatura)
        {
            this.tick = tick;
            this.tipo = tipo;
            this.linha = linha;
            this.coluna = coluna;
            this.material = material;
            this.temperatura = temperatura;
        }

        public long Tick { get { return tick; } }
        public TipoEvento Tipo { get { return tipo; } }
        public int Linha { get { return linha; } }
        public int Coluna { get { return coluna; } }
        public Material Material { get { return material; } }
        public double? Temperatura { get { return temperatura; } }
    }

    /// <summary>
    /// Cópia IMUTÁVEL do estado de todos os corpos em um tick. É o que a thread da UI entrega
    /// à thread de gravação: como a simulação continua alterando a grade, a gravação não pode
    /// ler a grade viva — precisa de uma cópia. Custo medido na N1: ~1,7 us em N=15.
    /// </summary>
    public sealed class SnapshotEstado
    {
        private readonly long tick;
        private readonly int tamanho;
        private readonly double[] temperaturas;
        private readonly Material[] materiais;
        private readonly double[] calores;

        private SnapshotEstado(long tick, int tamanho)
        {
            this.tick = tick;
            this.tamanho = tamanho;
            int total = tamanho * tamanho;
            temperaturas = new double[total];
            materiais = new Material[total];
            calores = new double[total];
        }

        public long Tick { get { return tick; } }
        public int Tamanho { get { return tamanho; } }
        public int Quantidade { get { return temperaturas.Length; } }
        public double Temperatura(int indice) { return temperaturas[indice]; }
        public Material MaterialDe(int indice) { return materiais[indice]; }
        public double CalorSensivel(int indice) { return calores[indice]; }

        /// <summary>Copia temperatura, material e calor sensível de todos os corpos (ordem linha a linha).</summary>
        public static SnapshotEstado Capturar(HeatGrid grade, long tick)
        {
            int n = grade.Tamanho;
            SnapshotEstado s = new SnapshotEstado(tick, n);
            int i = 0;
            for (int linha = 0; linha < n; linha++)
            {
                for (int coluna = 0; coluna < n; coluna++)
                {
                    Body corpo = grade.ObterCorpo(linha, coluna);
                    s.temperaturas[i] = corpo.TemperaturaAtual;
                    s.materiais[i] = corpo.Material;
                    s.calores[i] = corpo.CalcularCalorSensivel();
                    i++;
                }
            }
            return s;
        }
    }

    /// <summary>Execução "original" listada para o usuário escolher no Refazer.</summary>
    public sealed class ResumoExecucao
    {
        private readonly long execucaoId;
        private readonly long cenarioId;
        private readonly string nomeCenario;
        private readonly int tamanho;
        private readonly long ticks;
        private readonly ModoExecucao modo;
        private readonly bool concluida;

        public ResumoExecucao(long execucaoId, long cenarioId, string nomeCenario, int tamanho, long ticks, ModoExecucao modo, bool concluida)
        {
            this.execucaoId = execucaoId;
            this.cenarioId = cenarioId;
            this.nomeCenario = nomeCenario;
            this.tamanho = tamanho;
            this.ticks = ticks;
            this.modo = modo;
            this.concluida = concluida;
        }

        public long ExecucaoId { get { return execucaoId; } }
        public long CenarioId { get { return cenarioId; } }
        public string NomeCenario { get { return nomeCenario; } }
        public int Tamanho { get { return tamanho; } }
        public long Ticks { get { return ticks; } }
        public ModoExecucao Modo { get { return modo; } }
        public bool Concluida { get { return concluida; } }

        // O ComboBox mostra este texto
        public override string ToString()
        {
            return string.Format("#{0} {1} ({2}x{2}, {3} ticks{4})", execucaoId, nomeCenario, tamanho, ticks, concluida ? "" : ", INCOMPLETA");
        }
    }

    /// <summary>Cenário recarregado do banco: tudo que é preciso para remontar a grade no tick 0.</summary>
    public sealed class CenarioCarregado
    {
        private readonly long cenarioId;
        private readonly string nome;
        private readonly int tamanho;
        private readonly IList<CelulaCenario> celulas;

        public CenarioCarregado(long cenarioId, string nome, int tamanho, IList<CelulaCenario> celulas)
        {
            this.cenarioId = cenarioId;
            this.nome = nome;
            this.tamanho = tamanho;
            this.celulas = celulas;
        }

        public long CenarioId { get { return cenarioId; } }
        public string Nome { get { return nome; } }
        public int Tamanho { get { return tamanho; } }
        public IList<CelulaCenario> Celulas { get { return celulas; } }

        /// <summary>Remonta a grade com os materiais, lados e temperaturas gravados no tick 0.</summary>
        public HeatGrid MontarGrade()
        {
            if (celulas.Count == 0)
                throw new InvalidOperationException("Cenário sem células.");

            HeatGrid grade = new HeatGrid(tamanho, celulas[0].Material, celulas[0].LadoCubo, celulas[0].TemperaturaInicial);
            foreach (CelulaCenario c in celulas)
            {
                grade.TrocarMaterial(c.Linha, c.Coluna, c.Material);
                Body corpo = grade.ObterCorpo(c.Linha, c.Coluna);
                corpo.LadoCubo = c.LadoCubo;
                corpo.TemperaturaAtual = c.TemperaturaInicial;
            }
            return grade;
        }
    }

    /// <summary>Parâmetros gravados de uma execução (para o Refazer usar os MESMOS valores).</summary>
    public sealed class ExecucaoCarregada
    {
        private readonly long execucaoId;
        private readonly long cenarioId;
        private readonly double deltaT;
        private readonly double limiarEquilibrio;
        private readonly int gravACadaN;
        private readonly long ticks;
        private readonly bool concluida;

        public ExecucaoCarregada(long execucaoId, long cenarioId, double deltaT, double limiarEquilibrio, int gravACadaN, long ticks, bool concluida)
        {
            this.execucaoId = execucaoId;
            this.cenarioId = cenarioId;
            this.deltaT = deltaT;
            this.limiarEquilibrio = limiarEquilibrio;
            this.gravACadaN = gravACadaN;
            this.ticks = ticks;
            this.concluida = concluida;
        }

        public long ExecucaoId { get { return execucaoId; } }
        public long CenarioId { get { return cenarioId; } }
        public double DeltaT { get { return deltaT; } }
        public double LimiarEquilibrio { get { return limiarEquilibrio; } }
        public int GravACadaN { get { return gravACadaN; } }
        public long Ticks { get { return ticks; } }
        public bool Concluida { get { return concluida; } }
    }

    /// <summary>Resultado da view v_conferencia para uma execução refeita.</summary>
    public sealed class ResultadoConferencia
    {
        private readonly long pontosComparados;
        private readonly double diferencaMaximaK;

        public ResultadoConferencia(long pontosComparados, double diferencaMaximaK)
        {
            this.pontosComparados = pontosComparados;
            this.diferencaMaximaK = diferencaMaximaK;
        }

        public long PontosComparados { get { return pontosComparados; } }
        public double DiferencaMaximaK { get { return diferencaMaximaK; } }
    }
}
