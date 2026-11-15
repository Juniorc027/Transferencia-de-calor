using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MySqlConnector;
using TransferenciaCalor.Persistencia;
using TransferenciaCalor.Simulation;

namespace TransferenciaCalor.Benchmark
{
    /// <summary>Uma rodada medida do benchmark com banco (uma linha do CSV).</summary>
    internal sealed class RodadaBanco
    {
        private readonly string cenario;
        private readonly int n;
        private readonly int gravACadaN;
        private readonly int repeticao;
        private readonly double msSimulacao;     // do início até o produtor terminar o último tick
        private readonly double msEsvaziar;      // espera a fila esvaziar depois do último tick
        private readonly double msCalculo;       // só dentro de Tick()
        private readonly long linhas;
        private readonly int maximoFila;
        private readonly double msBloqueado;
        private readonly long msGravacao;
        private readonly double tamanhoMb;
        private readonly long leiturasNoBanco;

        public RodadaBanco(string cenario, int n, int gravACadaN, int repeticao, double msSimulacao, double msEsvaziar, double msCalculo,
            long linhas, int maximoFila, double msBloqueado, long msGravacao, double tamanhoMb, long leiturasNoBanco)
        {
            this.cenario = cenario; this.n = n; this.gravACadaN = gravACadaN; this.repeticao = repeticao;
            this.msSimulacao = msSimulacao; this.msEsvaziar = msEsvaziar; this.msCalculo = msCalculo;
            this.linhas = linhas; this.maximoFila = maximoFila; this.msBloqueado = msBloqueado; this.msGravacao = msGravacao;
            this.tamanhoMb = tamanhoMb; this.leiturasNoBanco = leiturasNoBanco;
        }

        public string Cenario { get { return cenario; } }
        public int N { get { return n; } }
        public int GravACadaN { get { return gravACadaN; } }
        public int Repeticao { get { return repeticao; } }
        public double MsSimulacao { get { return msSimulacao; } }
        public double MsEsvaziar { get { return msEsvaziar; } }
        public double MsTotal { get { return msSimulacao + msEsvaziar; } }
        public double MsCalculo { get { return msCalculo; } }
        public long Linhas { get { return linhas; } }
        public double LinhasPorSegundo { get { return MsTotal > 0 ? linhas / (MsTotal / 1000.0) : 0; } }
        public int MaximoFila { get { return maximoFila; } }
        public double MsBloqueado { get { return msBloqueado; } }
        public long MsGravacao { get { return msGravacao; } }
        public double TamanhoMb { get { return tamanhoMb; } }
        public long LeiturasNoBanco { get { return leiturasNoBanco; } }
    }

    /// <summary>
    /// Fase 5: quatro cenários na MESMA grade e no MESMO número de ticks:
    ///  A) N1 sem banco   B) N2 paralelo sem banco   C) N1 com gravação   D) N2 paralelo com gravação.
    /// Mede o impacto da gravação (D contra B) e o quanto o banco consegue absorver.
    /// </summary>
    internal static class BenchBanco
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string PrefixoNomeBenchmark = "BENCH-";

        public static int Executar(IRepositorioSimulacao repo, string stringConexao, int[] tamanhos, int ticks, int[] gravs,
            int repsSemBanco, int repsComBanco, bool manterDados, Ambiente amb, string pasta, string descricaoServidor)
        {
            repo.GarantirEsquema();
            AppDomain.MonitoringIsEnabled = true;
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High; } catch (Exception) { }

            List<RodadaBanco> rodadas = new List<RodadaBanco>();
            foreach (int n in tamanhos)
            {
                Console.WriteLine("\n=== N = {0}, {1} ticks ===", n, ticks);

                // Aquecimento: JIT do motor, do gravador e do driver antes de medir
                ExecutarUma(repo, "A", n, 200, 100, 0);
                ExecutarUma(repo, "D", n, 200, 100, 0);

                for (int r = 1; r <= repsSemBanco; r++)
                {
                    rodadas.Add(ExecutarUma(repo, "A", n, ticks, 0, r));
                    rodadas.Add(ExecutarUma(repo, "B", n, ticks, 0, r));
                }
                foreach (int g in gravs)
                {
                    // Linhas totais da rodada = (ticks/g + 1) * N*N: pula combinações que gerariam milhões de linhas
                    long linhasPrevistas = (ticks / g + 1L) * n * n;
                    if (linhasPrevistas > 2000000)
                    {
                        Console.WriteLine("  (pulado: N={0}, grav_a_cada_n={1} geraria {2:N0} linhas)", n, g, linhasPrevistas);
                        continue;
                    }
                    for (int r = 1; r <= repsComBanco; r++)
                    {
                        rodadas.Add(ExecutarUma(repo, "C", n, ticks, g, r));
                        rodadas.Add(ExecutarUma(repo, "D", n, ticks, g, r));
                    }
                }
            }

