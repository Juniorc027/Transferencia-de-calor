using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using TransferenciaCalor.Data;
using TransferenciaCalor.Models;
using TransferenciaCalor.Persistencia;
using TransferenciaCalor.Simulation;

namespace TransferenciaCalor.Benchmark
{
    /// <summary>
    /// Modo --verificar-gravacao: testa a lógica de gravação e do Refazer.
    /// Roda com o repositório em memória (sem servidor) e, se passar --conexao arquivo,
    /// repete o roundtrip no MySQL de verdade. Saída PASSOU/FALHOU, sem MessageBox.
    /// </summary>
    internal static class VerificacaoGravacao
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static int falhas;

        public static int Executar(string arquivoConexao)
        {
            falhas = 0;
            Console.WriteLine("== Lógica de gravação (repositório em memória) ==");
            TesteSplitSchema();
            TesteIdaEVoltaDouble();
            TesteRoundTrip("memória", new RepositorioEmMemoria());
            TesteFalhaDoBanco();
            TesteFilaLimitada();

            if (arquivoConexao != null)
            {
                Console.WriteLine("\n== Roundtrip no MySQL de verdade ({0}) ==", arquivoConexao);
                try
                {
                    IRepositorioSimulacao mysql = new RepositorioMySql(ConfiguracaoBanco.LerDeArquivo(arquivoConexao));
                    TesteRoundTrip("MySQL", mysql);
                }
                catch (Exception ex)
                {
                    Resultado("roundtrip MySQL", false, "exceção: " + ex.GetBaseException().Message);
                }
            }

            Console.WriteLine(falhas == 0 ? "\nRESULTADO GERAL: PASSOU" : "\nRESULTADO GERAL: FALHOU (" + falhas + " testes)");
            return falhas == 0 ? 0 : 1;
        }

        private static void Resultado(string nome, bool passou, string detalhe)
        {
            if (!passou) falhas++;
            Console.WriteLine("{0,-7} {1} {2}", passou ? "PASSOU" : "FALHOU", nome, detalhe);
        }

        // O schema é dividido em comandos: 1 CREATE DATABASE + 1 USE + 7 tabelas + 3 views = 12
        private static void TesteSplitSchema()
        {
            string schema;
            using (var fluxo = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("schema.sql"))
            using (var leitor = new System.IO.StreamReader(fluxo))
                schema = leitor.ReadToEnd();
            IList<string> comandos = RepositorioMySql.DividirComandos(schema);
            bool ok = comandos.Count == 12 && comandos.All(c => !c.Contains("--") && c.Length > 5);
            Resultado("divisão do schema.sql em comandos", ok, "(" + comandos.Count + " comandos, esperado 12)");
        }

        // "G17" + InvariantCulture precisa devolver EXATAMENTE o mesmo double (o Refazer compara com tolerância 0)
        private static void TesteIdaEVoltaDouble()
        {
            Random rng = new Random(7);
            long erros = 0;
            for (int i = 0; i < 2000000; i++)
            {
                double v = (rng.NextDouble() - 0.5) * Math.Pow(10, rng.Next(-12, 12));
                if (double.Parse(v.ToString("G17", Inv), Inv) != v) erros++;
            }
            // Temperaturas reais vindas de uma simulação
            HeatGrid g = Cenarios.CriarMisto(20);
            HeatSimulationEngine m = Cenarios.CriarMotor(g);
            for (int t = 0; t < 500; t++) m.Tick();
            for (int l = 0; l < 20; l++)
                for (int c = 0; c < 20; c++)
                {
                    double v = g.ObterCorpo(l, c).TemperaturaAtual;
                    if (double.Parse(v.ToString("G17", Inv), Inv) != v) erros++;
                }
            Resultado("ida e volta de double (G17, InvariantCulture)", erros == 0, "(2.000.400 valores, divergências: " + erros + ")");
        }

