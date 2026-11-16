using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace TransferenciaCalor.Benchmark
{
    /// <summary>
    /// Grava o CSV (dados brutos) e o Markdown (tabelas + análise derivada dos números medidos).
    /// Tudo que aparece como "estimativa" é cálculo sobre as medições, não medição direta.
    /// </summary>
    internal static class Relatorio
    {
        // Valores REAIS lidos do código do simulador (UI/MainForm.cs e UI/MainForm.Designer.cs):
        private const int TicksPorFrameBase = 20;      // MainForm.cs:449
        private const int VelocidadeMaxima = 10;       // MainForm.Designer.cs: trkVelocidade.Maximum
        private const double IntervaloTimerMs = 50.0;  // MainForm.Designer.cs: timerSimulacao.Interval
        private const int TicksPorSnapshot = 100;      // definido no enunciado da gravação em banco
        private const int NMaximoUi = 20;              // MainForm.Designer.cs: numTamanho.Maximum

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Gravar(string pasta, Ambiente amb, Resultados res)
        {
            string nome = string.Concat(amb.NomeMaquina.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_'));
            string csv = Path.Combine(pasta, "resultados_" + nome + ".csv");
            string md = Path.Combine(pasta, "resultados_" + nome + ".md");
            File.WriteAllText(csv, MontarCsv(amb, res), new UTF8Encoding(true));
            File.WriteAllText(md, MontarMarkdown(amb, res), new UTF8Encoding(true));
            Console.WriteLine("CSV: " + csv);
            Console.WriteLine("MD : " + md);
        }

        // ---------- CSV (formato "longo": uma linha por métrica e N) ----------
        internal static string MontarCsv(Ambiente amb, Resultados res)
        {
            Func<string, string> a = chave => amb.Itens.First(i => i.Key == chave).Value;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("maquina,cpu,nucleos_fisicos,nucleos_logicos,ram,windows,dotnet_framework,configuracao,plataforma,N,metrica,unidade,mediana,media,desvio_padrao,minimo,repeticoes,iteracoes_por_repeticao");
            string prefixo = string.Join(",", new[]
            {
                Csv(a("maquina")), Csv(a("cpu")), Csv(a("nucleos_fisicos")), Csv(a("nucleos_logicos")), Csv(a("ram")),
                Csv(a("windows")), Csv(a("dotnet_framework")), Csv(a("configuracao")), Csv(a("plataforma"))
            });
            foreach (Registro r in res.Todos)
            {
                sb.AppendLine(prefixo + "," + (r.N.HasValue ? r.N.Value.ToString(Inv) : "") + "," + Csv(r.Metrica) + "," + Csv(r.Unidade) + "," +
                    r.Mediana.ToString("R", Inv) + "," + r.Media.ToString("R", Inv) + "," + r.Desvio.ToString("R", Inv) + "," +
                    r.Minimo.ToString("R", Inv) + "," + r.Repeticoes.ToString(Inv) + "," + r.Iteracoes.ToString(Inv));
            }
            return sb.ToString();
        }

        private static string Csv(string valor)
        {
            return valor.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + valor.Replace("\"", "\"\"") + "\"" : valor;
        }

        // ---------- Markdown ----------
        internal static string US(double ns) { return double.IsNaN(ns) ? "n/m" : (ns / 1000.0).ToString(ns < 10000 ? "F3" : "F1", Inv); }

        private static string MontarMarkdown(Ambiente amb, Resultados res)
        {
            List<int> ns = res.TamanhosMedidos();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Resultados do benchmark — " + amb.NomeMaquina);
            sb.AppendLine();
            sb.AppendLine("Valores medidos pelo programa `Benchmark` (Release, sem debugger). \"n/m\" = não medido. Tempos em microssegundos (µs) por operação, salvo indicação.");
            sb.AppendLine();
            sb.AppendLine("## Ambiente");
            sb.AppendLine();
            sb.AppendLine("| Item | Valor |");
            sb.AppendLine("|---|---|");
            foreach (KeyValuePair<string, string> i in amb.Itens)
                sb.AppendLine("| " + i.Key + " | " + i.Value.Replace("|", "/") + " |");
            sb.AppendLine("| N máximo medido | " + ns.Max() + " |");
            sb.AppendLine();

            // Tabela principal (medianas)
            string[][] colunas =
            {
                new[] { "tick", "Tick()" },
                new[] { "equilibrio_misto", "Equilíbrio (misto)" },
                new[] { "equilibrio_completo", "Equilíbrio (varredura completa)" },
                new[] { "snapshot_novo", "Snapshot (arrays novos)" },
                new[] { "snapshot_reuso", "Snapshot (reuso)" },
                new[] { "alocar_matriz", "new double[n,n]" },
                new[] { "parallelfor_vazio_N", "Parallel.For vazio (N it.)" },
                new[] { "parallelfor_vazio_N2", "Parallel.For vazio (N² it.)" },
            };

            sb.AppendLine("## Tempo por operação — MEDIANA (µs)");
            sb.AppendLine();
            sb.AppendLine("| N | " + string.Join(" | ", colunas.Select(c => c[1])) + " |");
            sb.AppendLine("|---:|" + string.Join("|", colunas.Select(c => "---:")) + "|");
            foreach (int n in ns)
                sb.AppendLine("| " + n + " | " + string.Join(" | ", colunas.Select(c => US(res.Valor(n, c[0])))) + " |");
            sb.AppendLine();

            sb.AppendLine("## Dispersão — média ± desvio-padrão (µs) e coeficiente de variação");
            sb.AppendLine();
            sb.AppendLine("| N | " + string.Join(" | ", colunas.Select(c => c[1])) + " |");
            sb.AppendLine("|---:|" + string.Join("|", colunas.Select(c => "---:")) + "|");
            foreach (int n in ns)
            {
                List<string> cel = new List<string>();
                foreach (string[] c in colunas)
                {
                    Registro r = res.Obter(n, c[0]);
                    cel.Add(r == null ? "n/m" : US(r.Media) + " ± " + US(r.Desvio) + " (" + (r.Media > 0 ? (100 * r.Desvio / r.Media).ToString("F1", Inv) : "0") + "%)");
                }
                sb.AppendLine("| " + n + " | " + string.Join(" | ", cel) + " |");
            }
            sb.AppendLine();
            Registro rep = res.Obter(ns[0], "tick");
            sb.AppendLine("Repetições por medição: " + rep.Repeticoes + "; cada repetição executa a operação " +
                "o número de vezes necessário para durar ≥ ~100 ms (coluna `iteracoes_por_repeticao` do CSV).");
            sb.AppendLine();

            // INSERT em lote
            sb.AppendLine("## Montagem do texto de INSERT em lote (somente CPU, sem servidor)");
            sb.AppendLine();
            sb.AppendLine("| Linhas | Mediana (µs) | Média ± dp (µs) | ns/linha | Caracteres | Bytes alocados/op |");
            sb.AppendLine("|---:|---:|---:|---:|---:|---:|");
            foreach (int l in new[] { 1000, 5000 })
            {
                Registro r = res.Obter(null, "sql_" + l + "_linhas");
                if (r == null) continue;
                sb.AppendLine("| " + l + " | " + US(r.Mediana) + " | " + US(r.Media) + " ± " + US(r.Desvio) + " | " +
                    (r.Mediana / l).ToString("F1", Inv) + " | " + res.Valor(null, "sql_" + l + "_linhas_tamanho_texto").ToString("F0", Inv) + " | " +
                    res.Valor(null, "sql_" + l + "_linhas_bytes_por_op").ToString("F0", Inv) + " |");
            }
            sb.AppendLine();

            // Alocações
            sb.AppendLine("## Alocações e coletas de lixo");
            sb.AppendLine();
            sb.AppendLine("Coletas por 1.000 operações (gen0 / gen1 / gen2) e bytes alocados por operação, medidos em passada separada com `AppDomain.MonitoringIsEnabled`. O tamanho teórico de `new double[n,n]` é ≈ 8·N² + ~40 bytes; acima de 85.000 bytes (N ≳ 103) o array vai para o LOH (heap de objetos grandes), cuja coleta é gen2.");
            sb.AppendLine();
            sb.AppendLine("| N | Tick: bytes/tick | Tick: GC 0/1/2 por 1000 ticks | Equilíbrio (completo): bytes/chamada | Equilíbrio: GC 0/1/2 por 1000 | Snapshot novo: bytes/op | Temperaturas finitas |");
            sb.AppendLine("|---:|---:|---|---:|---|---:|:---:|");
            foreach (int n in ns)
            {
                sb.AppendLine("| " + n + " | " + res.Valor(n, "tick_bytes_por_op").ToString("F0", Inv) + " | " + Gcs(res, n, "tick") + " | " +
                    res.Valor(n, "equilibrio_completo_bytes_por_op").ToString("F0", Inv) + " | " + Gcs(res, n, "equilibrio_completo") + " | " +
                    res.Valor(n, "snapshot_novo_bytes_por_op").ToString("F0", Inv) + " | " +
                    (res.Valor(n, "tick_temperaturas_finitas") == 1 ? "sim" : "NÃO") + " |");
            }
            sb.AppendLine();

            AnaliseTempoReal(sb, res, ns);
            AnaliseFatias(sb, res, ns);
            AnaliseParalelo(sb, amb, res, ns);
            return sb.ToString();
        }

        private static string Gcs(Resultados res, int n, string m)
        {
            return res.Valor(n, m + "_gc_gen0").ToString("F2", Inv) + " / " + res.Valor(n, m + "_gc_gen1").ToString("F2", Inv) + " / " + res.Valor(n, m + "_gc_gen2").ToString("F2", Inv);
        }

        // ---------- Capacidade em tempo real ----------
        private static void AnaliseTempoReal(StringBuilder sb, Resultados res, List<int> ns)
        {
            double orcamentoNs = IntervaloTimerMs * 1e6;
            sb.AppendLine("## Capacidade em tempo real (cálculo sobre as medições)");
            sb.AppendLine();
            sb.AppendLine("Valores reais do código: `TicksPorFrameBase` = " + TicksPorFrameBase + " (MainForm.cs:449); velocidade 1x–" + VelocidadeMaxima +
                "x (TrackBar); Timer a " + IntervaloTimerMs + " ms (Designer); 1 chamada de `AtingiuEquilibrio()` por frame (MainForm.cs:490). Pior caso da UI = " +
                (TicksPorFrameBase * VelocidadeMaxima) + " ticks por frame = " + (TicksPorFrameBase * VelocidadeMaxima * 1000.0 / IntervaloTimerMs).ToString("F0", Inv) + " ticks/s.");
            sb.AppendLine("Custo do frame = ticks_por_frame × Tick() + 1 × AtingiuEquilibrio() (varredura completa, caso pessimista). **Não inclui** `AtualizarTendencias`, `Invalidate`/`Paint` (GDI+) nem o próprio Timer do Windows, que não foram medidos; portanto a folga real da UI é MENOR que a mostrada.");
            sb.AppendLine();
            sb.AppendLine("| N | Ticks/s sustentados pelo motor | Frame 1x (ms) | Frame 5x (ms) | Frame 10x (ms) | % do orçamento de 50 ms a 10x | Acompanha 10x? |");
            sb.AppendLine("|---:|---:|---:|---:|---:|---:|:---:|");
            List<double> custoPorCelula = new List<double>();
            foreach (int n in ns)
            {
                double tick = res.Valor(n, "tick"), eq = res.Valor(n, "equilibrio_completo");
                Func<int, double> frame = v => (TicksPorFrameBase * v * tick + eq) / 1e6;
                double f10 = frame(VelocidadeMaxima);
                if (n >= 60) custoPorCelula.Add((TicksPorFrameBase * VelocidadeMaxima * tick + eq) / ((double)n * n));
                sb.AppendLine("| " + n + " | " + (1e9 / tick).ToString("F0", Inv) + " | " + frame(1).ToString("F3", Inv) + " | " + frame(5).ToString("F3", Inv) + " | " +
                    f10.ToString("F3", Inv) + " | " + (100 * f10 * 1e6 / orcamentoNs).ToString("F1", Inv) + "% | " + (f10 * 1e6 < orcamentoNs ? "sim" : "NÃO") + " |");
            }
            sb.AppendLine();
            if (custoPorCelula.Count > 0)
            {
                double c = custoPorCelula.OrderBy(x => x).ElementAt(custoPorCelula.Count / 2);
                double nLimite = Math.Sqrt(orcamentoNs / c);
                sb.AppendLine("**Estimativa** (custo ≈ proporcional a N²; ≈ " + c.ToString("F1", Inv) + " ns por célula no frame a 10x, mediana dos N ≥ 60): o motor sozinho deixa de caber nos 50 ms a 10x perto de **N ≈ " + nLimite.ToString("F0", Inv) + "**. " +
                    "A UI hoje limita N a " + NMaximoUi + " (numTamanho.Maximum). Com o desenho + GDI+ (não medidos) o limite prático é menor.");
                sb.AppendLine();
            }
        }

        // ---------- Fatias de custo ----------
        private static void AnaliseFatias(StringBuilder sb, Resultados res, List<int> ns)
        {
            sb.AppendLine("## Fatia do custo por tick (cálculo sobre as medições)");
            sb.AppendLine();
            sb.AppendLine("Custo amortizado por tick = Tick() + AtingiuEquilibrio()/(ticks entre chamadas) + Snapshot/" + TicksPorSnapshot + " + montagem do INSERT/" + TicksPorSnapshot +
                " (INSERT = N² linhas × ns/linha medido com 5.000 linhas). Cenários: **1x** (" + TicksPorFrameBase + " ticks entre chamadas de equilíbrio), **10x** (" +
                (TicksPorFrameBase * VelocidadeMaxima) + ") e **cada tick** (se o equilíbrio fosse verificado a todo tick, como o laço antigo).");
            sb.AppendLine();
            sb.AppendLine("| N | Cenário | Tick() | Equilíbrio | Snapshot | INSERT | Total (µs/tick) | Domina |");
            sb.AppendLine("|---:|---|---:|---:|---:|---:|---:|---|");
            double nsPorLinha = res.Valor(null, "sql_5000_linhas") / 5000.0;
            foreach (int n in ns)
            {
                double tick = res.Valor(n, "tick"), eq = res.Valor(n, "equilibrio_completo"), snap = res.Valor(n, "snapshot_novo");
                double sql = nsPorLinha * n * n;
                foreach (var cen in new[] { Tuple.Create("1x", (double)TicksPorFrameBase), Tuple.Create("10x", (double)(TicksPorFrameBase * VelocidadeMaxima)), Tuple.Create("cada tick", 1.0) })
                {
                    double pEq = eq / cen.Item2, pSnap = snap / TicksPorSnapshot, pSql = sql / TicksPorSnapshot;
                    double total = tick + pEq + pSnap + pSql;
                    string dom = new[] { Tuple.Create("Tick()", tick), Tuple.Create("Equilíbrio", pEq), Tuple.Create("Snapshot", pSnap), Tuple.Create("INSERT", pSql) }.OrderByDescending(t => t.Item2).First().Item1;
                    sb.AppendLine("| " + n + " | " + cen.Item1 + " | " + Pct(tick, total) + " | " + Pct(pEq, total) + " | " + Pct(pSnap, total) + " | " + Pct(pSql, total) + " | " + US(total) + " | " + dom + " |");
                }
            }
            sb.AppendLine();
        }

        private static string Pct(double parte, double total) { return (100 * parte / total).ToString("F1", Inv) + "%"; }

        // ---------- Paralelismo: ponto de cruzamento e Amdahl ----------
        private static void AnaliseParalelo(StringBuilder sb, Ambiente amb, Resultados res, List<int> ns)
        {
            const int regioes = 2; // desenho proposto: (1) fluxo por aresta, (2) soma/atualização por corpo
            sb.AppendLine("## Paralelismo — ponto de cruzamento e Lei de Amdahl (ESTIMATIVAS, não medições)");
            sb.AppendLine();
            sb.AppendLine("O desenho proposto precisa de " + regioes + " regiões paralelas por tick (fluxo por aresta; depois soma por corpo/atualização). " +
                "Referência de custo de agendar: `Parallel.For` vazio com N iterações (uma por linha) × " + regioes + ".");
            sb.AppendLine();
            sb.AppendLine("| N | Tick() (µs) | Overhead de " + regioes + "× Parallel.For vazio (µs) | Tick / overhead | Equilíbrio / overhead |");
            sb.AppendLine("|---:|---:|---:|---:|---:|");
            List<Tuple<int, double>> razoes = new List<Tuple<int, double>>();
            foreach (int n in ns)
            {
                double tick = res.Valor(n, "tick"), eq = res.Valor(n, "equilibrio_completo"), pf = regioes * res.Valor(n, "parallelfor_vazio_N");
                razoes.Add(Tuple.Create(n, tick / pf));
                sb.AppendLine("| " + n + " | " + US(tick) + " | " + US(pf) + " | " + (tick / pf).ToString("F2", Inv) + "× | " + (eq / pf).ToString("F2", Inv) + "× |");
            }
            sb.AppendLine();
            foreach (double alvo in new[] { 1.0, 4.0 })
                sb.AppendLine("- Tick() passa a custar " + alvo.ToString("F0", Inv) + "× o overhead de agendamento em **N ≈ " + Cruzamento(razoes, alvo) +
                    "** (interpolação log-log entre os N medidos)" + (alvo == 1.0 ? " — abaixo disso, paralelizar PERDE tempo." : " — ali o overhead já é ≤ 25% do trabalho, mínimo razoável para haver ganho."));
            sb.AppendLine();

            int fis = amb.NucleosFisicos > 0 ? amb.NucleosFisicos : amb.NucleosLogicos;
            sb.AppendLine("Modelo: fração paralelizável p = 1 − (parte serial). Parte serial considerada = só `new double[n,n]` (medida), pois o restante é laço sobre células independentes **se** o fluxo for calculado por aresta. " +
                "Amdahl: S = 1/((1−p)+p/P). Com overhead: T_par = (1−p)·T + p·T/P + " + regioes + "·Parallel.For(N). P = " + fis + " núcleos físicos / " + amb.NucleosLogicos + " lógicos (só o P físico é usado: hyper-threading não rende 2× e extrapolar para 16 daria ganhos irreais). " +
                "Não considera largura de banda de memória nem falso compartilhamento de cache, então é um teto otimista.");
            sb.AppendLine();
            sb.AppendLine("| N | p (Tick) | Amdahl P=" + fis + " | Modelo c/ overhead P=" + fis + " | Equilíbrio: modelo c/ overhead P=" + fis + " |");
            sb.AppendLine("|---:|---:|---:|---:|---:|");
            foreach (int n in ns)
            {
                double tick = res.Valor(n, "tick"), eq = res.Valor(n, "equilibrio_completo"), aloc = res.Valor(n, "alocar_matriz"), pf = regioes * res.Valor(n, "parallelfor_vazio_N");
                double p = Math.Max(0, 1 - aloc / tick);
                double pe = Math.Max(0, 1 - aloc / eq);
                Func<double, double, double, double, double> modelo = (t, pp, P, o) => t / ((1 - pp) * t + pp * t / P + o);
                sb.AppendLine("| " + n + " | " + p.ToString("F3", Inv) + " | " + (1 / ((1 - p) + p / fis)).ToString("F2", Inv) + "× | " +
                    modelo(tick, p, fis, pf).ToString("F2", Inv) + "× | " +
                    modelo(eq, pe, fis, pf).ToString("F2", Inv) + "× |");
            }
            sb.AppendLine();
            sb.AppendLine("Valores < 1× no modelo significam que paralelizar daria PERDA. Confirmar só com a implementação real da N2.");
            sb.AppendLine();
        }

        // Interpolação log-log para achar o N em que tick/overhead = alvo
        private static string Cruzamento(List<Tuple<int, double>> razoes, double alvo)
        {
            if (razoes[0].Item2 >= alvo) return "< " + razoes[0].Item1 + " (já acima em todos os N medidos)";
            for (int i = 1; i < razoes.Count; i++)
            {
                double r1 = razoes[i - 1].Item2, r2 = razoes[i].Item2;
                if (r1 < alvo && r2 >= alvo)
                {
                    double n1 = razoes[i - 1].Item1, n2 = razoes[i].Item1;
                    double ln = Math.Log(n1) + (Math.Log(alvo) - Math.Log(r1)) * (Math.Log(n2) - Math.Log(n1)) / (Math.Log(r2) - Math.Log(r1));
                    return Math.Exp(ln).ToString("F0", Inv);
                }
            }
            return "> " + razoes[razoes.Count - 1].Item1 + " (não atingido nos N medidos)";
        }
    }
}
