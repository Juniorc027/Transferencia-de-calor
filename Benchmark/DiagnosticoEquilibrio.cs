using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TransferenciaCalor.Data;
using TransferenciaCalor.Models;
using TransferenciaCalor.Simulation;

namespace TransferenciaCalor.Benchmark
{
    /// <summary>
    /// Diagnóstico SOMENTE LEITURA do critério de equilíbrio (AtingiuEquilibrio): roda cenários sem UI e,
    /// a cada 1.000 ticks, mede quanto cada bloco ainda varia e por que o critério responde o que responde.
    /// Não altera nenhum motor: reimplementa a leitura do critério usando a grade pública.
    /// Uso: Benchmark.exe --diagnostico-equilibrio [--ticks 2000000] [--saida pasta]
    /// </summary>
    internal static class DiagnosticoEquilibrio
    {
        private const double Delta_t = 0.01;
        private const double Limiar = 1e-4;
        private const double LadoCubo = 0.1;
        private const int Janela = 64;          // ticks guardados para medir amplitude e período do ciclo
        private const int Passo = 1000;         // intervalo entre linhas do diagnóstico

        /// <summary>Uma das 3 implementações, rodando em paralelo (lockstep) a mesma grade inicial.</summary>
        private sealed class Instancia
        {
            private readonly string nome;
            private readonly HeatGrid grade;
            private readonly IMotorSimulacao motor;
            public Instancia(string nome, HeatGrid grade, IMotorSimulacao motor)
            {
                this.nome = nome; this.grade = grade; this.motor = motor;
            }
            public string Nome { get { return nome; } }
            public HeatGrid Grade { get { return grade; } }
            public IMotorSimulacao Motor { get { return motor; } }
        }

        /// <summary>Resultado da leitura do critério em um instante.</summary>
        private sealed class LeituraCriterio
        {
            private readonly bool todosOk;
            private readonly double diferenca, tolerancia, oscilacao, razao;
            private readonly int linhaA, colunaA, linhaB, colunaB;
            public LeituraCriterio(bool todosOk, double diferenca, double tolerancia, double oscilacao, double razao,
                int linhaA, int colunaA, int linhaB, int colunaB)
            {
                this.todosOk = todosOk; this.diferenca = diferenca; this.tolerancia = tolerancia;
                this.oscilacao = oscilacao; this.razao = razao;
                this.linhaA = linhaA; this.colunaA = colunaA; this.linhaB = linhaB; this.colunaB = colunaB;
            }
            public bool TodosOk { get { return todosOk; } }
            public double Diferenca { get { return diferenca; } }          // |Qa-Qb| do pior par
            public double Tolerancia { get { return tolerancia; } }        // limiarRelativo x max(Q)
            public double Oscilacao { get { return oscilacao; } }          // energia trocada por tick (soma dos dois corpos)
            public double LimiarEfetivo { get { return Math.Max(tolerancia, oscilacao); } }
            public double Razao { get { return razao; } }                  // diferenca / limiarEfetivo (>1 = reprovado)
            public int LinhaA { get { return linhaA; } }
            public int ColunaA { get { return colunaA; } }
            public int LinhaB { get { return linhaB; } }
            public int ColunaB { get { return colunaB; } }
        }

        private static Material Mat(string nome)
        {
            return MaterialCatalog.Todos.First(m => m.Nome == nome);
        }

        // 3x3 misto: cobre, alumínio, ferro e ouro repetidos em sequência; centro a 1000 K, resto 300 K.
        // (A posição exata de cada material na tela do usuário não foi informada: este é um layout fixo e declarado.)
        private static HeatGrid Misto3x3()
        {
            string[] ciclo = { "Cobre", "Alumínio", "Ferro", "Ouro" };
            HeatGrid g = new HeatGrid(3, Mat("Cobre"), LadoCubo, 300.0);
            for (int l = 0; l < 3; l++)
                for (int c = 0; c < 3; c++)
                    g.TrocarMaterial(l, c, Mat(ciclo[(l * 3 + c) % 4]));
            g.ObterCorpo(1, 1).TemperaturaAtual = 1000.0;
            return g;
        }