        // Grava uma execução "ao vivo" (mesmo fluxo da UI), com eventos e uma pausa em tick NÃO múltiplo
        // de 100, e refaz com cada motor. A conferência deve dar diferença 0.
        private static void TesteRoundTrip(string rotulo, IRepositorioSimulacao repo)
        {
            repo.GarantirEsquema();
            HeatGrid grade = Cenarios.CriarMisto(15);
            HeatSimulationEngine motor = Cenarios.CriarMotor(grade);
            long origemId;
            long ticksFinais = 1234;

            using (GravadorSimulacao grav = new GravadorSimulacao(repo))
            {
                SessaoGravacao sessao = new SessaoGravacao(grav, 100);
                sessao.Abrir(new DadosExecucao(null, "BENCH-verificacao", null, ModoExecucao.Sequencial, 1, 0.01, 1e-4, 100), grade);
                long tick = 0;
                Action<long> avancar = n =>
                {
                    while (n > 0)
                    {
                        long lote = Math.Min(n, sessao.TicksAteProximaAmostra(tick));
                        for (long i = 0; i < lote; i++) motor.Tick();
                        tick += lote; n -= lote;
                        sessao.AoConcluirTick(grade, tick);
                    }
                };

                avancar(250);
                // evento em tick que é múltiplo de 100? (250 não é) -> depois do tick 250
                grade.ObterCorpo(3, 4).TemperaturaAtual = 900.123456789012;
                sessao.RegistrarEvento(new EventoSimulacao(tick, TipoEvento.AplicouTemperatura, 3, 4, null, 900.123456789012));
                avancar(527);                                                  // tick 777
                sessao.Finalizar(grade, tick, false, 1, false);                // pausa em tick não múltiplo
                Material ouro = MaterialCatalog.Todos.First(m => m.Nome == "Ouro");
                grade.TrocarMaterial(7, 7, ouro);                              // dois eventos no MESMO tick, ordem importa
                sessao.RegistrarEvento(new EventoSimulacao(tick, TipoEvento.TrocouMaterial, 7, 7, ouro, null));
                grade.ObterCorpo(7, 7).TemperaturaAtual = 450.5;
                sessao.RegistrarEvento(new EventoSimulacao(tick, TipoEvento.AplicouTemperatura, 7, 7, null, 450.5));
                avancar(ticksFinais - tick);                                   // retoma e roda até 1234
                sessao.Finalizar(grade, tick, false, 1, true);
                bool esvaziou = grav.EsperarEsvaziar(60000);
                origemId = grav.ExecucaoAtualId;
                Resultado("[" + rotulo + "] gravação ao vivo terminou sem falha", esvaziou && !grav.Falhou, grav.Falhou ? grav.UltimoErro : "(" + grav.LinhasGravadas + " linhas)");
            }

            // Ticks gravados esperados: 0, 100..1200 (13), + pausa 777 + final 1234 = 15 ticks distintos
            if (repo is RepositorioEmMemoria)
            {
                List<long> ticks = ((RepositorioEmMemoria)repo).TicksGravados(origemId);
                bool ok = ticks.Count == 15 && ticks.Contains(0) && ticks.Contains(777) && ticks.Contains(1234) && ticks.Contains(1200);
                Resultado("[" + rotulo + "] ticks gravados = 0, múltiplos de 100, pausa (777) e final (1234)", ok, "(" + ticks.Count + " ticks)");
            }

            Func<string, Func<HeatGrid, IMotorSimulacao>, bool> refazer = (nome, fabrica) =>
            {
                ExecucaoCarregada exec = repo.CarregarExecucao(origemId);
                CenarioCarregado cenario = repo.CarregarCenario(exec.CenarioId);
                IList<EventoSimulacao> eventos = repo.CarregarEventos(exec.ExecucaoId);
                HeatGrid g = cenario.MontarGrade();
                IMotorSimulacao m = fabrica(g);
                using (GravadorSimulacao gr = new GravadorSimulacao(repo))
                {
                    SessaoGravacao s = new SessaoGravacao(gr, exec.GravACadaN);
                    s.Abrir(new DadosExecucao(cenario.CenarioId, cenario.Nome, exec.ExecucaoId, ModoExecucao.Sequencial, 1, exec.DeltaT, exec.LimiarEquilibrio, exec.GravACadaN), g);
                    ExecutorSimulacao.Executar(g, m, exec.Ticks, s, eventos);
                    gr.EsperarEsvaziar(60000);
                    if (gr.Falhou) { Resultado("[" + rotulo + "] refazer " + nome, false, gr.UltimoErro); return false; }
                    ResultadoConferencia conf = repo.Conferir(gr.ExecucaoAtualId);
                    // 14 ticks comparáveis (o 777 da pausa só existe na original) x 225 corpos
                    bool ok = exec.Ticks == ticksFinais && conf.DiferencaMaximaK == 0.0 && conf.PontosComparados == 14 * 225;
                    Resultado("[" + rotulo + "] refazer com " + nome, ok, "diferença máxima = " + conf.DiferencaMaximaK.ToString("R", Inv) + " K sobre " + conf.PontosComparados + " pontos (esperado 3150)");
                    return ok;
                }
            };

            refazer("N1", g => Cenarios.CriarMotor(g));
            refazer("N2 sequencial", g => Cenarios.CriarMotorN2(g, 1, 1, true));
            refazer("N2 paralelo (3 threads)", g => Cenarios.CriarMotorN2(g, 3, 1, false));
            refazer("N2 paralelo (" + Environment.ProcessorCount + " threads)", g => Cenarios.CriarMotorN2(g, Environment.ProcessorCount, 1, false));
        }

