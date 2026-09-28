using System;

namespace TransferenciaCalor.Models
{
    /// <summary>
    /// Material de um corpo, com as propriedades térmicas usadas na simulação
    /// (todas em unidades do SI).
    /// </summary>
    public class Material
    {
        // Atributos privados — encapsulamento
        private string nome;
        private double k;
        private double densidade;
        private double calorEspecifico;

        /// <summary>Nome exibido na interface (ex: "Cobre").</summary>
        public string Nome
        {
            get { return nome; }
            set { nome = value; }
        }

        /// <summary>Condutividade térmica k (W/(m·K)) — usada na Lei de Fourier q = k.A.ΔT.</summary>
        public double K
        {
            get { return k; }
            set { k = value; }
        }

        /// <summary>Densidade ρ (kg/m³) — usada para calcular a massa do corpo (m = ρ.V).</summary>
        public double Densidade
        {
            get { return densidade; }
            set { densidade = value; }
        }

        /// <summary>Calor específico c (J/(kg·K)) — usado no calor sensível Q = m.c.T.</summary>
        public double CalorEspecifico
        {
            get { return calorEspecifico; }
            set { calorEspecifico = value; }
        }

        //Construtor da classe Material
        public Material(string nome, double k, double densidade, double calorEspecifico)
        {
            this.nome = nome;
            this.k = k;
            this.densidade = densidade;
            this.calorEspecifico = calorEspecifico;
        }

        // Faz o ComboBox da interface exibir o nome do material em vez do nome da classe.
        public override string ToString()
        {
            return nome;
        }
    }
}
