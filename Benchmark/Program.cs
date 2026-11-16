using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TransferenciaCalor.Persistencia;
using TransferenciaCalor.Simulation;

namespace TransferenciaCalor.Benchmark
{
    /// <summary>Uma linha do CSV: uma métrica, para um N, com suas estatísticas.</summary>
    internal sealed class Registro
    {
        private readonly int? n;
        private readonly string metrica;
        private readonly string unidade;
        private readonly double mediana, media, desvio, minimo;
        private readonly int repeticoes;
        private readonly long iteracoes;

        public Registro(int? n, string metrica, string unidade, double mediana, double media, double desvio, double minimo, int repeticoes, long iteracoes)
        {
            this.n = n; this.metrica = metrica; this.unidade = unidade;
            this.mediana = mediana; this.media = media; this.desvio = desvio; this.minimo = minimo;
            this.repeticoes = repeticoes; this.iteracoes = iteracoes;
        }

        public int? N { get { return n; } }
        public string Metrica { get { return metrica; } }
        public string Unidade { get { return unidade; } }
        public double Mediana { get { return mediana; } }
        public double Media { get { return media; } }
        public double Desvio { get { return desvio; } }
        public double Minimo { get { return minimo; } }
        public int Repeticoes { get { return repeticoes; } }
        public long Iteracoes { get { return iteracoes; } }
    }

    internal sealed class Resultados
    {
        private readonly List<Registro> registros = new List<Registro>();

        public IList<Registro> Todos { get { return registros; } }

        public void AdicionarTempo(int? n, string metrica, Amostra a)
        {
            registros.Add(new Registro(n, metrica, "ns/op", a.Mediana, a.Media, a.Desvio, a.Minimo, a.Repeticoes, a.Iteracoes));
        }

        public void AdicionarValor(int? n, string metrica, string unidade, double valor, long iteracoes)
        {
            registros.Add(new Registro(n, metrica, unidade, valor, valor, 0, valor, 1, iteracoes));
        }

        public void AdicionarAlocacao(int? n, string metrica, Alocacao a)
        {
            AdicionarValor(n, metrica + "_bytes_por_op", "bytes/op", a.BytesPorOperacao, a.Operacoes);
            double por1000 = 1000.0 / Math.Max(1, a.Operacoes);
            AdicionarValor(n, metrica + "_gc_gen0", "coletas/1000 ops", a.Gen0 * por1000, a.Operacoes);
            AdicionarValor(n, metrica + "_gc_gen1", "coletas/1000 ops", a.Gen1 * por1000, a.Operacoes);
            AdicionarValor(n, metrica + "_gc_gen2", "coletas/1000 ops", a.Gen2 * por1000, a.Operacoes);
        }

        /// <summary>Valor (mediana) de uma métrica para um N; NaN se não foi medida.</summary>
        public double Valor(int? n, string metrica)
        {
            Registro r = registros.FirstOrDefault(x => x.N == n && x.Metrica == metrica);
            return r == null ? double.NaN : r.Mediana;
        }

        public Registro Obter(int? n, string metrica)
        {
            return registros.FirstOrDefault(x => x.N == n && x.Metrica == metrica);
        }

        public List<int> TamanhosMedidos()
        {
            return registros.Where(x => x.N.HasValue).Select(x => x.N.Value).Distinct().OrderBy(x => x).ToList();
        }
    }

    internal static class Program
    {
        private static readonly int[] TamanhosPadrao = { 5, 10, 15, 20, 30, 60, 100, 200, 400 };

        private static int Main(string[] args)
        {
            // Opções: --ns 5,10,20  --reps 7  --saida pasta
            int[] tamanhos = TamanhosPadrao;
            int repeticoes = 7; // >= 5 exigido
            string pastaSaida = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--ns") tamanhos = args[i + 1].Split(',').Select(int.Parse).ToArray();
                if (args[i] == "--reps") repeticoes = Math.Max(5, int.Parse(args[i + 1]));
                if (args[i] == "--saida") pastaSaida = args[i + 1];
            }
            pastaSaida = pastaSaida ?? PastaPadraoSaida();

            // Modos: --verificar (equivalência N1 x N2), --base (suíte original da N1),
            // padrão = benchmark de motores N1 x N2-seq x N2-par.
            if (args.Contains("--verificar"))
                return Verificacao.Executar();
            if (args.Contains("--verificar-gravacao"))
                return VerificacaoGravacao.Executar(ValorDe(args, "--conexao"));
            if (args.Contains("--diagnostico-equilibrio"))
                return DiagnosticoEquilibrio.Executar(args, pastaSaida);
            if (args.Contains("--banco"))
                return ExecutarBanco(args, repeticoes, pastaSaida);

            Ambiente ambiente = Ambiente.Coletar();
            foreach (KeyValuePair<string, string> item in ambiente.Itens)
                Console.WriteLine("  {0,-18} {1}", item.Key, item.Value);
            if (Debugger.IsAttached)
                Console.WriteLine("\n*** ATENÇÃO: debugger anexado, os números NÃO são válidos. Rode com Ctrl+F5 / pelo explorador. ***\n");