        // Banco fora do ar: a simulação não pode travar, a execução fica incompleta e o erro é exposto
        private static void TesteFalhaDoBanco()
        {
            RepositorioEmMemoria repo = new RepositorioEmMemoria();
            repo.ConfigurarFalha(true);
            HeatGrid g = Cenarios.CriarMisto(5);
            IMotorSimulacao m = Cenarios.CriarMotor(g);
            using (GravadorSimulacao grav = new GravadorSimulacao(repo, 4))
            {
                SessaoGravacao s = new SessaoGravacao(grav, 1);
                Stopwatch cron = Stopwatch.StartNew();
                s.Abrir(new DadosExecucao(null, "BENCH-falha", null, ModoExecucao.Sequencial, 1, 0.01, 1e-4, 1), g);
                ExecutorSimulacao.Executar(g, m, 2000, s, null); // gravN=1: tentaria 2001 snapshots com fila de 4
                bool terminouRapido = cron.ElapsedMilliseconds < 5000;
                bool esvaziou = grav.EsperarEsvaziar(5000);
                long exec = grav.ExecucaoAtualId;
                Resultado("banco fora do ar: simulação não trava, erro exposto, execução incompleta",
                    terminouRapido && esvaziou && grav.Falhou && grav.UltimoErro.Length > 0,
                    "(" + cron.ElapsedMilliseconds + " ms; descartados: " + grav.ItensDescartados + "; erro: " + grav.UltimoErro + ")");
            }
        }

        // Banco lento + fila pequena: o produtor BLOQUEIA (não perde dado) e o bloqueio é medido
        private static void TesteFilaLimitada()
        {
            RepositorioEmMemoria repo = new RepositorioEmMemoria();
            repo.ConfigurarLatencia(30);
            HeatGrid g = Cenarios.CriarMisto(10);
            IMotorSimulacao m = Cenarios.CriarMotor(g);
            using (GravadorSimulacao grav = new GravadorSimulacao(repo, 4, 500))
            {
                SessaoGravacao s = new SessaoGravacao(grav, 1);
                s.Abrir(new DadosExecucao(null, "BENCH-fila", null, ModoExecucao.Sequencial, 1, 0.01, 1e-4, 1), g);
                ExecutorSimulacao.Executar(g, m, 100, s, null);
                bool esvaziou = grav.EsperarEsvaziar(60000);
                bool tudo = repo.TotalLeituras == 101 * 100 && grav.LinhasGravadas == 101 * 100;
                Resultado("fila cheia bloqueia o produtor sem perder dados", esvaziou && tudo && grav.MsBloqueadoProdutor > 0 && grav.MaximoDaFila <= 5,
                    "(linhas gravadas " + repo.TotalLeituras + "/10100; produtor bloqueado " + grav.MsBloqueadoProdutor.ToString("F0", Inv) + " ms; fila máx " + grav.MaximoDaFila + ")");
            }
        }
    }
}
