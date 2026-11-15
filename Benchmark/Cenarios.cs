using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TransferenciaCalor.Data;
using TransferenciaCalor.Models;
using TransferenciaCalor.Simulation;

namespace TransferenciaCalor.Benchmark
{
    /// <summary>Monta as grades de teste, sempre com semente fixa (resultado reprodutível).</summary>
    internal static class Cenarios
    {
        private const int Semente = 20260928;
        private const double LadoCubo = 0.1;   // mesmo padrão de numLadoCubo na UI
        private const double Delta_t = 0.01;   // MainForm.IntervaloTempoSimulacao
        private const double Limiar = 1e-4;    // limiar passado pela UI ao motor

        /// <summary>
        /// Grade "misto": materiais sorteados do catálogo e gradiente de temperatura
        /// (300 K na esquerda até 800 K na direita, mais ruído). Evita o caso trivial
        /// de grade uniforme, em que o fluxo é sempre zero e o cálculo é pulado.
        /// </summary>
        public static HeatGrid CriarMisto(int n)
        {
            Random rng = new Random(Semente);
            HeatGrid grade = new HeatGrid(n, MaterialCatalog.Todos[0], LadoCubo, 300.0);
            for (int l = 0; l < n; l++)
            {
                for (int c = 0; c < n; c++)
                {
                    Body corpo = grade.ObterCorpo(l, c);
                    grade.TrocarMaterial(l, c, MaterialCatalog.Todos[rng.Next(MaterialCatalog.Todos.Count)]);
                    double fracao = n > 1 ? (double)c / (n - 1) : 0.0;
                    corpo.TemperaturaAtual = 300.0 + 500.0 * fracao + rng.NextDouble() * 20.0;
                }
            }
            return grade;
        }

        /// <summary>
        /// Grade quase em equilíbrio: mesmo material e temperaturas que diferem só ~1e-6 K.
        /// Faz AtingiuEquilibrio() varrer a grade INTEIRA (sem a saída antecipada do
        /// segundo laço) e com fluxos não nulos: é o pior caso desse método.
        /// </summary>
        public static HeatGrid CriarQuaseEquilibrio(int n)
        {
            Random rng = new Random(Semente);
            HeatGrid grade = new HeatGrid(n, MaterialCatalog.Todos[0], LadoCubo, 500.0);
            for (int l = 0; l < n; l++)
                for (int c = 0; c < n; c++)
                    grade.ObterCorpo(l, c).TemperaturaAtual = 500.0 + rng.NextDouble() * 1e-6;
            return grade;
        }

        /// <summary>Um único material (Cobre), mas com gradiente de temperatura: há fluxo, sem mistura de k.</summary>
        public static HeatGrid CriarMaterialUnico(int n)
        {
            Random rng = new Random(Semente + 1);
            HeatGrid grade = new HeatGrid(n, MaterialCatalog.Todos[0], LadoCubo, 300.0);
            for (int l = 0; l < n; l++)
                for (int c = 0; c < n; c++)
                    grade.ObterCorpo(l, c).TemperaturaAtual = 300.0 + 500.0 * rng.NextDouble();
            return grade;
        }

        /// <summary>Materiais mistos e lados de cubo DIFERENTES (exercita o Math.Min do lado na área de contato).</summary>
        public static HeatGrid CriarLadosDiferentes(int n)
        {
            Random rng = new Random(Semente + 2);
            HeatGrid grade = CriarMisto(n);
            for (int l = 0; l < n; l++)
                for (int c = 0; c < n; c++)
                    grade.ObterCorpo(l, c).LadoCubo = 0.05 + 0.15 * rng.NextDouble();
            return grade;
        }

        public static HeatSimulationEngine CriarMotor(HeatGrid grade)
        {
            return new HeatSimulationEngine(grade, Delta_t, Limiar);
        }

