using System.Collections.Generic;
using TransferenciaCalor.Models;

namespace TransferenciaCalor.Data
{
    /// <summary>
    /// Catálogo fixo de materiais disponíveis para a simulação, com suas
    /// propriedades térmicas de referência.
    /// </summary>
    public static class MaterialCatalog
    {
        // Material(nome, k [W/(m·K)], densidade [kg/m³], calorEspecifico [J/(kg·K)])
        // Valores de referência aproximados, à temperatura ambiente.
        private static readonly List<Material> materiais = new List<Material>
        {
            new Material("Cobre",     401,   8960,  385),
            new Material("Alumínio",  237,   2700,  897),
            new Material("Ferro",      80,   7870,  449),
            new Material("Aço",        50,   7850,  490),
            new Material("Ouro",      318,  19300,  129),
            new Material("Prata",     429,  10490,  235),
            new Material("Vidro",       1.0, 2500,  840),
            new Material("Madeira",     0.15, 700, 1700),
            new Material("Concreto",    1.7, 2300,  880),
            new Material("Água",        0.6, 1000, 4186),
            new Material("Ar",          0.026, 1.2, 1005),
        };

        // Lista pública "somente leitura" dos materiais.
        // O resto do programa pode LER os materiais (percorrer, pegar pelo índice),
        // mas não pode adicionar, remover nem trocar nenhum item do catálogo.
        // AsReadOnly() não copia a lista: só coloca uma "capa protetora" em volta
        // dela que bloqueia qualquer alteração vinda de fora desta classe.
        public static IReadOnlyList<Material> Todos { get; } = materiais.AsReadOnly();
    }
}
