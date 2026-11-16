using System.Collections.Generic;
using TransferenciaCalor.Models;
using TransferenciaCalor.Simulation;

namespace TransferenciaCalor.Persistencia
{
    /// <summary>
    /// Regras de AMOSTRAGEM de uma execução gravada. Fica entre a simulação e o
    /// GravadorSimulacao: decide QUANDO tirar snapshot e evita gravar duas vezes o mesmo tick.
    ///
    /// Amostragem em TICKS, nunca em tempo de relógio: o relógio depende da velocidade da
    /// máquina e tornaria a gravação irreproduzível. Grava: tick 0, todo múltiplo de
    /// grav_a_cada_n, e SEMPRE o tick da pausa, do equilíbrio e o último.
    /// Ordem fixa em cada tick T: [calcula o tick T] -> [snapshot de T] -> [eventos do usuário em T]
    /// -> [tick T+1]. O Refazer repete exatamente essa ordem.
    /// </summary>
    public sealed class SessaoGravacao
    {
        private readonly GravadorSimulacao gravador;
        private readonly int gravACadaN;
        private long ultimoTickGravado = -1;
        private bool aberta;

        public SessaoGravacao(GravadorSimulacao gravador, int gravACadaN)
        {
            this.gravador = gravador;
            this.gravACadaN = gravACadaN < 1 ? 1 : gravACadaN;
        }

        public int GravACadaN { get { return gravACadaN; } }
        public bool Aberta { get { return aberta; } }

        /// <summary>
        /// Abre a execução: captura a grade como está AGORA (tick 0, já com materiais, lados e
        /// temperaturas editados pelo usuário) e grava a primeira leitura.
        /// </summary>
        public void Abrir(DadosExecucao dados, HeatGrid grade)
        {
            int n = grade.Tamanho;
            List<CelulaCenario> celulas = new List<CelulaCenario>(n * n);
            for (int l = 0; l < n; l++)
                for (int c = 0; c < n; c++)
                {
                    Body b = grade.ObterCorpo(l, c);
                    celulas.Add(new CelulaCenario(l, c, b.Material, b.LadoCubo, b.TemperaturaAtual));
                }

            gravador.IniciarExecucao(dados, n, celulas);
            aberta = true;
            ultimoTickGravado = -1;
            GravarSnapshot(grade, 0);
        }

        /// <summary>Quantos ticks faltam até a próxima amostra (para o laço de ticks cortar o lote nela).</summary>
        public long TicksAteProximaAmostra(long tickAtual)
        {
            return gravACadaN - (tickAtual % gravACadaN);
        }

        /// <summary>Chamado depois de cada tick (ou lote cortado na amostra): grava se este tick é amostrado.</summary>
        public void AoConcluirTick(HeatGrid grade, long tickAtual)
        {
            if (aberta && tickAtual % gravACadaN == 0)
                GravarSnapshot(grade, tickAtual);
        }

        public void RegistrarEvento(EventoSimulacao evento)
        {
            if (aberta)
                gravador.EnfileirarEvento(evento);
        }

        /// <summary>
        /// Grava o snapshot do tick atual (se ainda não gravado) e finaliza a execução.
        /// encerrar = false (Pausar): a execução pode ser retomada; true: não grava mais nada.
        /// </summary>
        public void Finalizar(HeatGrid grade, long tickAtual, bool atingiuEquilibrio, long msCalculo, bool encerrar)
        {
            if (!aberta)
                return;

            GravarSnapshot(grade, tickAtual);
            gravador.FinalizarExecucao(tickAtual, atingiuEquilibrio, msCalculo);
            if (encerrar)
                aberta = false;
        }

        private void GravarSnapshot(HeatGrid grade, long tick)
        {
            // Mesmo tick nunca é gravado duas vezes (a chave primária de leituras rejeitaria)
            if (tick == ultimoTickGravado || gravador.Falhou)
                return;

            ultimoTickGravado = tick;
            // A cópia é feita AQUI, depois que o Tick terminou por completo (inclusive no modo
            // paralelo: o Parallel.For só retorna quando todas as threads acabaram).
            gravador.EnfileirarLeitura(SnapshotEstado.Capturar(grade, tick));
        }
    }
}
