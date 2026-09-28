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
    /// Teste manual rápido da lógica de vizinhança de HeatGrid.
    /// Cria uma grade 3x3 e confere a quantidade de vizinhos no
    /// canto, no centro e na borda antes de seguir pro motor de simulação.
    /// </summary>
    public static class TesteHeatGrid
    {
        public static void Executar()
        {
            Material materialCobre = MaterialCatalog.Todos.First(m => m.Nome == "Cobre");
            HeatGrid grade = new HeatGrid(3, materialCobre, 0.1, 300);

            int quantidadeCanto = grade.ObterVizinhos(0, 0).Quantidade;
            int quantidadeCentro = grade.ObterVizinhos(1, 1).Quantidade;
            int quantidadeBorda = grade.ObterVizinhos(0, 1).Quantidade;

            StringBuilder mensagem = new StringBuilder();
            mensagem.AppendLine("Teste manual: HeatGrid.ObterVizinhos (grade 3x3)");
            mensagem.AppendLine();
            AdicionarResultado(mensagem, "Canto (0,0)", quantidadeCanto, 2);
            AdicionarResultado(mensagem, "Centro (1,1)", quantidadeCentro, 4);
            AdicionarResultado(mensagem, "Borda (0,1)", quantidadeBorda, 3);

            MessageBox.Show(mensagem.ToString(), "Teste HeatGrid");
        }

        private static void AdicionarResultado(StringBuilder mensagem, string descricao, int obtido, int esperado)
        {
            string status = obtido == esperado ? "OK" : "FALHOU";
            mensagem.AppendLine(string.Format("[{0}] {1}: obtido {2}, esperado {3}", status, descricao, obtido, esperado));
        }
    }
}
#endif
