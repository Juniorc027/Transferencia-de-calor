using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TransferenciaCalor.Simulation;

namespace TransferenciaCalor.Benchmark
{
    /// <summary>
    /// Benchmark da Fase 2: custo por tick da N1, da N2 sequencial e da N2 paralela
    /// (1, 2, 4, 8 e 16 threads, limitado ao que a máquina tem), para cada tamanho de grade.
    /// Separar "N2 sequencial" de "N2 paralela" mostra quanto do ganho vem da reorganização
    /// do algoritmo (sem alocação por tick, sem Math de grade 2D) e quanto vem das threads.
    /// </summary>
    internal static class BenchMotores
    {
        private static readonly int[] Tamanhos = { 5, 10, 15, 20, 30, 60, 100, 200, 400 };
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // Nome do motor -> fábrica (a grade inicial é sempre a mesma: misto, semente fixa)
        private sealed class Motor
        {
            private readonly string nome;
            private readonly int threads;
            private readonly Func<int, IMotorSimulacao> fabrica;
            public Motor(string nome, int threads, Func<int, IMotorSimulacao> fabrica) { this.nome = nome; this.threads = threads; this.fabrica = fabrica; }
            public string Nome { get { return nome; } }
            public int Threads { get { return threads; } }
            public IMotorSimulacao Criar(int n) { return fabrica(n); }
        }

        public static List<int> ThreadsMedidos()
        {
            int max = Environment.ProcessorCount;
            return new[] { 1, 2, 4, 8, 16 }.Where(t => t <= max).ToList();
        }

