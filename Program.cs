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
        /// Ponto de entrada principal para o aplicativo.
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new MainForm());
        }
    }
}
