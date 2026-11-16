using System;
using System.Threading.Tasks;
using TransferenciaCalor.Models;

namespace TransferenciaCalor.Simulation
{
    /// <summary>
    /// Motor da N2: mesma física da N1 (HeatSimulationEngine), reorganizada para poder
    /// rodar em várias threads SEM condição de corrida e SEM lock.
    ///
    /// Por que a N1 não pode ser paralelizada como está: ProcessarPar escreve na energia
    /// de DOIS corpos por par (a perde, b ganha). Duas threads processando pares que
    /// compartilham um corpo escreveriam na mesma posição ao mesmo tempo.
    ///
    /// Desenho em duas fases (fork-join, cada uma termina antes da seguinte começar):
    ///  Fase A: o fluxo de cada ARESTA (par de vizinhos) é calculado uma única vez e
    ///          guardado em fluxoHorizontal / fluxoVertical. Cada thread só escreve nas
    ///          arestas das suas próprias linhas e só LÊ temperaturas.
    ///  Fase B: cada corpo SOMA a energia das suas até 4 arestas e atualiza a PRÓPRIA
    ///          temperatura. Cada thread só escreve nos corpos das suas linhas; as arestas
    ///          já estão prontas (a Fase A terminou), então só são lidas.
    ///
    /// Resultado idêntico ao da N1 bit a bit: as contas são as mesmas e a soma em cada
    /// corpo respeita a MESMA ORDEM em que a N1 acumulava (veja SomarEnergiaDoCorpo).
    /// </summary>
    public class HeatSimulationEngineParalelo : IMotorSimulacao
    {
        /// <summary>
        /// Tamanho de grade (N) a partir do qual o modo paralelo compensa. Abaixo disso o
        /// custo de acordar threads (fork-join duas vezes por tick) é maior que o cálculo.
        /// Valor 15: no benchmark (Ryzen 7 5700G, 8 núcleos/16 lógicos) o paralelo perdeu em N=10
        /// (0,76x a 0,83x da N2 sequencial) e passou a ganhar em N=15 (1,19x a 1,22x com 4 a 16
        /// threads). O ponto exato está entre 10 e 15; em máquinas com menos núcleos o ganho em
        /// N=15 some (com 2 threads deu 1,03x) — refaça o benchmark no PC do laboratório.
        /// </summary>
        public static int TamanhoMinimoPadrao
        {
            get { return tamanhoMinimoPadrao; }
        }

        // Constante privada (regra do projeto: nenhum campo público); também serve de
        // valor padrão do parâmetro do construtor.
        private const int tamanhoMinimoPadrao = 15;

        // Atributos privados — encapsulamento
        private readonly HeatGrid grade;
        private readonly double intervaloTempo;
        private readonly int tamanho;
        private readonly int grauParalelismo;
        private readonly int tamanhoMinimoParaParalelizar;
        private readonly bool forcarSequencial;

        // Os corpos NUNCA são substituídos na grade (TrocarMaterial só muda Body.Material),
        // então guardar as referências num vetor plano é seguro e evita a indireção do
        // ObterCorpo a cada acesso. Não altera nenhum valor calculado.
        private readonly Body[] corpos;

        // Energia (J) que cada ARESTA transfere neste tick, positiva no sentido "a -> b":
        //  fluxoHorizontal[l, c] = par (l,c) -> (l,c+1);  fluxoVertical[l, c] = par (l,c) -> (l+1,c).
        // Reaproveitados entre ticks (sem new por tick): na N1 cada Tick alocava um
        // double[n,n], o que em N grande ia para o LOH e disparava coletas gen1/gen2.
        private readonly double[,] energiaHorizontal;
        private readonly double[,] energiaVertical;

        // Faixas de linhas: faixa f cobre as linhas [inicioFaixa[f], inicioFaixa[f+1]).
        // Poucas faixas grandes (uma por thread) mantêm o overhead de agendamento baixo.
        private readonly int[] inicioFaixa;
        private readonly int quantidadeFaixas;

        private readonly ParallelOptions opcoesParalelas;
        // Delegates criados uma vez só (não por tick), para não alocar closures a cada Tick
        private readonly Action<int> executarFaixaFaseA;
        private readonly Action<int> executarFaixaFaseB;

        // AtingiuEquilibrio() é a lógica da N1, reaproveitada. Ver comentário no método.
        private readonly HeatSimulationEngine verificadorEquilibrio;

        /// <summary>Grade de corpos que este motor está simulando.</summary>
        public HeatGrid Grade
        {
            get { return grade; }
        }

        /// <summary>Δt (s) usado em cada Tick().</summary>
        public double IntervaloTempo
        {
            get { return intervaloTempo; }
        }

        /// <summary>Máximo de threads usadas na parte paralela.</summary>
        public int GrauParalelismo
        {
            get { return grauParalelismo; }
        }

