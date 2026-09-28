namespace TransferenciaCalor.Estrutura
{
    /// <summary>
    /// Nó de uma lista encadeada genérica: guarda um valor e a referência
    /// para o próximo nó da lista.
    /// </summary>
    public class No<T>
    {
        // Atributos privados — encapsulamento
        private T valor;
        private No<T> proximo;

        /// <summary>Dado armazenado neste nó.</summary>
        public T Valor
        {
            get { return valor; }
            set { valor = value; }
        }

        /// <summary>Próximo nó da lista (null quando este é o último).</summary>
        public No<T> Proximo
        {
            get { return proximo; }
            set { proximo = value; }
        }

        // Cria um nó "solto", ainda sem ligação com outro nó.
        public No(T valor)
        {
            this.valor = valor;
            this.proximo = null;
        }
    }
}
