using System;
using System.Collections.Generic;
using TransferenciaCalor.Models;

namespace TransferenciaCalor.Simulation
{
    /// <summary>
    /// Motor responsável por calcular e aplicar a troca de calor entre
    /// os corpos da grade, avançando a simulação até o equilíbrio térmico.
    /// </summary>
    public class HeatSimulationEngine
    {
        // Atributos privados — encapsulamento
        private HeatGrid grade;
        private double intervaloTempo; // Δt de cada tick, em segundos
        private double limiarEquilibrio; // diferença RELATIVA de calor sensível considerada "equilíbrio" (1e-4 = 0,01%)

        /// <summary>Grade de corpos que este motor está simulando.</summary>
        public HeatGrid Grade
        {
            get { return grade; }
        }

        /// <summary>Δt (s) usado em cada Tick(). Exposto só leitura pra quem consome o
        /// motor (a UI) poder calcular o tempo simulado (Δt x nº de ticks) sem precisar
        /// guardar esse valor duplicado em outro lugar.</summary>
        public double IntervaloTempo
        {
            get { return intervaloTempo; }
        }

        /// <summary>
        /// Cria o motor para a grade informada.
        /// intervaloTempo: Δt de cada tick (s) — quanto "tempo simulado" passa a cada Tick().
        /// limiarEquilibrio: diferença relativa de calor sensível entre vizinhos abaixo da qual
        /// a grade é considerada em equilíbrio (1e-4 = 0,01%).
        /// </summary>
        public HeatSimulationEngine(HeatGrid grade, double intervaloTempo = 0.01, double limiarEquilibrio = 1e-4)
        {
            this.grade = grade;
            this.intervaloTempo = intervaloTempo;
            this.limiarEquilibrio = limiarEquilibrio;
        }

        // Calcula a taxa de calor (W) trocada entre dois corpos vizinhos "a" e "b".
        // (Método que já fizemos juntos — calcula o fluxo pela Lei de Fourier)
        //
        //q = k.A.[(Delta T)/(Delta x)]
        //
        //Onde q é a taxa de transferência de calor (em W ou J/s); k é a constante de condutividade térmica do material utilizado (adimensional);
        //Na prática, k é dado em W/(m.K) (não é adimensional); como o Delta x é desconsiderado neste cálculo,
        //o resultado de k.A.ΔT segue a simplificação pedida no enunciado (e fica, a rigor, em W.m em vez de W).
        //"A" é área da seção transversal por onde o calor vai fluir entre os corpos; Delta T é a diferença de temperatura entre as superfícies
        //que estão trocando calor; e Delta x é espessura ou comprimento do material usado.
        //Para este cálculo estou desconsiderando o Delta x, pois considero que os corpos estão se tocando perfeitamente.
        // Para o caso onde os corpos são de materiais diferentes, o k utilizado será o menor.
        // Conforme o enunciado, o sentido do fluxo é definido pelo CALOR SENSÍVEL (m.c.T):
        // o corpo com maior calor sensível cede calor para o de menor calor sensível.
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

            // Materiais diferentes: usa o menor k (o corpo que conduz menos limita a troca).
            double k = Math.Min(a.Material.K, b.Material.K);

            // Área de contato = face do cubo (lado²). Se os lados forem diferentes,
            // o contato fica limitado à face do cubo menor.
            double area = Math.Pow(Math.Min(a.LadoCubo, b.LadoCubo), 2);
            double deltaT = Math.Abs(corpoQuente.TemperaturaAtual - corpoFrio.TemperaturaAtual);

            // Lei de Fourier sem o Δx: q = k.A.ΔT (W)
            double q = k * area * deltaT;

            // Positivo: calor sai de "a" em direção a "b". Negativo: o contrário.
            return (corpoQuente == a) ? q : -q;
        }

        /// <summary>
        /// Executa uma iteração (tick) da simulação: calcula o fluxo de calor
        /// entre cada par de corpos vizinhos e aplica a transferência a todos
        /// simultaneamente, evitando que a ordem de cálculo influencie o resultado.
        /// </summary>
        public void Tick()
        {
            int n = grade.Tamanho;

            // Acumula a energia líquida (ganha ou perdida) de cada corpo neste tick,
            // sem alterar a temperatura ainda — só depois de calcular todos os pares.
            double[,] energiaLiquida = new double[n, n];

            for (int linha = 0; linha < n; linha++)
            {
                for (int coluna = 0; coluna < n; coluna++)
                {
                    Body atual = grade.ObterCorpo(linha, coluna);

                    // Verifica só o vizinho da direita e o de baixo — isso garante
                    // que cada par de corpos seja processado uma única vez
                    // (evita calcular "A→B" e depois "B→A" separadamente).
                    if (coluna < n - 1)
                        ProcessarPar(atual, grade.ObterCorpo(linha, coluna + 1), energiaLiquida);

                    if (linha < n - 1)
                        ProcessarPar(atual, grade.ObterCorpo(linha + 1, coluna), energiaLiquida);
                }
            }

            // Só agora, com todos os fluxos já calculados, atualiza as temperaturas.
            for (int linha = 0; linha < n; linha++)
            {
                for (int coluna = 0; coluna < n; coluna++)
                {
                    AtualizarTemperatura(grade.ObterCorpo(linha, coluna), energiaLiquida[linha, coluna]);
                }
            }
        }