        public static int Executar(int[] tamanhosEscolhidos, int repeticoes, string pasta, Ambiente amb)
        {
            int[] ns = tamanhosEscolhidos ?? Tamanhos;
            List<Motor> motores = new List<Motor>();
            motores.Add(new Motor("N1", 1, n => Cenarios.CriarMotor(Cenarios.CriarMisto(n))));
            motores.Add(new Motor("N2-seq", 1, n => Cenarios.CriarMotorN2(Cenarios.CriarMisto(n), 1, 1, true)));
            foreach (int t in ThreadsMedidos())
            {
                int threads = t;
                // tamanhoMinimo = 1: queremos medir o caminho paralelo em TODOS os N, inclusive
                // onde ele perde, para descobrir o ponto de cruzamento de verdade.
                motores.Add(new Motor("N2-par-T" + t, t, n => Cenarios.CriarMotorN2(Cenarios.CriarMisto(n), threads, 1, false)));
            }

            AppDomain.MonitoringIsEnabled = true;
            try { System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.High; } catch (Exception) { }

            Resultados res = new Resultados();
            foreach (int n in ns)
            {
                Console.WriteLine("\n=== N = {0} ({1} corpos) ===", n, n * n);
                foreach (Motor m in motores)
                {
                    int nn = n;
                    Motor mm = m;
                    IMotorSimulacao motor = null;
                    Action preparar = () => { motor = mm.Criar(nn); };
                    Action tick = () => motor.Tick();
                    Amostra a = Medidor.Medir(preparar, tick, repeticoes);
                    res.AdicionarTempo(n, "tick_" + m.Nome, a);
                    res.AdicionarAlocacao(n, "tick_" + m.Nome, Medidor.MedirAlocacao(preparar, tick, a.Iteracoes));
                    res.AdicionarValor(n, "tick_" + m.Nome + "_finito", "1=sim,0=nao", Cenarios.TudoFinito(motor.Grade) ? 1 : 0, a.Iteracoes);
                    Console.WriteLine("  {0,-10} {1,12:F2} us", m.Nome, a.Mediana / 1000);
                }
            }

            string nome = string.Concat(amb.NomeMaquina.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_'));
            string csv = Path.Combine(pasta, "resultados_n2_" + nome + ".csv");
            string md = Path.Combine(pasta, "resultados_n2_" + nome + ".md");
            File.WriteAllText(csv, Relatorio.MontarCsv(amb, res), new UTF8Encoding(true));
            File.WriteAllText(md, MontarMarkdown(amb, res, ns.ToList(), motores.Select(x => x.Nome).ToList()), new UTF8Encoding(true));
            Console.WriteLine("\nCSV: " + csv + "\nMD : " + md);
            return 0;
        }

        private static string F(double v, string fmt = "F2") { return double.IsNaN(v) ? "n/m" : v.ToString(fmt, Inv); }

        private static string MontarMarkdown(Ambiente amb, Resultados res, List<int> ns, List<string> motores)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Benchmark N1 x N2 (motores) — " + amb.NomeMaquina);
            sb.AppendLine();
            sb.AppendLine("Release, sem debugger; 7 repetições por medição (≥ ~100 ms cada), warm-up descartado. Tempos em µs por Tick(). \"n/m\" = não medido.");
            sb.AppendLine();
            sb.AppendLine("## Ambiente");
            sb.AppendLine();
            sb.AppendLine("| Item | Valor |");
            sb.AppendLine("|---|---|");
            foreach (KeyValuePair<string, string> i in amb.Itens)
                sb.AppendLine("| " + i.Key + " | " + i.Value.Replace("|", "/") + " |");
            sb.AppendLine();

            List<string> paralelos = motores.Where(m => m.StartsWith("N2-par")).ToList();

            sb.AppendLine("## Tempo por tick — MEDIANA (µs)");
            sb.AppendLine();
            sb.AppendLine("| N | " + string.Join(" | ", motores) + " |");
            sb.AppendLine("|---:|" + string.Join("|", motores.Select(m => "---:")) + "|");
            foreach (int n in ns)
                sb.AppendLine("| " + n + " | " + string.Join(" | ", motores.Select(m => Relatorio.US(res.Valor(n, "tick_" + m)))) + " |");
            sb.AppendLine();

            sb.AppendLine("## Dispersão — média ± desvio-padrão (µs) e coeficiente de variação");
            sb.AppendLine();
            sb.AppendLine("| N | " + string.Join(" | ", motores) + " |");
            sb.AppendLine("|---:|" + string.Join("|", motores.Select(m => "---:")) + "|");
            foreach (int n in ns)
            {
                List<string> cel = new List<string>();
                foreach (string m in motores)
                {
                    Registro r = res.Obter(n, "tick_" + m);
                    cel.Add(r == null ? "n/m" : Relatorio.US(r.Media) + " ± " + Relatorio.US(r.Desvio) + " (" + F(r.Media > 0 ? 100 * r.Desvio / r.Media : 0, "F1") + "%)");
                }
                sb.AppendLine("| " + n + " | " + string.Join(" | ", cel) + " |");
            }
            sb.AppendLine();

            // Speedups
            sb.AppendLine("## Speedup sobre a N1 (> 1 = mais rápido que a N1)");
            sb.AppendLine();
            List<string> contraN1 = motores.Where(m => m != "N1").ToList();
            sb.AppendLine("| N | " + string.Join(" | ", contraN1) + " |");
            sb.AppendLine("|---:|" + string.Join("|", contraN1.Select(m => "---:")) + "|");
            foreach (int n in ns)
                sb.AppendLine("| " + n + " | " + string.Join(" | ", contraN1.Select(m => F(res.Valor(n, "tick_N1") / res.Valor(n, "tick_" + m)) + "×")) + " |");
            sb.AppendLine();

            sb.AppendLine("## Speedup sobre a N2 sequencial (isola o efeito das threads)");
            sb.AppendLine();
            sb.AppendLine("| N | " + string.Join(" | ", paralelos) + " |");
            sb.AppendLine("|---:|" + string.Join("|", paralelos.Select(m => "---:")) + "|");
            foreach (int n in ns)
                sb.AppendLine("| " + n + " | " + string.Join(" | ", paralelos.Select(m => F(res.Valor(n, "tick_N2-seq") / res.Valor(n, "tick_" + m)) + "×")) + " |");
            sb.AppendLine();

            sb.AppendLine("## Eficiência = (speedup sobre a N2 sequencial) / nº de threads");
            sb.AppendLine();
            sb.AppendLine("1,00 = escala linear; abaixo disso as threads gastam tempo esperando, agendando ou disputando memória/cache.");
            sb.AppendLine();
            sb.AppendLine("| N | " + string.Join(" | ", paralelos) + " |");
            sb.AppendLine("|---:|" + string.Join("|", paralelos.Select(m => "---:")) + "|");
            foreach (int n in ns)
                sb.AppendLine("| " + n + " | " + string.Join(" | ", paralelos.Select(m =>
                {
                    int t = int.Parse(m.Substring(m.LastIndexOf('T') + 1), Inv);
                    return F(res.Valor(n, "tick_N2-seq") / res.Valor(n, "tick_" + m) / t);
                })) + " |");
            sb.AppendLine();

            // Cruzamento
            sb.AppendLine("## Ponto de cruzamento (primeiro N medido em que o paralelo passa a ganhar)");
            sb.AppendLine();
            sb.AppendLine("| Motor paralelo | Ganha da N2 sequencial a partir de N = | Ganha da N1 a partir de N = |");
            sb.AppendLine("|---|---:|---:|");
            foreach (string m in paralelos)
                sb.AppendLine("| " + m + " | " + Cruzamento(res, ns, m, "N2-seq") + " | " + Cruzamento(res, ns, m, "N1") + " |");
            sb.AppendLine();
            sb.AppendLine("\"Ganha\" = mediana estritamente menor E sem sobreposição com a média ± 1 desvio da outra. O ponto real fica entre o N indicado e o N medido anterior.");
            sb.AppendLine();

            // Alocações
            sb.AppendLine("## Alocações e GC por tick");
            sb.AppendLine();
            sb.AppendLine("Bytes alocados por tick / coletas gen0-gen1-gen2 por 1.000 ticks (passada separada, com `AppDomain.MonitoringIsEnabled`).");
            sb.AppendLine();
            List<string> alvo = new List<string> { "N1", "N2-seq" };
            string maior = paralelos.LastOrDefault();
            if (maior != null) alvo.Add(maior);
            sb.AppendLine("| N | " + string.Join(" | ", alvo.Select(m => m + ": B/tick | GC 0/1/2")) + " |");
            sb.AppendLine("|---:|" + string.Join("|", alvo.Select(m => "---:|---")) + "|");
            foreach (int n in ns)
                sb.AppendLine("| " + n + " | " + string.Join(" | ", alvo.Select(m =>
                    F(res.Valor(n, "tick_" + m + "_bytes_por_op"), "F0") + " | " +
                    F(res.Valor(n, "tick_" + m + "_gc_gen0"), "F2") + " / " + F(res.Valor(n, "tick_" + m + "_gc_gen1"), "F2") + " / " + F(res.Valor(n, "tick_" + m + "_gc_gen2"), "F2"))) + " |");
            sb.AppendLine();
            sb.AppendLine("Observação: a N2 não aloca arrays por tick; os bytes que aparecem no modo paralelo vêm da infraestrutura do `Parallel.For` (objetos internos por chamada). Em N grande a contagem de coletas tem poucas repetições e é pouco confiável.");
            sb.AppendLine();
            return sb.ToString();
        }

        private static string Cruzamento(Resultados res, List<int> ns, string paralelo, string referencia)
        {
            foreach (int n in ns)
            {
                Registro p = res.Obter(n, "tick_" + paralelo), r = res.Obter(n, "tick_" + referencia);
                if (p == null || r == null) continue;
                // "Ganha de verdade": a mediana é menor e a média do paralelo + 1 dp ainda fica abaixo da média da referência - 1 dp
                if (p.Mediana < r.Mediana && p.Media + p.Desvio < r.Media - r.Desvio)
                    return n.ToString(Inv);
            }
            return "não ganhou nos N medidos";
        }
    }
}