        private static HeatGrid FerroUnico(int n, double temperaturaCentro)
        {
            HeatGrid g = new HeatGrid(n, Mat("Ferro"), LadoCubo, 300.0);
            g.ObterCorpo(n / 2, n / 2).TemperaturaAtual = temperaturaCentro;
            return g;
        }

        // Varredura: sorteia N layouts 3x3 com cobre/alumínio/ferro/ouro (centro 1000 K, resto 300 K, semente fixa)
        // e compara o critério ANTIGO (réplica só de leitura, LerCriterio) com o NOVO (o AtingiuEquilibrio() real do
        // N1 e do N2 sequencial, que devem concordar). Verifica o tick de disparo de cada um.
        private static int Varredura(int layouts, long ticks, int detalhar)
        {
            string[] nomes = { "Cobre", "Alumínio", "Ferro", "Ouro" };
            Random rng = new Random(20261115);
            Console.WriteLine("layout  materiais(linha a linha)          antigo (tick)  novo N1 (tick)  N1=N2seq");

            HeatGrid uniforme = new HeatGrid(3, Mat("Cobre"), LadoCubo, 300.0);
            for (int l = 0; l < 3; l++) for (int c = 0; c < 3; c++) uniforme.TrocarMaterial(l, c, Mat(nomes[(l * 3 + c) % 4]));
            Console.WriteLine("Controle: 3x3 misto, TODOS a 300 K (fluxo exatamente 0): antigo={0}  novo={1}",
                LerCriterio(uniforme).TodosOk, new HeatSimulationEngine(uniforme, Delta_t, Limiar).AtingiuEquilibrio());

            int antigoDispara = 0, antigoNao = 0, idem = 0, novoAntes = 0, novoDispara = 0, novoNao = 0, divergenciasSeq = 0;
            for (int k = 0; k < layouts; k++)
            {
                string[] mat = new string[9];
                StringBuilder desc = new StringBuilder();
                for (int i = 0; i < 9; i++)
                {
                    mat[i] = nomes[rng.Next(4)];
                    desc.Append(mat[i].Substring(0, 2));
                    if (i % 3 == 2) desc.Append(' ');
                }
                if (detalhar >= 0 && k != detalhar) continue; // modo detalhe: só simula o layout pedido

                HeatGrid g = MontarLayout(mat), gs = MontarLayout(mat);
                HeatSimulationEngine motor = new HeatSimulationEngine(g, Delta_t, Limiar);
                HeatSimulationEngineParalelo seq = new HeatSimulationEngineParalelo(gs, Delta_t, Limiar, 0, 1, true);

                long achouAntigo = -1, achouNovo = -1, tFim = 0;
                double[] tNovo = null;
                for (long t = 1; t <= ticks; t++)
                {
                    motor.Tick(); seq.Tick();
                    if (t % Passo != 0) continue;
                    bool novo = motor.AtingiuEquilibrio();
                    if (novo != seq.AtingiuEquilibrio()) divergenciasSeq++;
                    if (achouNovo < 0 && novo) { achouNovo = t; tNovo = Temperaturas(g); }
                    tFim = t;
                    if (achouAntigo < 0 && LerCriterio(g).TodosOk) achouAntigo = t;
                    if (achouAntigo >= 0) break; // o novo aceita tudo que o antigo aceita: já decidido
                    if (detalhar == k && t % 100000 == 0)
                        Console.WriteLine("    [layout {0}] tick {1}: razao antiga {2:F1}  razao nova {3:F1}", k, t, LerCriterio(g).Razao, RazaoNova(g));
                }

                if (achouAntigo >= 0) { antigoDispara++; if (achouNovo == achouAntigo) idem++; else if (achouNovo >= 0 && achouNovo < achouAntigo) novoAntes++; }
                else antigoNao++;
                if (achouNovo >= 0) novoDispara++; else novoNao++;
                // Deriva depois do disparo do novo: quanto as temperaturas ainda andaram até o fim da rodada
                double deriva = 0;
                if (tNovo != null)
                {
                    double[] fim = Temperaturas(g);
                    for (int i = 0; i < fim.Length; i++) deriva = Math.Max(deriva, Math.Abs(fim[i] - tNovo[i]));
                }
                Console.WriteLine("{0,5}   {1,-30} antigo={2,-9} novo={3,-9} {4}  deriva apos o disparo novo: {5:E2} K em {6} ticks",
                    k, desc.ToString(), achouAntigo < 0 ? "nao" : achouAntigo.ToString(), achouNovo < 0 ? "NUNCA" : achouNovo.ToString(),
                    achouAntigo >= 0 ? (achouNovo == achouAntigo ? "mesmo tick" : "TICK DIFERENTE") : "", deriva, achouNovo < 0 ? 0 : tFim - achouNovo);
            }
            Console.WriteLine("Antigo disparou em {0} layouts; destes, novo no MESMO tick em {1}, novo ANTES do antigo em {2}.", antigoDispara, idem, novoAntes);
            Console.WriteLine("Novo disparou em {0}; nao disparou em {1} (antigo nao disparou em {2}). Divergencias N1 x N2seq: {3}.", novoDispara, novoNao, antigoNao, divergenciasSeq);
            return 0;
        }