            if (!args.Contains("--base"))
                return BenchMotores.Executar(args.Contains("--ns") ? tamanhos : null, repeticoes, pastaSaida, ambiente);

            // Liga o monitoramento de alocação do AppDomain (usado só nas passadas de memória)
            AppDomain.MonitoringIsEnabled = true;

            // Prioridade alta reduz a interferência de outros processos; registrado no relatório
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High; } catch (Exception) { }

            Resultados res = new Resultados();
            Stopwatch total = Stopwatch.StartNew();

            foreach (int n in tamanhos)
            {
                Console.WriteLine("\n=== N = {0} ({1} corpos) ===  [{2:F0}s decorridos]", n, n * n, total.Elapsed.TotalSeconds);
                MedirTamanho(n, repeticoes, res);
            }

            Console.WriteLine("\n=== Montagem do INSERT em lote (independente de N) ===");
            MedirSql(repeticoes, res);

            Relatorio.Gravar(pastaSaida, ambiente, res);
            Console.WriteLine("\nConcluído em {0:F0} s. Arquivos gravados em: {1}", total.Elapsed.TotalSeconds, pastaSaida);
            return 0;
        }

        private static void MedirTamanho(int n, int reps, Resultados res)
        {
            // 1) Tick() sozinho. Uma grade nova a cada repetição (fora do tempo), para
            //    todas as repetições partirem do mesmo estado inicial misto.
            HeatSimulationEngine motor = null;
            Action prepararTick = () => { motor = Cenarios.CriarMotor(Cenarios.CriarMisto(n)); };
            Action tick = () => motor.Tick();
            Amostra aTick = Medidor.Medir(prepararTick, tick, reps);
            res.AdicionarTempo(n, "tick", aTick);
            Console.WriteLine("  Tick()                      {0,12:F2} us", aTick.Mediana / 1000);

            // Alocações/GC e checagem de sanidade (sem NaN/Infinity depois de muitos ticks)
            res.AdicionarAlocacao(n, "tick", Medidor.MedirAlocacao(prepararTick, tick, aTick.Iteracoes));
            res.AdicionarValor(n, "tick_temperaturas_finitas", "1=sim,0=nao", Cenarios.TudoFinito(motor.Grade) ? 1 : 0, aTick.Iteracoes);

            // 2a) AtingiuEquilibrio() no estado misto (sai cedo no 2º laço, mas o 1º laço é completo)
            HeatSimulationEngine motorEq = null;
            bool descarte = false;
            Action prepararEq = () => { motorEq = Cenarios.CriarMotor(Cenarios.CriarMisto(n)); };
            Action eqMisto = () => { descarte = motorEq.AtingiuEquilibrio(); };
            Amostra aEqMisto = Medidor.Medir(prepararEq, eqMisto, reps);
            res.AdicionarTempo(n, "equilibrio_misto", aEqMisto);
            res.AdicionarAlocacao(n, "equilibrio_misto", Medidor.MedirAlocacao(prepararEq, eqMisto, aEqMisto.Iteracoes));
            Console.WriteLine("  AtingiuEquilibrio (misto)   {0,12:F2} us", aEqMisto.Mediana / 1000);

            // 2b) AtingiuEquilibrio() varrendo tudo (pior caso)
            HeatSimulationEngine motorEq2 = null;
            Action prepararEq2 = () => { motorEq2 = Cenarios.CriarMotor(Cenarios.CriarQuaseEquilibrio(n)); };
            Action eqCompleto = () => { descarte = motorEq2.AtingiuEquilibrio(); };
            Amostra aEqCompleto = Medidor.Medir(prepararEq2, eqCompleto, reps);
            res.AdicionarTempo(n, "equilibrio_completo", aEqCompleto);
            res.AdicionarAlocacao(n, "equilibrio_completo", Medidor.MedirAlocacao(prepararEq2, eqCompleto, aEqCompleto.Iteracoes));
            res.AdicionarValor(n, "equilibrio_completo_retorno_true", "1=sim,0=nao", descarte ? 1 : 0, 1);
            Console.WriteLine("  AtingiuEquilibrio (completo){0,12:F2} us  (retornou {1})", aEqCompleto.Mediana / 1000, descarte);

            // 3) Snapshot dos corpos: copiando para arrays NOVOS (o que a thread de gravação receberá)
            HeatGrid grade = null;
            SnapshotCorpos snap = null;
            Action prepararSnap = () => { grade = Cenarios.CriarMisto(n); snap = SnapshotCorpos.Capturar(grade); };
            Action snapNovo = () => { snap = SnapshotCorpos.Capturar(grade); };
            Amostra aSnap = Medidor.Medir(prepararSnap, snapNovo, reps);
            res.AdicionarTempo(n, "snapshot_novo", aSnap);
            res.AdicionarAlocacao(n, "snapshot_novo", Medidor.MedirAlocacao(prepararSnap, snapNovo, aSnap.Iteracoes));
            Console.WriteLine("  Snapshot (arrays novos)     {0,12:F2} us", aSnap.Mediana / 1000);

            //    ...e reaproveitando os arrays (referência do custo só da cópia)
            Action snapReuso = () => snap.Recapturar(grade);
            Amostra aSnapReuso = Medidor.Medir(prepararSnap, snapReuso, reps);
            res.AdicionarTempo(n, "snapshot_reuso", aSnapReuso);
            Console.WriteLine("  Snapshot (arrays reusados)  {0,12:F2} us", aSnapReuso.Mediana / 1000);

            // Referência: custo só de alocar o double[n,n] que Tick()/AtingiuEquilibrio() criam
            double[,] arr = null;
            Action alocar = () => { arr = new double[n, n]; };
            Amostra aAloc = Medidor.Medir(null, alocar, reps);
            res.AdicionarTempo(n, "alocar_matriz", aAloc);
            res.AdicionarAlocacao(n, "alocar_matriz", Medidor.MedirAlocacao(null, alocar, aAloc.Iteracoes));

            // Referência de agendamento: Parallel.For com corpo VAZIO. Mede só o custo de
            // dividir/despachar o trabalho nas threads. Duas variantes de nº de iterações:
            // N (uma por linha) e N*N (uma por célula).
            Action<int> vazio = i => { };
            Action pfLinhas = () => Parallel.For(0, n, vazio);
            Action pfCelulas = () => Parallel.For(0, n * n, vazio);
            Amostra aPf1 = Medidor.Medir(null, pfLinhas, reps);
            Amostra aPf2 = Medidor.Medir(null, pfCelulas, reps);
            res.AdicionarTempo(n, "parallelfor_vazio_N", aPf1);
            res.AdicionarTempo(n, "parallelfor_vazio_N2", aPf2);
            Console.WriteLine("  Parallel.For vazio (N it.)  {0,12:F2} us ; (N*N it.) {1:F2} us", aPf1.Mediana / 1000, aPf2.Mediana / 1000);

            GC.KeepAlive(arr);
            GC.KeepAlive(descarte);
        }

        private static void MedirSql(int reps, Resultados res)
        {
            // Snapshot de uma grade 100x100 (10.000 corpos), suficiente para 5.000 linhas
            SnapshotCorpos snap = SnapshotCorpos.Capturar(Cenarios.CriarMisto(100));
            string texto = null;
            foreach (int linhas in new[] { 1000, 5000 })
            {
                int l = linhas;
                Action montar = () => { texto = ConstrutorInsert.Montar(snap, 1, l); };
                Amostra a = Medidor.Medir(null, montar, reps);
                res.AdicionarTempo(null, "sql_" + l + "_linhas", a);
                res.AdicionarAlocacao(null, "sql_" + l + "_linhas", Medidor.MedirAlocacao(null, montar, a.Iteracoes));
                res.AdicionarValor(null, "sql_" + l + "_linhas_tamanho_texto", "caracteres", texto.Length, 1);
                Console.WriteLine("  INSERT {0} linhas: {1,10:F1} us  ({2:F1} ns/linha, {3} chars)", l, a.Mediana / 1000, a.Mediana / l, texto.Length);
            }
            GC.KeepAlive(texto);
        }

        private static string ValorDe(string[] args, string opcao)
        {
            int i = Array.IndexOf(args, opcao);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        // Fase 5: --banco --conexao ConexaoMySql.config [--ns 20,60] [--ticks 2000] [--gravs 100,10,1] [--reps-banco 3] [--manter-dados]
        //         [--repositorio memoria] (só para testar o programa sem servidor: os números NÃO valem como medição de banco)
        private static int ExecutarBanco(string[] args, int repeticoes, string pasta)
        {
            Ambiente amb = Ambiente.Coletar();
            int[] ns = (ValorDe(args, "--ns") ?? "20,60").Split(',').Select(int.Parse).ToArray();
            int[] gravs = (ValorDe(args, "--gravs") ?? "100,10,1").Split(',').Select(int.Parse).ToArray();
            int ticks = int.Parse(ValorDe(args, "--ticks") ?? "2000");
            int repsBanco = int.Parse(ValorDe(args, "--reps-banco") ?? "3");
            bool memoria = ValorDe(args, "--repositorio") == "memoria";

            IRepositorioSimulacao repo;
            string conexao = null;
            if (memoria)
                repo = new RepositorioEmMemoria();
            else
            {
                string arquivo = ValorDe(args, "--conexao") ?? "ConexaoMySql.config";
                conexao = ConfiguracaoBanco.LerDeArquivo(arquivo);
                repo = new RepositorioMySql(conexao);
            }
            return BenchBanco.Executar(repo, conexao, ns, ticks, gravs, repeticoes, repsBanco, args.Contains("--manter-dados"), amb, pasta,
                memoria ? "REPOSITÓRIO EM MEMÓRIA (teste do programa, NÃO é banco real)" : ValorDe(args, "--servidor"));
        }

        // Se o exe estiver em Benchmark\bin\Release, grava em Benchmark\; senão, na pasta atual
        private static string PastaPadraoSaida()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string candidata = Path.GetFullPath(Path.Combine(baseDir, "..", ".."));
            return File.Exists(Path.Combine(candidata, "Benchmark.csproj")) ? candidata : Directory.GetCurrentDirectory();
        }
    }
}