        /// <summary>Motor N2. tamanhoMinimo = 1 força o caminho paralelo mesmo em N pequeno (para medir/testar).</summary>
        public static HeatSimulationEngineParalelo CriarMotorN2(HeatGrid grade, int threads, int tamanhoMinimo, bool sequencial)
        {
            return new HeatSimulationEngineParalelo(grade, Delta_t, Limiar, threads, tamanhoMinimo, sequencial);
        }

        /// <summary>True se todas as temperaturas da grade são números finitos (sem NaN/Infinity).</summary>
        public static bool TudoFinito(HeatGrid grade)
        {
            for (int l = 0; l < grade.Tamanho; l++)
                for (int c = 0; c < grade.Tamanho; c++)
                {
                    double t = grade.ObterCorpo(l, c).TemperaturaAtual;
                    if (double.IsNaN(t) || double.IsInfinity(t))
                        return false;
                }
            return true;
        }
    }

    /// <summary>
    /// Cópia imutável do estado dos corpos (temperatura, material e calor sensível),
    /// que é o que a gravação no banco receberá a cada 100 ticks. Precisa ser CÓPIA
    /// porque a simulação continua alterando a grade enquanto a gravação acontece.
    /// </summary>
    internal sealed class SnapshotCorpos
    {
        private readonly double[] temperaturas;
        private readonly Material[] materiais;
        private readonly double[] calores;

        private SnapshotCorpos(int quantidade)
        {
            temperaturas = new double[quantidade];
            materiais = new Material[quantidade];
            calores = new double[quantidade];
        }

        public int Quantidade { get { return temperaturas.Length; } }
        public double Temperatura(int i) { return temperaturas[i]; }
        public Material MaterialDe(int i) { return materiais[i]; }
        public double CalorSensivel(int i) { return calores[i]; }

        /// <summary>Cria um snapshot novo (3 arrays alocados).</summary>
        public static SnapshotCorpos Capturar(HeatGrid grade)
        {
            SnapshotCorpos s = new SnapshotCorpos(grade.Tamanho * grade.Tamanho);
            s.Preencher(grade);
            return s;
        }

        /// <summary>Reaproveita os arrays de um snapshot anterior (zero alocação).</summary>
        public void Recapturar(HeatGrid grade)
        {
            Preencher(grade);
        }

        private void Preencher(HeatGrid grade)
        {
            int n = grade.Tamanho;
            int i = 0;
            for (int l = 0; l < n; l++)
            {
                for (int c = 0; c < n; c++)
                {
                    Body b = grade.ObterCorpo(l, c);
                    temperaturas[i] = b.TemperaturaAtual;
                    materiais[i] = b.Material;
                    calores[i] = b.CalcularCalorSensivel();
                    i++;
                }
            }
        }
    }

    /// <summary>
    /// Monta o TEXTO de um INSERT em lote. Só CPU: nada é enviado a servidor algum.
    /// double sempre com InvariantCulture, senão em pt-BR sairia "300,5" e quebraria o SQL.
    /// </summary>
    internal static class ConstrutorInsert
    {
        public static string Montar(SnapshotCorpos snapshot, long tick, int linhas)
        {
            // Capacidade inicial estimada (~90 caracteres por linha) para o StringBuilder não crescer
            StringBuilder sb = new StringBuilder(linhas * 90 + 128);
            sb.Append("INSERT INTO leitura (tick, indice, material, temperatura, calor_sensivel) VALUES ");

            int total = snapshot.Quantidade;
            for (int i = 0; i < linhas; i++)
            {
                int origem = i % total; // se o snapshot tiver menos corpos que linhas, recicla
                if (i > 0) sb.Append(',');
                sb.Append('(')
                  .Append(tick.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(i.ToString(CultureInfo.InvariantCulture)).Append(",'")
                  .Append(snapshot.MaterialDe(origem).Nome.Replace("'", "''")).Append("',")
                  .Append(snapshot.Temperatura(origem).ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(snapshot.CalorSensivel(origem).ToString(CultureInfo.InvariantCulture)).Append(')');
            }
            return sb.ToString();
        }
    }
}
