using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Runtime;
using Microsoft.Win32;

namespace TransferenciaCalor.Benchmark
{
    /// <summary>
    /// Registra o ambiente da medição. Cada item é lido com try/catch: se algo
    /// não puder ser consultado, vira "indisponível" em vez de derrubar o benchmark.
    /// </summary>
    internal sealed class Ambiente
    {
        private readonly List<KeyValuePair<string, string>> itens = new List<KeyValuePair<string, string>>();

        public IList<KeyValuePair<string, string>> Itens { get { return itens; } }
        public string NomeMaquina { get { return Environment.MachineName; } }
        public int NucleosLogicos { get { return Environment.ProcessorCount; } }
        public int NucleosFisicos { get; private set; }

        public static Ambiente Coletar()
        {
            Ambiente a = new Ambiente();
            a.Adicionar("maquina", Environment.MachineName);
            a.Adicionar("data_hora", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            string cpu = "indisponível";
            int fisicos = 0;
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name, NumberOfCores FROM Win32_Processor"))
                    foreach (ManagementBaseObject o in s.Get())
                    {
                        cpu = Convert.ToString(o["Name"]).Trim();
                        fisicos += Convert.ToInt32(o["NumberOfCores"]);
                    }
            }
            catch (Exception) { }
            a.NucleosFisicos = fisicos;
            a.Adicionar("cpu", cpu);
            a.Adicionar("nucleos_fisicos", fisicos > 0 ? fisicos.ToString() : "indisponível");
            a.Adicionar("nucleos_logicos", Environment.ProcessorCount.ToString());

            string ram = "indisponível";
            string so = Environment.OSVersion.ToString();
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                    foreach (ManagementBaseObject o in s.Get())
                        ram = (Convert.ToInt64(o["TotalPhysicalMemory"]) / (1024.0 * 1024 * 1024)).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " GB";
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Caption, Version FROM Win32_OperatingSystem"))
                    foreach (ManagementBaseObject o in s.Get())
                        so = Convert.ToString(o["Caption"]).Trim() + " (" + Convert.ToString(o["Version"]) + ")";
            }
            catch (Exception) { }
            a.Adicionar("ram", ram);
            a.Adicionar("windows", so);

            a.Adicionar("dotnet_framework", LerVersaoFramework());
            a.Adicionar("clr", Environment.Version.ToString());
#if DEBUG
            a.Adicionar("configuracao", "DEBUG (INVÁLIDO para medir)");
#else
            a.Adicionar("configuracao", "Release");
#endif
            a.Adicionar("plataforma", Environment.Is64BitProcess ? "x64 (processo 64 bits; Prefer 32-bit desligado)" : "x86 (processo 32 bits)");
            a.Adicionar("debugger_anexado", Debugger.IsAttached ? "SIM (INVÁLIDO para medir)" : "não");
            a.Adicionar("gc", GCSettings.IsServerGC ? "servidor" : "estação de trabalho (workstation)");
            a.Adicionar("plano_energia", LerPlanoEnergia());
            return a;
        }

        private void Adicionar(string chave, string valor)
        {
            itens.Add(new KeyValuePair<string, string>(chave, valor));
        }

        // O número da versão do Framework fica no registro (valor "Release"), não em Environment.Version
        private static string LerVersaoFramework()
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
                {
                    object r = k == null ? null : k.GetValue("Release");
                    if (r == null) return "indisponível";
                    int release = (int)r;
                    string nome = release >= 533320 ? "4.8.1 ou superior" : release >= 528040 ? "4.8" : release >= 461808 ? "4.7.2" : "anterior a 4.7.2";
                    return nome + " (Release=" + release + ")";
                }
            }
            catch (Exception) { return "indisponível"; }
        }

        // Plano "Economia de energia" reduz a frequência da CPU e distorce a medição
        private static string LerPlanoEnergia()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("powercfg", "/getactivescheme")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (Process p = Process.Start(psi))
                {
                    string saida = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit();
                    return saida.Length > 0 ? saida : "indisponível";
                }
            }
            catch (Exception) { return "indisponível"; }
        }
    }
}