        /// <summary>N mínimo para o Tick() usar threads (abaixo roda o mesmo algoritmo sem Parallel).</summary>
        public int TamanhoMinimoParaParalelizar
        {
            get { return tamanhoMinimoParaParalelizar; }
        }

        /// <summary>True se o Tick() está usando Parallel.For nesta grade.</summary>
        public bool ExecutandoEmParalelo
        {
            get { return !forcarSequencial && tamanho >= tamanhoMinimoParaParalelizar; }
        }

        /// <summary>
        /// grauMaximoParalelismo: 0 = Environment.ProcessorCount.
        /// tamanhoMinimoParaParalelizar: abaixo desse N o algoritmo roda sequencial.
        /// forcarSequencial: modo "N2 sequencial" (mesmo algoritmo de arestas, laços normais),
        /// usado no benchmark para separar o ganho da reorganização do ganho do paralelismo.
        /// </summary>
        public HeatSimulationEngineParalelo(HeatGrid grade, double intervaloTempo = 0.01, double limiarEquilibrio = 1e-4,
            int grauMaximoParalelismo = 0, int tamanhoMinimoParaParalelizar = tamanhoMinimoPadrao, bool forcarSequencial = false)
        {
            if (grade == null)
                throw new ArgumentNullException("grade");

            this.grade = grade;
            this.intervaloTempo = intervaloTempo;
            this.tamanho = grade.Tamanho;
            this.grauParalelismo = grauMaximoParalelismo <= 0 ? Environment.ProcessorCount : grauMaximoParalelismo;
            this.tamanhoMinimoParaParalelizar = tamanhoMinimoParaParalelizar;
            this.forcarSequencial = forcarSequencial;

            int n = tamanho;
            corpos = new Body[n * n];
            for (int l = 0; l < n; l++)
                for (int c = 0; c < n; c++)
                    corpos[l * n + c] = grade.ObterCorpo(l, c);

            // Para n = 1 não há arestas; os vetores ficam com dimensão 1 só para evitar tamanho 0
            energiaHorizontal = new double[n, Math.Max(1, n - 1)];
            energiaVertical = new double[Math.Max(1, n - 1), n];

            quantidadeFaixas = Math.Max(1, Math.Min(grauParalelismo, n));
            inicioFaixa = new int[quantidadeFaixas + 1];
            for (int f = 0; f <= quantidadeFaixas; f++)
                inicioFaixa[f] = (int)((long)n * f / quantidadeFaixas);

            opcoesParalelas = new ParallelOptions { MaxDegreeOfParallelism = grauParalelismo };
            executarFaixaFaseA = faixa => CalcularEnergiaDasArestas(inicioFaixa[faixa], inicioFaixa[faixa + 1]);
            executarFaixaFaseB = faixa => AtualizarCorpos(inicioFaixa[faixa], inicioFaixa[faixa + 1]);

            verificadorEquilibrio = new HeatSimulationEngine(grade, intervaloTempo, limiarEquilibrio);
        }

        /// <summary>
        /// Um tick. Continua sendo chamado pela thread da UI e só retorna quando as duas
        /// fases terminaram (fork-join): quem chama nunca vê a grade "pela metade".
        /// </summary>
        public void Tick()
        {
            if (ExecutandoEmParalelo)
            {
                Parallel.For(0, quantidadeFaixas, opcoesParalelas, executarFaixaFaseA);
                // Parallel.For só retorna depois de TODAS as faixas: a Fase B pode ler
                // as arestas de linhas vizinhas (outra faixa) com segurança.
                Parallel.For(0, quantidadeFaixas, opcoesParalelas, executarFaixaFaseB);
            }
            else
            {
                CalcularEnergiaDasArestas(0, tamanho);
                AtualizarCorpos(0, tamanho);
            }
        }

        // Fase A (linhas [de, ate)): energia trocada em cada aresta que NASCE nessas linhas
        // (par com o vizinho da direita e par com o de baixo, como a N1).
        private void CalcularEnergiaDasArestas(int de, int ate)
        {
            int n = tamanho;
            for (int linha = de; linha < ate; linha++)
            {
                for (int coluna = 0; coluna < n; coluna++)
                {
                    Body atual = corpos[linha * n + coluna];

                    if (coluna < n - 1)
                        energiaHorizontal[linha, coluna] = CalcularEnergiaDoPar(atual, corpos[linha * n + coluna + 1]);

                    if (linha < n - 1)
                        energiaVertical[linha, coluna] = CalcularEnergiaDoPar(atual, corpos[(linha + 1) * n + coluna]);
                }
            }
        }

