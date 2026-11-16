using System;
using System.Diagnostics;
using System.Linq;

namespace TransferenciaCalor.Benchmark
{
    /// <summary>
    /// Resultado de uma medição: tempo (ns) por operação em cada repetição,
    /// já com mediana, média e desvio-padrão calculados.
    /// </summary>
    internal sealed class Amostra
    {
        private readonly double[] nsPorOperacao;
        private readonly long iteracoes;

        public Amostra(double[] nsPorOperacao, long iteracoes)
        {
            this.nsPorOperacao = nsPorOperacao;
            this.iteracoes = iteracoes;
        }

        public int Repeticoes { get { return nsPorOperacao.Length; } }
        public long Iteracoes { get { return iteracoes; } }
        public double Minimo { get { return nsPorOperacao.Min(); } }
        public double Media { get { return nsPorOperacao.Average(); } }

        // Mediana é a estatística principal: resiste a uma repetição ruim
        // (ex.: o Windows agendando outro processo no meio da medição).
        public double Mediana
        {
            get
            {
                double[] ordenado = nsPorOperacao.OrderBy(x => x).ToArray();
                int meio = ordenado.Length / 2;
                return ordenado.Length % 2 == 1 ? ordenado[meio] : (ordenado[meio - 1] + ordenado[meio]) / 2.0;
            }
        }

        // Desvio-padrão amostral (n-1)
        public double Desvio
        {
            get
            {
                if (nsPorOperacao.Length < 2) return 0.0;
                double media = Media;
                double soma = nsPorOperacao.Sum(x => (x - media) * (x - media));
                return Math.Sqrt(soma / (nsPorOperacao.Length - 1));
            }
        }
    }

    /// <summary>Contadores de memória/GC observados durante um lote de operações.</summary>
    internal sealed class Alocacao
    {
        private readonly long operacoes;
        private readonly long bytes;
        private readonly int gen0;
        private readonly int gen1;
        private readonly int gen2;

        public Alocacao(long operacoes, long bytes, int gen0, int gen1, int gen2)
        {
            this.operacoes = operacoes;
            this.bytes = bytes;
            this.gen0 = gen0;
            this.gen1 = gen1;
            this.gen2 = gen2;
        }

        public long Operacoes { get { return operacoes; } }
        public double BytesPorOperacao { get { return operacoes == 0 ? 0 : (double)bytes / operacoes; } }
        public int Gen0 { get { return gen0; } }
        public int Gen1 { get { return gen1; } }
        public int Gen2 { get { return gen2; } }
    }

    /// <summary>
    /// Medidor genérico. Metodologia: aquecimento descartado (JIT), calibração da
    /// quantidade de repetições da operação para a medição durar >= ~100 ms
    /// (Stopwatch tem resolução de ~100 ns; operações de 1 µs não são mensuráveis
    /// uma a uma) e N repetições da medição, com GC limpo antes de cada uma.
    /// </summary>
    internal static class Medidor
    {
        private const double DuracaoMinimaNs = 100e6; // 100 ms

        public static Amostra Medir(Action preparar, Action operacao, int repeticoes)
        {
            long iter = Calibrar(preparar, operacao);

            // Mais duas execuções completas descartadas: estabiliza caches e frequência da CPU
            for (int i = 0; i < 2; i++)
            {
                Preparar(preparar);
                Cronometrar(operacao, iter);
            }

            double[] resultados = new double[repeticoes];
            for (int r = 0; r < repeticoes; r++)
            {
                Preparar(preparar);
                resultados[r] = Cronometrar(operacao, iter) / iter;
            }
            return new Amostra(resultados, iter);
        }

        // Mesma operação, mas lendo os contadores de GC e de bytes alocados.
        // É feito em passada SEPARADA da de tempo, porque ligar o monitoramento
        // de alocação por AppDomain tem custo próprio e contaminaria os tempos.
        public static Alocacao MedirAlocacao(Action preparar, Action operacao, long iteracoes)
        {
            Preparar(preparar);
            long b0 = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);

            for (long i = 0; i < iteracoes; i++)
                operacao();

            long b1 = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            return new Alocacao(iteracoes, b1 - b0,
                GC.CollectionCount(0) - g0, GC.CollectionCount(1) - g1, GC.CollectionCount(2) - g2);
        }

        // Devolve quantas execuções da operação cabem em >= 100 ms; as tentativas
        // da calibração também servem de aquecimento do JIT.
        public static long Calibrar(Action preparar, Action operacao)
        {
            long iter = 1;
            while (true)
            {
                Preparar(preparar);
                double ns = Cronometrar(operacao, iter);
                if (ns >= DuracaoMinimaNs)
                    return iter;

                double fator = ns <= 0 ? 10.0 : (DuracaoMinimaNs * 1.2) / ns;
                iter = (long)Math.Ceiling(iter * Math.Max(fator, 2.0));
            }
        }

        private static void Preparar(Action preparar)
        {
            // Preparação (montar a grade) e coleta de lixo ficam FORA do tempo medido
            if (preparar != null)
                preparar();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        // Tempo total (ns) de "iter" execuções. O custo do delegate (~2 ns) é
        // desprezível diante de operações de microssegundos.
        private static double Cronometrar(Action operacao, long iter)
        {
            long t0 = Stopwatch.GetTimestamp();
            for (long i = 0; i < iter; i++)
                operacao();
            long t1 = Stopwatch.GetTimestamp();
            return (t1 - t0) * 1e9 / Stopwatch.Frequency;
        }
    }
}