        private static HeatGrid MontarLayout(string[] materiais)
        {
            HeatGrid g = new HeatGrid(3, Mat("Cobre"), LadoCubo, 300.0);
            for (int i = 0; i < 9; i++) g.TrocarMaterial(i / 3, i % 3, Mat(materiais[i]));
            g.ObterCorpo(1, 1).TemperaturaAtual = 1000.0;
            return g;
        }

        // Pior razão do critério NOVO: por par vale o menor entre (diferença de Q / limiar efetivo) e (|ΔT| / limiar de T);
        // >1 = ainda reprovado. Serve para estimar quanto falta quando o critério não dispara.
        private static double RazaoNova(HeatGrid g)
        {
            int n = g.Tamanho;
            double[,] energia = new double[n, n];
            for (int l = 0; l < n; l++)
                for (int c = 0; c < n; c++)
                {
                    if (c < n - 1) Acumular(g, l, c, l, c + 1, energia);
                    if (l < n - 1) Acumular(g, l, c, l + 1, c, energia);
                }
            double pior = 0;
            for (int l = 0; l < n; l++)
                for (int c = 0; c < n; c++)
                {
                    if (c < n - 1) pior = Math.Max(pior, RazaoPar(g, l, c, l, c + 1, energia));
                    if (l < n - 1) pior = Math.Max(pior, RazaoPar(g, l, c, l + 1, c, energia));
                }
            return pior;
        }

        private static double RazaoPar(HeatGrid g, int la, int ca, int lb, int cb, double[,] energia)
        {
            Body a = g.ObterCorpo(la, ca), b = g.ObterCorpo(lb, cb);
            double qa = a.CalcularCalorSensivel(), qb = b.CalcularCalorSensivel();
            double lim = Math.Max(Limiar * Math.Max(qa, qb), energia[la, ca] + energia[lb, cb]);
            double rq = lim > 0 ? Math.Abs(qa - qb) / lim : 0;
            double rt = Math.Abs(a.TemperaturaAtual - b.TemperaturaAtual) / (Limiar * Math.Max(a.TemperaturaAtual, b.TemperaturaAtual));
            return Math.Min(rq, rt);
        }

