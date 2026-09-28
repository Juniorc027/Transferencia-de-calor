#if DEBUG
// Teste manual (roda só via chamada explícita, ver Program.cs) — não faz parte
// da entrega em Release: compilado só quando o símbolo DEBUG está definido.
using System;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using TransferenciaCalor.Data;
using TransferenciaCalor.Models;
using TransferenciaCalor.Simulation;

namespace TransferenciaCalor.Testes
{
    /// <summary>
    /// Teste manual: grade 2x2 em "xadrez" com Alumínio a 400 K e Aço a 300 K.
    /// O aço tem ρ.c maior, então mesmo mais frio ele tem MAIS calor sensível
    /// (m.c.T) que o alumínio. Pela regra do enunciado, o calor deve sair do
    /// corpo com maior calor sensível (aço) para o de menor (alumínio), até
    /// que o calor sensível de todos os corpos se iguale. Confere também se a
    /// energia total se conserva e se o motor detecta o equilíbrio (AtingiuEquilibrio).
    /// </summary>
    public static class TesteMateriaisDiferentes
    {
        private const int MaximoTicks = 100000;
        private const double LadoCubo = 0.001;
        private const double TemperaturaAluminio = 400;
        private const double TemperaturaAco = 300;
        private const double ToleranciaCalorRelativa = 1e-3; // 0,1% de diferença entre os calores sensíveis
        private const double ToleranciaEnergiaRelativa = 1e-9;

        public static void Executar()
        {
            Material aluminio = MaterialCatalog.Todos.First(m => m.Nome == "Alumínio");
            Material aco = MaterialCatalog.Todos.First(m => m.Nome == "Aço");

            HeatGrid grade = new HeatGrid(2, aluminio, LadoCubo, TemperaturaAluminio);
            PrepararCorpoAco(grade.ObterCorpo(0, 0), aco);
            PrepararCorpoAco(grade.ObterCorpo(1, 1), aco);

            Body corpoAluminio = grade.ObterCorpo(0, 1);
            Body corpoAco = grade.ObterCorpo(0, 0);

            double energiaInicial = CalcularEnergiaTotal(grade);
            double calorSensivelEsperado = energiaInicial / 4; // no equilíbrio, os 4 corpos têm o mesmo m.c.T

            bool acoComecaComMaisCalor = corpoAco.CalcularCalorSensivel() > corpoAluminio.CalcularCalorSensivel();

            HeatSimulationEngine motor = new HeatSimulationEngine(grade);

            double calorAnteriorAco = corpoAco.CalcularCalorSensivel();
            double calorAnteriorAluminio = corpoAluminio.CalcularCalorSensivel();

            bool valoresValidos = true;
            bool acoSempreCede = true;
            bool aluminioSempreRecebe = true;
            bool atingiuEquilibrio = false;
            int ticksExecutados = 0;

            while (ticksExecutados < MaximoTicks)
            {
                motor.Tick();
                ticksExecutados++;

                double qAco = corpoAco.CalcularCalorSensivel();
                double qAluminio = corpoAluminio.CalcularCalorSensivel();

                if (double.IsNaN(qAco) || double.IsInfinity(qAco) ||
                    double.IsNaN(qAluminio) || double.IsInfinity(qAluminio))
                {
                    valoresValidos = false;
                    break;
                }

                // Pequena tolerância para ruído numérico de ponto flutuante.
                if (qAco > calorAnteriorAco * (1 + 1e-12))
                    acoSempreCede = false;

                if (qAluminio < calorAnteriorAluminio * (1 - 1e-12))
                    aluminioSempreRecebe = false;

                calorAnteriorAco = qAco;
                calorAnteriorAluminio = qAluminio;

                if (motor.AtingiuEquilibrio())
                {
                    atingiuEquilibrio = true;
                    break;
                }
            }

            double energiaFinal = CalcularEnergiaTotal(grade);
            bool calorSensivelIgualado = atingiuEquilibrio
                && Math.Abs(corpoAco.CalcularCalorSensivel() - corpoAluminio.CalcularCalorSensivel())
                   / Math.Max(corpoAco.CalcularCalorSensivel(), corpoAluminio.CalcularCalorSensivel()) < ToleranciaCalorRelativa;
            bool energiaConservada = Math.Abs(energiaFinal - energiaInicial) / energiaInicial < ToleranciaEnergiaRelativa;

            // Temperatura que cada material deve ter quando m.c.T for igual em todos: T = Q / (m.c)
            double temperaturaEsperadaAco = calorSensivelEsperado / (corpoAco.CalcularMassa() * aco.CalorEspecifico);
            double temperaturaEsperadaAluminio = calorSensivelEsperado / (corpoAluminio.CalcularMassa() * aluminio.CalorEspecifico);

            StringBuilder mensagem = new StringBuilder();
            mensagem.AppendLine(string.Format("Teste manual: materiais diferentes (Alumínio x Aço, grade 2x2, {0} ticks)", ticksExecutados));
            mensagem.AppendLine();
            mensagem.AppendLine(string.Format("Aço (0,0): {0:0.0} -> {1:0.00} K (esperado ≈ {2:0.00} K)", TemperaturaAco, corpoAco.TemperaturaAtual, temperaturaEsperadaAco));
            mensagem.AppendLine(string.Format("Alumínio (0,1): {0:0.0} -> {1:0.00} K (esperado ≈ {2:0.00} K)", TemperaturaAluminio, corpoAluminio.TemperaturaAtual, temperaturaEsperadaAluminio));
            mensagem.AppendLine();
            AdicionarResultado(mensagem, "Aço começa com mais calor sensível que o alumínio", acoComecaComMaisCalor);
            AdicionarResultado(mensagem, "Sem NaN/Infinito", valoresValidos);
            AdicionarResultado(mensagem, "Aço (maior calor sensível) só cede calor", acoSempreCede);
            AdicionarResultado(mensagem, "Alumínio (menor calor sensível) só recebe calor", aluminioSempreRecebe);
            AdicionarResultado(mensagem, "Energia total conservada", energiaConservada);
            AdicionarResultado(mensagem, string.Format("Motor detectou o equilíbrio em até {0} ticks", MaximoTicks), atingiuEquilibrio);
            AdicionarResultado(mensagem, string.Format("Calor sensível igualado (±{0:0.0%})", ToleranciaCalorRelativa), calorSensivelIgualado);

            MessageBox.Show(mensagem.ToString(), "Teste Materiais Diferentes");
        }

        private static void PrepararCorpoAco(Body corpo, Material aco)
        {
            corpo.Material = aco;
            corpo.TemperaturaAtual = TemperaturaAco;
        }

        // Σ m.c.T de todos os corpos (J)
        private static double CalcularEnergiaTotal(HeatGrid grade)
        {
            double total = 0;
            for (int linha = 0; linha < grade.Tamanho; linha++)
                for (int coluna = 0; coluna < grade.Tamanho; coluna++)
                    total += grade.ObterCorpo(linha, coluna).CalcularCalorSensivel();
            return total;
        }

        private static void AdicionarResultado(StringBuilder mensagem, string descricao, bool passou)
        {
            string status = passou ? "OK" : "FALHOU";
            mensagem.AppendLine(string.Format("[{0}] {1}", status, descricao));
        }
    }
}
#endif
