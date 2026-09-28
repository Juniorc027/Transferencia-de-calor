namespace TransferenciaCalor.Models
{
    /// <summary>
    /// Um corpo (cubo) da grade de simulação, com posição, material, lado e temperatura.
    /// </summary>
    public class Body
    {
        // Atributos privados — encapsulamento
        private readonly int linha;
        private readonly int coluna;
        private Material material;
        private double ladoCubo;
        private double temperaturaAtual;
        private bool fixo;

        /// <summary>Linha do corpo na grade (0 = topo). Não muda depois de criado.</summary>
        public int Linha
        {
            get { return linha; }
        }

        /// <summary>Coluna do corpo na grade (0 = esquerda). Não muda depois de criado.</summary>
        public int Coluna
        {
            get { return coluna; }
        }

        /// <summary>Material do corpo; pode ser trocado durante a simulação.</summary>
        public Material Material
        {
            get { return material; }
            set { material = value; }
        }

        /// <summary>Comprimento do lado do cubo (m).</summary>
        public double LadoCubo
        {
            get { return ladoCubo; }
            set { ladoCubo = value; }
        }

        /// <summary>Temperatura atual do corpo (K).</summary>
        public double TemperaturaAtual
        {
            get { return temperaturaAtual; }
            set { temperaturaAtual = value; }
        }

        /// <summary>Quando true, a temperatura é fixada (condição de contorno) e não varia na simulação.</summary>
        public bool Fixo
        {
            get { return fixo; }
            set { fixo = value; }
        }

        //Construtor da classe Body
        public Body(int linha, int coluna, Material material, double ladoCubo, double temperaturaInicial)
        {
            this.linha = linha;
            this.coluna = coluna;
            this.material = material;
            this.ladoCubo = ladoCubo;
            this.temperaturaAtual = temperaturaInicial;
        }

        /// <summary>Massa do cubo: m = ρ · V (kg).</summary>
        public double CalcularMassa()
        {
            double volume = ladoCubo * ladoCubo * ladoCubo;
            return material.Densidade * volume;
        }

        /// <summary>Calor sensível armazenado no corpo: Q = m · c · T (J).</summary>
        public double CalcularCalorSensivel()
        {
            return CalcularMassa() * material.CalorEspecifico * temperaturaAtual;
        }
    }
}