            string versao = descricaoServidor ?? ConsultarVersao(stringConexao);
            long totalLeituras = repo.ContarLeituras();
            double tamanhoFinalMb = repo.TamanhoTabelasMb();
            Console.WriteLine("\nTotal de linhas em leituras: {0:N0}; tamanho aproximado das tabelas: {1:F1} MB", totalLeituras, tamanhoFinalMb);

            string nome = string.Concat(amb.NomeMaquina.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_'));
            File.WriteAllText(Path.Combine(pasta, "resultados_banco_" + nome + ".csv"), MontarCsv(amb, rodadas), new UTF8Encoding(true));
            File.WriteAllText(Path.Combine(pasta, "resultados_banco_" + nome + ".md"), MontarMarkdown(amb, rodadas, ticks, versao, totalLeituras, tamanhoFinalMb), new UTF8Encoding(true));
            Console.WriteLine("Gravado: resultados_banco_" + nome + ".csv / .md em " + pasta);

            // Remove só o que ESTE benchmark criou (cenários com nome BENCH-*; o resto do banco não é tocado)
            if (!manterDados && stringConexao != null)
                LimparDadosDoBenchmark(stringConexao);
            return 0;
        }

        private static RodadaBanco ExecutarUma(IRepositorioSimulacao repo, string cenario, int n, int ticks, int gravACadaN, int repeticao)
        {
            bool comBanco = cenario == "C" || cenario == "D";
            bool paralelo = cenario == "B" || cenario == "D";

            HeatGrid grade = Cenarios.CriarMisto(n);
            IMotorSimulacao motor = paralelo
                ? (IMotorSimulacao)new HeatSimulationEngineParalelo(grade, 0.01, 1e-4)  // configuração padrão do app
                : Cenarios.CriarMotor(grade);

            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (!comBanco)
            {
                Stopwatch cron = Stopwatch.StartNew();
                ResultadoExecucao res = ExecutorSimulacao.Executar(grade, motor, ticks, null, null);
                double ms = cron.Elapsed.TotalMilliseconds;
                if (repeticao > 0)
                    Console.WriteLine("  {0}  N={1,-3} rep {2}: {3,9:F1} ms ({4:F2} us/tick)", cenario, n, repeticao, ms, ms * 1000 / ticks);
                return new RodadaBanco(cenario, n, 0, repeticao, ms, 0, res.MsCalculo, 0, 0, 0, 0, 0, 0);
            }

            using (GravadorSimulacao grav = new GravadorSimulacao(repo))
            {
                SessaoGravacao sessao = new SessaoGravacao(grav, gravACadaN);
                HeatSimulationEngineParalelo p = motor as HeatSimulationEngineParalelo;
                DadosExecucao dados = new DadosExecucao(null, PrefixoNomeBenchmark + cenario + "-N" + n + "-g" + gravACadaN, null,
                    p != null && p.ExecutandoEmParalelo ? ModoExecucao.Paralelo : ModoExecucao.Sequencial,
                    p != null && p.ExecutandoEmParalelo ? p.GrauParalelismo : 1, 0.01, 1e-4, gravACadaN);

                Stopwatch cron = Stopwatch.StartNew();
                sessao.Abrir(dados, grade);
                ResultadoExecucao res = ExecutorSimulacao.Executar(grade, motor, ticks, sessao, null);
                double msSimulacao = cron.Elapsed.TotalMilliseconds;

                // Quanto o banco ainda precisa para terminar DEPOIS que a simulação acabou
                Stopwatch esvaziar = Stopwatch.StartNew();
                bool ok = grav.EsperarEsvaziar(600000);
                double msEsvaziar = esvaziar.Elapsed.TotalMilliseconds;
                if (!ok || grav.Falhou)
                    throw new InvalidOperationException("Gravação falhou no cenário " + cenario + ": " + (grav.Falhou ? grav.UltimoErro : "tempo esgotado"));

                RodadaBanco r = new RodadaBanco(cenario, n, gravACadaN, repeticao, msSimulacao, msEsvaziar, res.MsCalculo,
                    grav.LinhasGravadas, grav.MaximoDaFila, grav.MsBloqueadoProdutor, grav.MsGravacao, repo.TamanhoTabelasMb(), repo.ContarLeituras());
                if (repeticao > 0)
                    Console.WriteLine("  {0}  N={1,-3} g={2,-3} rep {3}: simulação {4,9:F1} ms + esvaziar {5,8:F1} ms | {6:N0} linhas ({7:N0}/s) | fila máx {8}, bloqueado {9:F1} ms",
                        cenario, n, gravACadaN, repeticao, msSimulacao, msEsvaziar, r.Linhas, r.LinhasPorSegundo, r.MaximoFila, r.MsBloqueado);
                return r;
            }
        }