        // Fase B (linhas [de, ate)): soma as energias das arestas de cada corpo e atualiza só ele.
        private void AtualizarCorpos(int de, int ate)
        {
            int n = tamanho;
            for (int linha = de; linha < ate; linha++)
            {
                for (int coluna = 0; coluna < n; coluna++)
                    AtualizarTemperatura(corpos[linha * n + coluna], SomarEnergiaDoCorpo(linha, coluna));
            }
        }

        // Energia líquida do corpo (linha, coluna), somando NESTA ORDEM: cima, esquerda,
        // direita, baixo, partindo de 0.0. É a ordem em que a N1 acumulava, porque ela varre
        // a grade linha a linha e, ao visitar cada corpo, processa primeiro o par da direita e
        // depois o de baixo:
        //   1) a aresta de cima foi processada ao visitar o corpo de cima (linha anterior);
        //   2) a da esquerda, ao visitar o corpo da esquerda (mesma linha, coluna anterior);
        //   3) a da direita e 4) a de baixo, ao visitar o próprio corpo.
        // Soma de ponto flutuante não é associativa; mudar essa ordem mudaria os últimos bits.
        // Sinais: o corpo que é "b" do par RECEBE (+); o que é "a" CEDE (-).
        private double SomarEnergiaDoCorpo(int linha, int coluna)
        {
            int n = tamanho;
            double energiaLiquida = 0.0;

            if (linha > 0)
                energiaLiquida += energiaVertical[linha - 1, coluna];   // veio de cima (aqui somos "b")
            if (coluna > 0)
                energiaLiquida += energiaHorizontal[linha, coluna - 1]; // veio da esquerda ("b")
            if (coluna < n - 1)
                energiaLiquida -= energiaHorizontal[linha, coluna];     // sai para a direita (aqui somos "a")
            if (linha < n - 1)
                energiaLiquida -= energiaVertical[linha, coluna];       // sai para baixo ("a")

            return energiaLiquida;
        }

        // Equivale ao ProcessarPar da N1, mas só CALCULA a energia (q x Δt) em vez de
        // acumulá-la nos dois corpos; quem acumula é a Fase B.
        private double CalcularEnergiaDoPar(Body a, Body b)
        {
            double q = CalcularFluxoCalor(a, b); // W (positivo: a -> b)
            return q * intervaloTempo;           // J trocados neste tick
        }

        // CÓPIA FIEL de HeatSimulationEngine.CalcularFluxoCalor (que é private na N1 e a N1
        // não deve ser alterada). Mesmas expressões, inclusive Math.Pow e a escolha do menor k,
        // para o resultado ser idêntico.
        private double CalcularFluxoCalor(Body a, Body b)
        {
            double calorSensivelA = a.CalcularCalorSensivel();
            double calorSensivelB = b.CalcularCalorSensivel();

            // Já em equilíbrio: não há fluxo
            if (calorSensivelA == calorSensivelB)
                return 0.0;

            // "Quente" = quem cede calor (maior calor sensível); "Frio" = quem recebe.
            Body corpoQuente = calorSensivelA > calorSensivelB ? a : b;
            Body corpoFrio = calorSensivelA > calorSensivelB ? b : a;

            // Materiais diferentes: usa o menor k
            double k = Math.Min(a.Material.K, b.Material.K);

            // Área de contato = face do cubo menor
            double area = Math.Pow(Math.Min(a.LadoCubo, b.LadoCubo), 2);
            double deltaT = Math.Abs(corpoQuente.TemperaturaAtual - corpoFrio.TemperaturaAtual);

            // Lei de Fourier sem o Δx: q = k.A.ΔT (W)
            double q = k * area * deltaT;

            return (corpoQuente == a) ? q : -q;
        }

        // Cópia fiel de HeatSimulationEngine.AtualizarTemperatura: T = Q / (m.c)
        private void AtualizarTemperatura(Body corpo, double energiaLiquida)
        {
            double calorSensivelAtual = corpo.CalcularCalorSensivel();
            double novoCalorSensivel = calorSensivelAtual + energiaLiquida;

            double massa = corpo.CalcularMassa();
            corpo.TemperaturaAtual = novoCalorSensivel / (massa * corpo.Material.CalorEspecifico);
        }

        /// <summary>
        /// Critério de equilíbrio idêntico ao da N1 (delegado ao motor sequencial da N1).
        /// NÃO foi paralelizado de propósito: medido na N1, ele custa 0,5% do tempo por tick
        /// a 10x e ~5% a 1x (a UI o chama UMA vez por frame, não por tick). Pelo Amdahl,
        /// paralelizar algo que pesa ~0,5% rende no máximo ~0,5% do total, e o custo de
        /// acordar threads a cada frame consumiria esse ganho.
        /// </summary>
        public bool AtingiuEquilibrio()
        {
            return verificadorEquilibrio.AtingiuEquilibrio();
        }
    }
}
