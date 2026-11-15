using System;
using System.Collections.Generic;
using System.Diagnostics;
using TransferenciaCalor.Models;
using TransferenciaCalor.Simulation;

namespace TransferenciaCalor.Persistencia
{
    /// <summary>Resultado de uma execução "sem tela" (Refazer e benchmark).</summary>
    public sealed class ResultadoExecucao
    {
        private readonly long ticks;
        private readonly double msCalculo;

        public ResultadoExecucao(long ticks, double msCalculo)
        {
            this.ticks = ticks;
            this.msCalculo = msCalculo;
        }

        public long Ticks { get { return ticks; } }

        /// <summary>Tempo acumulado dentro de Tick() (sem a cópia dos snapshots nem a fila).</summary>
        public double MsCalculo { get { return msCalculo; } }
    }

    /// <summary>
    /// Roda a simulação "em modo rápido", sem Timer e sem tela: usado pelo Refazer e pelo
    /// benchmark com banco. Segue a MESMA ordem de ações da simulação ao vivo
    /// (tick -> snapshot -> eventos do usuário -> próximo tick), por isso o resultado é
    /// comparável tick a tick com o da execução original.
    /// </summary>
    public static class ExecutorSimulacao
    {
        /// <summary>
        /// Executa até exatamente <paramref name="ticksAlvo"/> ticks. NÃO para por detecção de
        /// equilíbrio: o tick em que ela dispararia na tela depende do tamanho do lote por frame
        /// (velocidade) e quebraria a conferência. Se há sessão, ela deve já estar aberta (tick 0).
        /// </summary>
        public static ResultadoExecucao Executar(HeatGrid grade, IMotorSimulacao motor, long ticksAlvo,
            SessaoGravacao sessao, IList<EventoSimulacao> eventos)
        {
            IList<EventoSimulacao> pendentes = eventos ?? new List<EventoSimulacao>();
            int proximoEvento = 0;
            long tick = 0;
            long cronometroCalculo = 0;

            while (true)
            {
                // Eventos "devidos" no tick atual: aplicados DEPOIS do snapshot deste tick
                while (proximoEvento < pendentes.Count && pendentes[proximoEvento].Tick <= tick)
                {
                    Aplicar(grade, pendentes[proximoEvento]);
                    if (sessao != null)
                        sessao.RegistrarEvento(pendentes[proximoEvento]);
                    proximoEvento++;
                }

                if (tick >= ticksAlvo)
                    break;

                // Corta o lote no que vier primeiro: fim, próxima amostra ou próximo evento
                long limite = ticksAlvo;
                if (sessao != null)
                    limite = Math.Min(limite, tick + sessao.TicksAteProximaAmostra(tick));
                if (proximoEvento < pendentes.Count)
                    limite = Math.Min(limite, pendentes[proximoEvento].Tick);

                long lote = limite - tick;
                long t0 = Stopwatch.GetTimestamp();
                for (long i = 0; i < lote; i++)
                    motor.Tick();
                cronometroCalculo += Stopwatch.GetTimestamp() - t0;
                tick += lote;

                if (sessao != null)
                    sessao.AoConcluirTick(grade, tick);
            }

            double msCalculo = cronometroCalculo * 1000.0 / Stopwatch.Frequency;
            if (sessao != null)
                sessao.Finalizar(grade, tick, false, (long)msCalculo, true);
            return new ResultadoExecucao(tick, msCalculo);
        }

        // Reaplica uma ação do usuário exatamente como a UI a aplicou
        private static void Aplicar(HeatGrid grade, EventoSimulacao e)
        {
            if (e.Tipo == TipoEvento.TrocouMaterial)
                grade.TrocarMaterial(e.Linha, e.Coluna, e.Material);
            else
                grade.ObterCorpo(e.Linha, e.Coluna).TemperaturaAtual = e.Temperatura.Value;
        }
    }
}