        private static string ConsultarVersao(string stringConexao)
        {
            if (stringConexao == null) return "servidor não identificado";
            try
            {
                using (MySqlConnection c = new MySqlConnection(stringConexao))
                {
                    c.Open();
                    using (MySqlCommand cmd = new MySqlCommand("SELECT VERSION()", c))
                        return "MySQL/MariaDB " + Convert.ToString(cmd.ExecuteScalar());
                }
            }
            catch (Exception) { return "servidor não identificado"; }
        }

        private static void LimparDadosDoBenchmark(string stringConexao)
        {
            try
            {
                MySqlConnectionStringBuilder b = new MySqlConnectionStringBuilder(stringConexao);
                b.Database = "simulador_calor";
                using (MySqlConnection c = new MySqlConnection(b.ConnectionString))
                {
                    c.Open();
                    // ON DELETE CASCADE leva execuções, leituras e eventos junto
                    using (MySqlCommand cmd = new MySqlCommand("DELETE FROM cenarios WHERE nome LIKE 'BENCH-%'", c))
                    {
                        cmd.CommandTimeout = 600;
                        Console.WriteLine("Limpeza: {0} cenários BENCH-* removidos (use --manter-dados para conservar).", cmd.ExecuteNonQuery());
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine("Limpeza não executada: " + ex.Message); }
        }

        private static string MontarCsv(Ambiente amb, List<RodadaBanco> rodadas)
        {
            Func<string, string> a = chave => amb.Itens.First(i => i.Key == chave).Value;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("maquina,cpu,nucleos_fisicos,nucleos_logicos,ram,configuracao,cenario,N,grav_a_cada_n,repeticao,ms_simulacao,ms_esvaziar_fila,ms_total,ms_calculo,linhas,linhas_por_s,fila_maxima,ms_bloqueado_produtor,ms_gravacao,leituras_no_banco,tamanho_tabelas_mb");
            foreach (RodadaBanco r in rodadas.Where(x => x.Repeticao > 0))
                sb.AppendLine(string.Join(",", new[] { "\"" + a("maquina") + "\"", "\"" + a("cpu") + "\"", a("nucleos_fisicos"), a("nucleos_logicos"), "\"" + a("ram") + "\"", a("configuracao"),
                    r.Cenario, r.N.ToString(Inv), r.GravACadaN.ToString(Inv), r.Repeticao.ToString(Inv),
                    r.MsSimulacao.ToString("F3", Inv), r.MsEsvaziar.ToString("F3", Inv), r.MsTotal.ToString("F3", Inv), r.MsCalculo.ToString("F3", Inv),
                    r.Linhas.ToString(Inv), r.LinhasPorSegundo.ToString("F1", Inv), r.MaximoFila.ToString(Inv), r.MsBloqueado.ToString("F3", Inv),
                    r.MsGravacao.ToString(Inv), r.LeiturasNoBanco.ToString(Inv), r.TamanhoMb.ToString("F2", Inv) }));
            return sb.ToString();
        }

        private static double Mediana(IEnumerable<double> valores)
        {
            double[] v = valores.OrderBy(x => x).ToArray();
            if (v.Length == 0) return double.NaN;
            return v.Length % 2 == 1 ? v[v.Length / 2] : (v[v.Length / 2 - 1] + v[v.Length / 2]) / 2.0;
        }

        private static string MontarMarkdown(Ambiente amb, List<RodadaBanco> rodadas, int ticks, string servidor, long totalLeituras, double tamanhoMb)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Benchmark com banco de dados — " + amb.NomeMaquina);
            sb.AppendLine();
            sb.AppendLine("Servidor: **" + servidor + "**. " + ticks + " ticks por rodada; medianas das repetições; Release, sem debugger.");
            sb.AppendLine("Cenários: A = N1 sem banco; B = N2 paralelo sem banco; C = N1 com gravação; D = N2 paralelo com gravação.");
            sb.AppendLine();
            sb.AppendLine("Ambiente: " + string.Join("; ", amb.Itens.Where(i => new[] { "cpu", "nucleos_fisicos", "nucleos_logicos", "ram", "windows", "dotnet_framework", "configuracao", "plataforma" }.Contains(i.Key)).Select(i => i.Key + " = " + i.Value.Replace("|", "/"))));
            sb.AppendLine();
            sb.AppendLine("## Resultados (mediana, ms)");
            sb.AppendLine();
            sb.AppendLine("`simulação` = tempo até o último tick (o que o usuário sente); `esvaziar` = espera extra para o banco terminar; `total` = simulação + esvaziar.");
            sb.AppendLine();
            sb.AppendLine("| N | grav a cada | Cenário | simulação | esvaziar fila | total | cálculo (Tick) | linhas | linhas/s | fila máx | produtor bloqueado | ms gravação (consumidor) |");
            sb.AppendLine("|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
            foreach (var grupo in rodadas.Where(r => r.Repeticao > 0).GroupBy(r => new { r.N, r.GravACadaN, r.Cenario }).OrderBy(g => g.Key.N).ThenBy(g => g.Key.GravACadaN).ThenBy(g => g.Key.Cenario))
            {
                sb.AppendLine("| " + grupo.Key.N + " | " + (grupo.Key.GravACadaN == 0 ? "—" : grupo.Key.GravACadaN.ToString()) + " | " + grupo.Key.Cenario + " | " +
                    Mediana(grupo.Select(r => r.MsSimulacao)).ToString("F1", Inv) + " | " + Mediana(grupo.Select(r => r.MsEsvaziar)).ToString("F1", Inv) + " | " +
                    Mediana(grupo.Select(r => r.MsTotal)).ToString("F1", Inv) + " | " + Mediana(grupo.Select(r => r.MsCalculo)).ToString("F1", Inv) + " | " +
                    Mediana(grupo.Select(r => (double)r.Linhas)).ToString("F0", Inv) + " | " + Mediana(grupo.Select(r => r.LinhasPorSegundo)).ToString("F0", Inv) + " | " +
                    Mediana(grupo.Select(r => (double)r.MaximoFila)).ToString("F0", Inv) + " | " + Mediana(grupo.Select(r => r.MsBloqueado)).ToString("F1", Inv) + " | " +
                    Mediana(grupo.Select(r => (double)r.MsGravacao)).ToString("F0", Inv) + " |");
            }
            sb.AppendLine();
            sb.AppendLine("## Impacto da gravação sobre a simulação paralela (D contra B)");
            sb.AppendLine();
            sb.AppendLine("| N | grav a cada | B: simulação (ms) | D: simulação (ms) | acréscimo no que o usuário sente | D: total até gravar tudo (ms) | acréscimo total |");
            sb.AppendLine("|---:|---:|---:|---:|---:|---:|---:|");
            foreach (var g in rodadas.Where(r => r.Repeticao > 0 && r.Cenario == "D").GroupBy(r => new { r.N, r.GravACadaN }).OrderBy(g => g.Key.N).ThenBy(g => g.Key.GravACadaN))
            {
                double b = Mediana(rodadas.Where(r => r.Repeticao > 0 && r.Cenario == "B" && r.N == g.Key.N).Select(r => r.MsSimulacao));
                double dSim = Mediana(g.Select(r => r.MsSimulacao)), dTot = Mediana(g.Select(r => r.MsTotal));
                sb.AppendLine("| " + g.Key.N + " | " + g.Key.GravACadaN + " | " + b.ToString("F1", Inv) + " | " + dSim.ToString("F1", Inv) + " | " + (100 * (dSim - b) / b).ToString("F1", Inv) + "% | " +
                    dTot.ToString("F1", Inv) + " | " + (100 * (dTot - b) / b).ToString("F1", Inv) + "% |");
            }
            sb.AppendLine();
            sb.AppendLine("## Tamanho no banco");
            sb.AppendLine();
            sb.AppendLine("Ao final do benchmark: **" + totalLeituras.ToString("N0", Inv) + "** linhas em `leituras`; tabelas do simulador ocupam ≈ **" + tamanhoMb.ToString("F1", Inv) + " MB** (dados + índices, `information_schema`).");
            sb.AppendLine();
            return sb.ToString();
        }
    }
}
