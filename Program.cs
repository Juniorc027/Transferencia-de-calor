using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TransferenciaCalor.UI;

namespace TransferenciaCalor
{
    internal static class Program
    {
        /// <summary>
        /// Ponto de entrada principal para o aplicativo.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Os testes manuais (TesteHeatGrid, TesteConvergenciaTermica, em Testes/)
            // não rodam mais automaticamente aqui pra não travar a abertura do app
            // com MessageBox. Chamem-nos manualmente (ex: no Immediate Window ou
            // temporariamente aqui) quando quiserem validar de novo.

            Application.Run(new MainForm());
        }
    }
}
