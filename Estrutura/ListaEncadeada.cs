using System.Collections;
using System.Collections.Generic;

namespace TransferenciaCalor.Estrutura
{
    /// <summary>
    /// Lista encadeada simples e genérica, implementada manualmente.
    /// Pode ser reutilizada para qualquer tipo de dado (Body, Material, etc.).
    /// </summary>
    public class ListaEncadeada<T> : IEnumerable<T>
    {
        // Atributos privados — encapsulamento
        private No<T> primeiro;
        private No<T> ultimo;
        private int quantidade;

        /// <summary>Quantidade de elementos na lista.</summary>
        public int Quantidade
        {
            get { return quantidade; }
        }

        /// <summary>True quando a lista não tem nenhum elemento.</summary>
        public bool EstaVazia
        {
            get { return primeiro == null; }
        }

        // Cria uma lista vazia.
        public ListaEncadeada()
        {
            primeiro = null;
            ultimo = null;
            quantidade = 0;
        }

        /// <summary>
        /// Adiciona um elemento no final da lista.
        /// </summary>
        public void Adicionar(T valor)
        {
            No<T> novoNo = new No<T>(valor);

            if (EstaVazia)
            {
                // Lista vazia: o novo nó é o primeiro e o último
                primeiro = novoNo;
                ultimo = novoNo;
            }
            else
            {
                // Encadeia o novo nó depois do último existente
                ultimo.Proximo = novoNo;
                ultimo = novoNo;
            }

            quantidade++;
        }

        /// <summary>
        /// Remove a primeira ocorrência do valor informado.
        /// Retorna true se removeu, false se não encontrou.
        /// </summary>
        public bool Remover(T valor)
        {
            No<T> atual = primeiro;
            No<T> anterior = null;

            while (atual != null)
            {
                if (Equals(atual.Valor, valor))
                {
                    if (anterior == null)
                    {
                        // Removendo o primeiro nó da lista
                        primeiro = atual.Proximo;
                    }
                    else
                    {
                        // "Pula" o nó atual, ligando o anterior direto ao próximo
                        anterior.Proximo = atual.Proximo;
                    }

                    // Se o nó removido era o último, atualiza a referência
                    if (atual == ultimo)
                    {
                        ultimo = anterior;
                    }

                    quantidade--;
                    return true;
                }

                anterior = atual;
                atual = atual.Proximo;
            }

            return false; // Valor não encontrado
        }

        /// <summary>
        /// Verifica se um valor existe na lista.
        /// </summary>
        public bool Contem(T valor)
        {
            No<T> atual = primeiro;

            while (atual != null)
            {
                if (Equals(atual.Valor, valor))
                    return true;

                atual = atual.Proximo;
            }

            return false;
        }

        /// <summary>
        /// Permite usar foreach diretamente na ListaEncadeada.
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            No<T> atual = primeiro;

            while (atual != null)
            {
                yield return atual.Valor;
                atual = atual.Proximo;
            }
        }

        // Versão não genérica exigida pela interface IEnumerable; só repassa para a de cima.
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}