        // Compara, a CADA tick, o disparo do critério antigo (réplica) com o novo nas 3 implementações (N1, N2-seq, N2-par).
        private static int Comparar(long ticks)
        {
            CompararDisparo("3x3 so ferro, centro 1000 K", () => FerroUnico(3, 1000.0), ticks);
            CompararDisparo("5x5 so ferro, centro 1000 K", () => FerroUnico(5, 1000.0), ticks);
            CompararDisparo("3x3 misto (Cu/Al/Fe/Au), centro 1000 K", () => Misto3x3(), ticks);
            return 0;
        }

        private static void CompararDisparo(string titulo, Func<HeatGrid> criar, long ticks)
        {
            HeatGrid g1 = criar(), g2 = criar(), g3 = criar();
            HeatSimulationEngine n1 = new HeatSimulationEngine(g1, Delta_t, Limiar);
            HeatSimulationEngineParalelo seq = new HeatSimulationEngineParalelo(g2, Delta_t, Limiar, 0, 1, true);
            HeatSimulationEngineParalelo par = new HeatSimulationEngineParalelo(g3, Delta_t, Limiar, 4, 1, false);
            long antigo = -1, novoN1 = -1, novoSeq = -1, novoPar = -1, desacordos = 0, t;
            for (t = 0; t <= ticks; t++)
            {
                if (t > 0) { n1.Tick(); seq.Tick(); par.Tick(); }
                bool a = n1.AtingiuEquilibrio(), b = seq.AtingiuEquilibrio(), c = par.AtingiuEquilibrio();
                if (a != b || a != c) desacordos++;
                if (novoN1 < 0 && a) novoN1 = t;
                if (novoSeq < 0 && b) novoSeq = t;
                if (novoPar < 0 && c) novoPar = t;
                if (antigo < 0 && LerCriterio(g1).TodosOk) antigo = t;
                if (antigo >= 0 && novoN1 >= 0) break;
                if (novoN1 >= 0 && t > novoN1 + 200000) break; // antigo ainda não disparou 200 mil ticks depois do novo
            }
            Console.WriteLine("{0}: antigo(replica)={1}  novo N1={2}  novo N2-seq={3}  novo N2-par={4}  ticks em que as 3 divergiram={5}",
                titulo, antigo < 0 ? "nao" : antigo.ToString(), novoN1 < 0 ? "nao" : novoN1.ToString(),
                novoSeq < 0 ? "nao" : novoSeq.ToString(), novoPar < 0 ? "nao" : novoPar.ToString(), desacordos);
        }

        public static int Executar(string[] args, string pastaSaida)
        {
            long ticks = 2000000;
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "--ticks") ticks = long.Parse(args[i + 1]);
            if (args.Contains("--comparar-criterio"))
                return Comparar(ticks);
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "--varredura")
                {
                    int det = -1;
                    for (int j = 0; j < args.Length - 1; j++) if (args[j] == "--detalhar") det = int.Parse(args[j + 1]);
                    return Varredura(int.Parse(args[i + 1]), ticks, det);
                }

            StringBuilder csv = new StringBuilder();
            csv.AppendLine("cenario;tick;max_dT_por_tick;bloco_max;oscilacao_pior_par;limiar_relativo_pior_par;limiar_efetivo;diferenca_Q_pior_par;razao;par_pior;AtingiuEquilibrio_N1;replica_do_criterio;amplitude_T_bloco_64ticks;periodo_ciclo_ticks;inversoes_64ticks;N1_igual_N2seq;N1_igual_N2par");

            Rodar("3x3 misto (Cu/Al/Fe/Au), centro 1000 K", () => Misto3x3(), ticks, csv);
            Rodar("3x3 so ferro, centro 1000 K (controle)", () => FerroUnico(3, 1000.0), ticks, csv);
            Rodar("15x15 so ferro, centro 1200 K", () => FerroUnico(15, 1200.0), ticks, csv);

