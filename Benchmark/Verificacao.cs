using System;
using System.Collections.Generic;
using TransferenciaCalor.Models;
using TransferenciaCalor.Simulation;

namespace TransferenciaCalor.Benchmark
{
    /// <summary>
    /// Teste de equivalência (modo --verificar): roda a N1, a N2 sequencial e a N2 paralela
    /// sobre grades iniciais idênticas e compara TODAS as temperaturas a cada tick.
    /// O esperado é diferença exatamente 0, porque as contas e a ordem das somas são as mesmas.
    /// Não usa MessageBox: só imprime PASSOU/FALHOU e devolve o código de saída.
    /// </summary>
    internal static class Verificacao
    {
        private const int TicksPorCenario = 5000;

        private sealed class CenarioTeste
        {
            private readonly string nome;
            private readonly Func<int, HeatGrid> fabrica;
            public CenarioTeste(string nome, Func<int, HeatGrid> fabrica) { this.nome = nome; this.fabrica = fabrica; }
            public string Nome { get { return nome; } }
            public HeatGrid Criar(int n) { return fabrica(n); }
        }

        public static int Executar()
        {
            CenarioTeste[] cenarios =
            {
                new CenarioTeste("material unico", Cenarios.CriarMaterialUnico),
                new CenarioTeste("materiais mistos (lados iguais)", Cenarios.CriarMisto),
                new CenarioTeste("materiais mistos + lados diferentes", Cenarios.CriarLadosDiferentes),
            };
            int[] tamanhos = { 5, 15, 20, 60 };
            // Graus de paralelismo: o da máquina e um que NÃO divide N de forma exata (faixas desiguais)
            int[] grausParalelos = { Environment.ProcessorCount, 3 };

            double erroGlobal = 0.0;
            int falhas = 0;
            Console.WriteLine("Equivalência N1 x N2-seq x N2-paralelo ({0} ticks por caso, comparando todas as temperaturas a cada tick)\n", TicksPorCenario);

            foreach (CenarioTeste cen in cenarios)
            {
                foreach (int n in tamanhos)
                {
                    HeatSimulationEngine n1 = Cenarios.CriarMotor(cen.Criar(n));
                    HeatSimulationEngineParalelo seq = Cenarios.CriarMotorN2(cen.Criar(n), 1, 1, true);
                    List<HeatSimulationEngineParalelo> paralelos = new List<HeatSimulationEngineParalelo>();
                    foreach (int g in grausParalelos)
                        paralelos.Add(Cenarios.CriarMotorN2(cen.Criar(n), g, 1, false)); // tamanhoMinimo=1: força o Parallel

                    double erroMax = 0.0;
                    long primeiroTickDivergente = -1;
                    bool equilibrioDiverge = false;

                    for (int tick = 1; tick <= TicksPorCenario; tick++)
                    {
                        n1.Tick();
                        seq.Tick();
                        foreach (HeatSimulationEngineParalelo p in paralelos) p.Tick();

                        double e = DiferencaMaxima(n1.Grade, seq.Grade);
                        foreach (HeatSimulationEngineParalelo p in paralelos)
                            e = Math.Max(e, DiferencaMaxima(n1.Grade, p.Grade));
                        if (e > 0 && primeiroTickDivergente < 0) primeiroTickDivergente = tick;
                        erroMax = Math.Max(erroMax, e);

                        // O critério de equilíbrio também deve dar a mesma resposta nos três motores
                        if (tick % 100 == 0)
                        {
                            bool ref1 = n1.AtingiuEquilibrio();
                            if (seq.AtingiuEquilibrio() != ref1) equilibrioDiverge = true;
                            foreach (HeatSimulationEngineParalelo p in paralelos)
                                if (p.AtingiuEquilibrio() != ref1) equilibrioDiverge = true;
                        }
                    }

                    bool passou = erroMax == 0.0 && !equilibrioDiverge && Cenarios.TudoFinito(n1.Grade);
                    if (!passou) falhas++;
                    erroGlobal = Math.Max(erroGlobal, erroMax);
                    Console.WriteLine("{0,-7} {1,-38} N={2,-3} erro_max_abs={3:E3}{4}{5}", passou ? "PASSOU" : "FALHOU", cen.Nome, n, erroMax,
                        primeiroTickDivergente >= 0 ? "  1a divergencia no tick " + primeiroTickDivergente : "",
                        equilibrioDiverge ? "  (AtingiuEquilibrio divergiu)" : "");
                }
            }

            Console.WriteLine("\nErro maximo absoluto geral (temperatura, K): {0:E3}", erroGlobal);
            Console.WriteLine(falhas == 0 ? "RESULTADO GERAL: PASSOU" : "RESULTADO GERAL: FALHOU (" + falhas + " casos)");
            return falhas == 0 ? 0 : 1;
        }

        private static double DiferencaMaxima(HeatGrid a, HeatGrid b)
        {
            double maximo = 0.0;
            int n = a.Tamanho;
            for (int l = 0; l < n; l++)
                for (int c = 0; c < n; c++)
                {
                    double d = Math.Abs(a.ObterCorpo(l, c).TemperaturaAtual - b.ObterCorpo(l, c).TemperaturaAtual);
                    // NaN em qualquer lado conta como divergência (NaN não compara com nada)
                    if (double.IsNaN(d)) return double.PositiveInfinity;
                    if (d > maximo) maximo = d;
                }
            return maximo;
        }
    }
}
