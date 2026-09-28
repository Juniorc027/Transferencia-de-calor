using System;
using TransferenciaCalor.Models;
using TransferenciaCalor.Estrutura;

namespace TransferenciaCalor.Simulation
{
    /// <summary>
    ///  Representa a grade N x N de corpos da simulação.
    /// Responsável por criar os corpos e identificar a vizinhança de cada um.
    /// </summary>
    public class HeatGrid
    {
        private Body[,] corpos;
        private int tamanho;

        public int Tamanho
        {
            get { return tamanho; }
        }

        /// <summary>
        /// Cria a grade com N x N corpos, todos com o mesmo material,
        /// lado e temperatura inicial informados.
        /// </summary>
        public HeatGrid(int n, Material materialInicial, double ladoCubo, double temperatura)
        {
            // Validações defensivas: a UI (numTamanho, numLadoCubo e a checagem de
            // material nulo em btnCriarGrade_Click) já impede esses valores de chegar
            // aqui, mas sem essa checagem uma chamada direta (ex.: em código de teste)
            // com lado <= 0 não daria erro NENHUM na hora — só geraria massa 0 e,
            // mais adiante, NaN/Infinity silencioso em AtualizarTemperatura (divisão
            // por massa*c = 0).
            if (materialInicial == null)
                throw new ArgumentNullException("materialInicial");
            if (ladoCubo <= 0)
                throw new ArgumentOutOfRangeException("ladoCubo", "O lado do cubo deve ser maior que zero.");
            if (n < 1)
                throw new ArgumentOutOfRangeException("n", "A grade precisa ter pelo menos 1 linha/coluna.");

            tamanho = n;
            corpos = new Body[n, n];

            for (int linha = 0; linha < n; linha++)
            {
                for (int coluna = 0; coluna < n; coluna++)
                {
                    corpos[linha, coluna] = new Body(linha, coluna, materialInicial, ladoCubo, temperatura);
                }
            }
        }

        /// <summary>
        /// Retorna o corpo na posição (linha, coluna) informada.
        /// </summary>
        public Body ObterCorpo(int linha, int coluna)
        {
            return corpos[linha, coluna];
        }

        /// <summary>
        /// Troca o material do corpo na posição (linha, coluna) informada.
        /// </summary>
        public void TrocarMaterial(int linha, int coluna, Material novoMaterial)
        {
            if (novoMaterial == null)
                throw new ArgumentNullException("novoMaterial");

            // A temperatura do corpo é mantida como está; como massa (ρ.V) e calor
            // específico mudam com o material novo, o calor sensível (m.c.T) muda de
            // valor no instante da troca — equivale a substituir fisicamente o objeto
            // por outro do novo material, na mesma temperatura. Ou seja, este evento
            // NÃO conserva energia (decisão de projeto: preserva T, não preserva Q).
            corpos[linha, coluna].Material = novoMaterial;
        }

        /// <summary>
        /// Retorna a lista encadeada com os corpos vizinhos (cima, baixo, esquerda, direita)
        /// do corpo na posição informada. Corpos nas bordas da grade terão menos vizinhos(2 ou 3).
        /// </summary>
        public ListaEncadeada<Body> ObterVizinhos(int linha, int coluna)
        {
            ListaEncadeada<Body> vizinhos = new ListaEncadeada<Body>();

            //Cima
            if(linha > 0)
            {
                vizinhos.Adicionar(corpos[linha - 1, coluna]);
            }

            //Baixo
            if(linha < tamanho - 1)
            {
                vizinhos.Adicionar(corpos[linha + 1, coluna]);
            }

            //Esquerda
            if(coluna > 0)
            {
                vizinhos.Adicionar(corpos[linha, coluna - 1]);
            }

            //Direita
            if(coluna < tamanho - 1)
            {
                vizinhos.Adicionar(corpos[linha, coluna + 1]);
            }

            return vizinhos;
        }
    }
}