            string arquivo = Path.Combine(pastaSaida, "diagnostico_equilibrio.csv");
            File.WriteAllText(arquivo, csv.ToString(), new UTF8Encoding(true));
            Console.WriteLine("\nCSV completo (uma linha por 1.000 ticks): " + arquivo);
            return 0;
        }

        private static void Rodar(string titulo, Func<HeatGrid> criar, long ticks, StringBuilder csv)
        {
            Console.WriteLine("\n=== " + titulo + " ===");
            HeatGrid g1 = criar(), g2 = criar(), g3 = criar();
            Instancia n1 = new Instancia("N1", g1, new HeatSimulationEngine(g1, Delta_t, Limiar));
            Instancia seq = new Instancia("N2-seq", g2, new HeatSimulationEngineParalelo(g2, Delta_t, Limiar, 0, 1, true));
            // tamanhoMinimo = 1: força o caminho Parallel.For mesmo numa grade pequena (3x3)
            Instancia par = new Instancia("N2-par", g3, new HeatSimulationEngineParalelo(g3, Delta_t, Limiar, 4, 1, false));

            int n = g1.Tamanho, total = n * n;
            double[][] anel = new double[Janela][];
            for (int i = 0; i < Janela; i++) anel[i] = new double[total];
            double[] anterior = Temperaturas(g1);

            long primeiroEquilibrio = -1;
            long divergencias = 0, divergenciasEq = 0;
            Console.WriteLine("{0,9} {1,12} {2,6} {3,12} {4,12} {5,12} {6,9} {7,6} {8,9} {9,7} {10}",
                "tick", "max|dT|/tick", "bloco", "oscil(J)", "lim.rel(J)", "lim.efet(J)", "razao", "Atingiu", "ampl.(K)", "periodo", "N1=N2s=N2p");

            for (long t = 1; t <= ticks; t++)
            {
                n1.Motor.Tick(); seq.Motor.Tick(); par.Motor.Tick();
                double[] atual = anel[t % Janela];
                Copiar(g1, atual);

                if (t % Passo != 0) { continue; }

                // max |ΔT| em UM tick (este tick contra o anterior), e qual bloco
                double[] ant = anel[(t - 1) % Janela];
                double maxDt = 0; int blocoMax = 0;
                for (int i = 0; i < total; i++)
                {
                    double d = Math.Abs(atual[i] - ant[i]);
                    if (d > maxDt) { maxDt = d; blocoMax = i; }
                }

                LeituraCriterio crit = LerCriterio(g1);
                bool atingiu = n1.Motor.AtingiuEquilibrio();
                bool atingiuSeq = seq.Motor.AtingiuEquilibrio();
                bool atingiuPar = par.Motor.AtingiuEquilibrio();

                bool igualSeq = Iguais(g1, g2), igualPar = Iguais(g1, g3);
                if (!igualSeq || !igualPar) divergencias++;
                if (atingiu != atingiuSeq || atingiu != atingiuPar) divergenciasEq++;
                if (atingiu && primeiroEquilibrio < 0) primeiroEquilibrio = t;

                // Ciclo: série de temperatura do bloco de maior variação nos últimos 64 ticks
                double[] serie = new double[Janela];
                for (int k = 0; k < Janela; k++) serie[k] = anel[(t - (Janela - 1) + k + Janela * 4) % Janela][blocoMax];
                // (só vale com t >= 64; antes disso os zeros iniciais distorcem, então marcamos como "-")
                double amplitude = double.NaN; int periodo = 0, inversoes = 0;
                if (t >= Janela)
                {
                    double mn = serie.Min(), mx = serie.Max();
                    amplitude = mx - mn;
                    periodo = PeriodoExato(serie);
                    inversoes = ContarInversoes(serie);
                }

                string bloco = string.Format("({0},{1})", blocoMax / n, blocoMax % n);
                string par_ = string.Format("({0},{1})-({2},{3})", crit.LinhaA, crit.ColunaA, crit.LinhaB, crit.ColunaB);
                string trio = (igualSeq && igualPar) ? "igual" : "DIFERE";

                csv.AppendLine(string.Join(";", new[] {
                    titulo, t.ToString(), F(maxDt), bloco, F(crit.Oscilacao), F(crit.Tolerancia), F(crit.LimiarEfetivo),
                    F(crit.Diferenca), F(crit.Razao), par_, atingiu.ToString(), crit.TodosOk.ToString(),
                    double.IsNaN(amplitude) ? "" : F(amplitude), periodo.ToString(), inversoes.ToString(), igualSeq.ToString(), igualPar.ToString() }));

                // No console: as 6 primeiras linhas, depois de 100.000 em 100.000 (o CSV tem todas)
                if (t <= 6 * Passo || t % 100000 == 0 || (atingiu && t == primeiroEquilibrio))
                    Console.WriteLine("{0,9} {1,12:E3} {2,6} {3,12:E3} {4,12:E3} {5,12:E3} {6,9:F3} {7,6} {8,9:E2} {9,7} {10}",
                        t, maxDt, bloco, crit.Oscilacao, crit.Tolerancia, crit.LimiarEfetivo, crit.Razao,
                        atingiu ? "TRUE" : "false", amplitude, periodo == 0 ? "-" : periodo.ToString(), trio);

                // Se já atingiu o equilíbrio, as 3 versões concordam e ficamos 5.000 ticks para confirmar; depois encerra
                if (atingiu && t >= primeiroEquilibrio + 5000) break;
            }

            Console.WriteLine("Primeiro AtingiuEquilibrio()==true: {0}", primeiroEquilibrio < 0 ? "NUNCA (até o fim da rodada)" : "tick " + primeiroEquilibrio);
            Console.WriteLine("Amostras em que N1, N2-seq e N2-par divergiram nas temperaturas (bit a bit): {0}", divergencias);
            Console.WriteLine("Amostras em que o AtingiuEquilibrio() das 3 versões (ou a réplica do critério) divergiu: {0}", divergenciasEq);
            Console.WriteLine("Temperaturas finais N1 (K):");
            for (int l = 0; l < n && n <= 5; l++)
            {
                StringBuilder sb = new StringBuilder("  ");
                for (int c = 0; c < n; c++) sb.Append(g1.ObterCorpo(l, c).TemperaturaAtual.ToString("F4", CultureInfo.InvariantCulture)).Append("  ");
                Console.WriteLine(sb.ToString());
            }
        }

        // Réplica SOMENTE LEITURA do critério de HeatSimulationEngine.AtingiuEquilibrio (mesmas contas e fórmulas),
        // devolvendo o pior par (maior diferenca / limiarEfetivo) para podermos ver o número que o reprova.
        private static LeituraCriterio LerCriterio(HeatGrid g)
        {
            int n = g.Tamanho;
            double[,] energia = new double[n, n];
            for (int l = 0; l < n; l++)
                for (int c = 0; c < n; c++)
                {
                    if (c < n - 1) Acumular(g, l, c, l, c + 1, energia);
                    if (l < n - 1) Acumular(g, l, c, l + 1, c, energia);
                }

            bool todosOk = true;
            double pDif = 0, pTol = 0, pOsc = 0, pRazao = -1;
            int pla = 0, pca = 0, plb = 0, pcb = 0;
            for (int l = 0; l < n; l++)
                for (int c = 0; c < n; c++)
                {
                    if (c < n - 1) Avaliar(g, l, c, l, c + 1, energia, ref todosOk, ref pDif, ref pTol, ref pOsc, ref pRazao, ref pla, ref pca, ref plb, ref pcb);
                    if (l < n - 1) Avaliar(g, l, c, l + 1, c, energia, ref todosOk, ref pDif, ref pTol, ref pOsc, ref pRazao, ref pla, ref pca, ref plb, ref pcb);
                }
            return new LeituraCriterio(todosOk, pDif, pTol, pOsc, pRazao, pla, pca, plb, pcb);
        }

        private static void Avaliar(HeatGrid g, int la, int ca, int lb, int cb, double[,] energia, ref bool todosOk,
            ref double pDif, ref double pTol, ref double pOsc, ref double pRazao, ref int pla, ref int pca, ref int plb, ref int pcb)
        {
            double qa = g.ObterCorpo(la, ca).CalcularCalorSensivel();
            double qb = g.ObterCorpo(lb, cb).CalcularCalorSensivel();
            double dif = Math.Abs(qa - qb);
            double tol = Limiar * Math.Max(qa, qb);
            double osc = energia[la, ca] + energia[lb, cb];
            double lim = Math.Max(tol, osc);
            if (!(dif <= lim)) todosOk = false;
            double razao = lim > 0 ? dif / lim : (dif > 0 ? double.PositiveInfinity : 0);
            if (razao > pRazao) { pRazao = razao; pDif = dif; pTol = tol; pOsc = osc; pla = la; pca = ca; plb = lb; pcb = cb; }
        }

        private static void Acumular(HeatGrid g, int la, int ca, int lb, int cb, double[,] energia)
        {
            Body a = g.ObterCorpo(la, ca), b = g.ObterCorpo(lb, cb);
            double e = Math.Abs(Fluxo(a, b)) * Delta_t;
            energia[la, ca] += e;
            energia[lb, cb] += e;
        }

        // Mesma fórmula de HeatSimulationEngine.CalcularFluxoCalor (cópia só para leitura)
        private static double Fluxo(Body a, Body b)
        {
            double qa = a.CalcularCalorSensivel(), qb = b.CalcularCalorSensivel();
            if (qa == qb) return 0.0;
            Body quente = qa > qb ? a : b, frio = qa > qb ? b : a;
            double k = Math.Min(a.Material.K, b.Material.K);
            double area = Math.Pow(Math.Min(a.LadoCubo, b.LadoCubo), 2);
            double q = k * area * Math.Abs(quente.TemperaturaAtual - frio.TemperaturaAtual);
            return quente == a ? q : -q;
        }

        private static double[] Temperaturas(HeatGrid g)
        {
            double[] r = new double[g.Tamanho * g.Tamanho];
            Copiar(g, r);
            return r;
        }

        private static void Copiar(HeatGrid g, double[] destino)
        {
            int n = g.Tamanho;
            for (int l = 0; l < n; l++)
                for (int c = 0; c < n; c++)
                    destino[l * n + c] = g.ObterCorpo(l, c).TemperaturaAtual;
        }

        private static bool Iguais(HeatGrid a, HeatGrid b)
        {
            int n = a.Tamanho;
            for (int l = 0; l < n; l++)
                for (int c = 0; c < n; c++)
                    if (BitConverter.DoubleToInt64Bits(a.ObterCorpo(l, c).TemperaturaAtual) != BitConverter.DoubleToInt64Bits(b.ObterCorpo(l, c).TemperaturaAtual))
                        return false;
            return true;
        }

        // Menor p (1..32) tal que a série se repete exatamente (|diferença| <= 1e-9 K); 0 se não é periódica
        private static int PeriodoExato(double[] s)
        {
            for (int p = 1; p <= Janela / 2; p++)
            {
                bool ok = true;
                for (int i = 0; i + p < s.Length && ok; i++)
                    ok = Math.Abs(s[i] - s[i + p]) <= 1e-9;
                if (ok) return p;
            }
            return 0;
        }

        // Quantas vezes o sentido da variação (sobe/desce) muda dentro da janela
        private static int ContarInversoes(double[] s)
        {
            int inv = 0, anterior = 0;
            for (int i = 1; i < s.Length; i++)
            {
                double d = s[i] - s[i - 1];
                int sinal = d > 0 ? 1 : (d < 0 ? -1 : 0);
                if (sinal != 0 && anterior != 0 && sinal != anterior) inv++;
                if (sinal != 0) anterior = sinal;
            }
            return inv;
        }

        private static string F(double v)
        {
            return v.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
