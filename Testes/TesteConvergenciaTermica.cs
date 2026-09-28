#if DEBUG
// Teste manual (roda só via chamada explícita, ver Program.cs) — não faz parte
// da entrega em Release: compilado só quando o símbolo DEBUG está definido.
using System.Linq;
using System.Text;
using System.Windows.Forms;
using TransferenciaCalor.Data;
using TransferenciaCalor.Models;
using TransferenciaCalor.Simulation;

namespace TransferenciaCalor.Testes
{
    /// <summary>
    /// Teste manual: grade pequena, mesmo material em todos os corpos, mas
    /// com um canto com temperatura inicial bem diferente do resto. Roda
    /// vários Tick() e confere se as temperaturas convergem suavemente para
    /// um valor intermediário, sem oscilar nem disparar para infinito.
    /// </summary>
    public static class TesteConvergenciaTermica
    {
        private const int Ticks = 300;
        private const double TemperaturaBase = 300;
        private const double TemperaturaCantoQuente = 1000;

        public static void Executar()
        {
            Material materialCobre = MaterialCatalog.Todos.First(m => m.Nome == "Cobre");

            HeatGrid grade = new HeatGrid(3, materialCobre, 0.1, TemperaturaBase);
            Body cantoQuente = grade.ObterCorpo(0, 0);
            Body cantoOposto = grade.ObterCorpo(2, 2);
            cantoQuente.TemperaturaAtual = TemperaturaCantoQuente;

            HeatSimulationEngine motor = new HeatSimulationEngine(grade);

            double temperaturaAnteriorQuente = cantoQuente.TemperaturaAtual;
            double temperaturaAnteriorOposto = cantoOposto.TemperaturaAtual;

            bool valoresValidos = true;
            bool quenteSempreDecrescente = true;
            bool opostoSempreCrescente = true;

            for (int i = 1; i <= Ticks; i++)
            {
                motor.Tick();

                double tQuente = cantoQuente.TemperaturaAtual;
                double tOposto = cantoOposto.TemperaturaAtual;

                if (double.IsNaN(tQuente) || double.IsInfinity(tQuente) ||
                    double.IsNaN(tOposto) || double.IsInfinity(tOposto))
                {
                    valoresValidos = false;
                    break;
                }

                // Pequena tolerância para ruído numérico de ponto flutuante.
                if (tQuente > temperaturaAnteriorQuente + 1e-9)
                    quenteSempreDecrescente = false;

                if (tOposto < temperaturaAnteriorOposto - 1e-9)
                    opostoSempreCrescente = false;

                temperaturaAnteriorQuente = tQuente;
                temperaturaAnteriorOposto = tOposto;
            }

            bool convergiuParaIntermediario = valoresValidos
                && cantoQuente.TemperaturaAtual > TemperaturaBase
                && cantoQuente.TemperaturaAtual < TemperaturaCantoQuente;

            StringBuilder mensagem = new StringBuilder();
            mensagem.AppendLine(string.Format("Teste manual: convergência térmica (grade 3x3, {0} ticks)", Ticks));
            mensagem.AppendLine();
            mensagem.AppendLine(string.Format("Canto quente (0,0): {0:0.0} -> {1:0.0000}", TemperaturaCantoQuente, cantoQuente.TemperaturaAtual));
            mensagem.AppendLine(string.Format("Canto oposto (2,2): {0:0.0} -> {1:0.0000}", TemperaturaBase, cantoOposto.TemperaturaAtual));
            mensagem.AppendLine();
            AdicionarResultado(mensagem, "Sem NaN/Infinito", valoresValidos);
            AdicionarResultado(mensagem, "Canto quente só esfria (sem oscilar)", quenteSempreDecrescente);
            AdicionarResultado(mensagem, "Canto oposto só esquenta (sem oscilar)", opostoSempreCrescente);
            AdicionarResultado(mensagem, "Convergiu p/ valor intermediário (entre 300 e 1000)", convergiuParaIntermediario);

            MessageBox.Show(mensagem.ToString(), "Teste Convergência Térmica");
        }

        private static void AdicionarResultado(StringBuilder mensagem, string descricao, bool passou)
        {
            string status = passou ? "OK" : "FALHOU";
            mensagem.AppendLine(string.Format("[{0}] {1}", status, descricao));
        }
    }
}
#endif