        // Calcula o fluxo entre um par de corpos vizinhos e acumula a energia
        // trocada (positiva para quem recebe, negativa para quem cede) na matriz.
        private void ProcessarPar(Body a, Body b, double[,] energiaLiquida)
        {
            double q = CalcularFluxoCalor(a, b); // W (positivo: a → b)
            double energia = q * intervaloTempo; // J, trocados neste tick

            energiaLiquida[a.Linha, a.Coluna] -= energia; // "a" perdeu essa energia
            energiaLiquida[b.Linha, b.Coluna] += energia; // "b" ganhou essa energia
        }

        // Aplica a energia líquida acumulada no tick ao corpo, recalculando
        // sua temperatura a partir do novo calor sensível.
        private void AtualizarTemperatura(Body corpo, double energiaLiquida)
        {
            double calorSensivelAtual = corpo.CalcularCalorSensivel();
            double novoCalorSensivel = calorSensivelAtual + energiaLiquida;

            // T = Q / (m.c), isolando a temperatura da fórmula Q = m.c.T
            double massa = corpo.CalcularMassa();
            corpo.TemperaturaAtual = novoCalorSensivel / (massa * corpo.Material.CalorEspecifico);
        }

        /// <summary>
        /// Verifica se a grade já atingiu o equilíbrio térmico. Como pelo enunciado o
        /// fluxo vai do maior para o menor calor sensível, o equilíbrio é quando o
        /// calor sensível (m.c.T) de todos os pares de vizinhos se iguala — com
        /// materiais diferentes as temperaturas NÃO ficam iguais.
        /// </summary>
        public bool AtingiuEquilibrio()
        {
            int n = grade.Tamanho;

            // Energia total (em módulo) que cada corpo troca com TODOS os seus vizinhos em um tick.
            double[,] energiaTrocadaPorTick = new double[n, n];

            for (int linha = 0; linha < n; linha++)
            {
                for (int coluna = 0; coluna < n; coluna++)
                {
                    if (coluna < n - 1)
                        AcumularEnergiaTrocada(linha, coluna, linha, coluna + 1, energiaTrocadaPorTick);

                    if (linha < n - 1)
                        AcumularEnergiaTrocada(linha, coluna, linha + 1, coluna, energiaTrocadaPorTick);
                }
            }

            // Mesmo esquema do Tick(): só vizinho da direita e de baixo, cada par uma vez.
            for (int linha = 0; linha < n; linha++)
            {
                for (int coluna = 0; coluna < n; coluna++)
                {
                    if (coluna < n - 1 && !ParEmEquilibrio(linha, coluna, linha, coluna + 1, energiaTrocadaPorTick))
                        return false;

                    if (linha < n - 1 && !ParEmEquilibrio(linha, coluna, linha + 1, coluna, energiaTrocadaPorTick))
                        return false;
                }
            }

            return true;
        }

        private void AcumularEnergiaTrocada(int linhaA, int colunaA, int linhaB, int colunaB, double[,] energiaTrocadaPorTick)
        {
            Body a = grade.ObterCorpo(linhaA, colunaA);
            Body b = grade.ObterCorpo(linhaB, colunaB);
            double energia = Math.Abs(CalcularFluxoCalor(a, b)) * intervaloTempo;

            energiaTrocadaPorTick[linhaA, colunaA] += energia;
            energiaTrocadaPorTick[linhaB, colunaB] += energia;
        }

        // Um par está em equilíbrio quando a diferença de calor sensível é menor que:
        //  - o limiar relativo (ex: 0,01% do maior calor sensível), OU
        //  - a soma da energia que os dois corpos trocam com todos os seus vizinhos em um tick.
        // O segundo caso é necessário porque o fluxo q = k.A.ΔT não diminui conforme os
        // calores sensíveis se aproximam (com materiais diferentes o ΔT continua grande),
        // então perto do equilíbrio o sentido dos fluxos inverte a cada tick e o calor
        // sensível de cada corpo fica "oscilando" em torno do equilíbrio, com amplitude
        // da ordem do que ele troca por tick — a diferença nunca fica menor que isso.
        private bool ParEmEquilibrio(int linhaA, int colunaA, int linhaB, int colunaB, double[,] energiaTrocadaPorTick)
        {
            double calorSensivelA = grade.ObterCorpo(linhaA, colunaA).CalcularCalorSensivel();
            double calorSensivelB = grade.ObterCorpo(linhaB, colunaB).CalcularCalorSensivel();

            double diferenca = Math.Abs(calorSensivelA - calorSensivelB);
            double toleranciaRelativa = limiarEquilibrio * Math.Max(calorSensivelA, calorSensivelB);
            double oscilacaoPorTick = energiaTrocadaPorTick[linhaA, colunaA] + energiaTrocadaPorTick[linhaB, colunaB];

            return diferenca <= Math.Max(toleranciaRelativa, oscilacaoPorTick);
        }
    }
}