using System.Collections.Generic;
using TransferenciaCalor.Models;

namespace TransferenciaCalor.Persistencia
{
    /// <summary>
    /// Contrato de acesso ao banco. O resto do programa (gravador, Refazer, benchmark) só
    /// conhece esta interface, então o MySQL pode ser trocado (ou simulado em teste)
    /// sem mexer na lógica da simulação.
    /// Cada método abre e fecha a sua própria conexão (o pool do driver reaproveita), então
    /// a thread de gravação e a UI podem usar o mesmo repositório ao mesmo tempo.
    /// </summary>
    public interface IRepositorioSimulacao
    {
        /// <summary>Cria o banco e as tabelas se ainda não existirem (executa o schema.sql).</summary>
        void GarantirEsquema();

        /// <summary>Grava o cenário (grade no tick 0) e devolve o id.</summary>
        long SalvarCenario(string nome, int tamanho, IList<CelulaCenario> celulas);

        /// <summary>Cria uma execução (fim = NULL até ser finalizada) e devolve o id.</summary>
        long CriarExecucao(long cenarioId, DadosExecucao dados);

        /// <summary>Grava, numa transação, as leituras de todos os corpos dos snapshots informados.</summary>
        void GravarLeituras(long execucaoId, IList<SnapshotEstado> lote);

        /// <summary>Grava ações do usuário (troca de material, temperatura) com o tick exato.</summary>
        void GravarEventos(long execucaoId, IList<EventoSimulacao> eventos);

        /// <summary>Preenche fim, ticks, equilíbrio e os tempos acumulados da execução.</summary>
        void FinalizarExecucao(long execucaoId, long ticks, bool atingiuEquilibrio, long msCalculo, long msGravacao);

        /// <summary>Execuções originais (não são "refazer"), mais recentes primeiro.</summary>
        IList<ResumoExecucao> ListarExecucoesOriginais();

        CenarioCarregado CarregarCenario(long cenarioId);
        ExecucaoCarregada CarregarExecucao(long execucaoId);

        /// <summary>Eventos de uma execução, na ordem em que ocorreram (tick, depois id).</summary>
        IList<EventoSimulacao> CarregarEventos(long execucaoId);

        /// <summary>Consulta a view v_conferencia da execução refeita.</summary>
        ResultadoConferencia Conferir(long execucaoRefeitaId);

        /// <summary>Tamanho aproximado (MB) das tabelas do simulador, para o relatório.</summary>
        double TamanhoTabelasMb();

        /// <summary>Total de linhas gravadas em leituras (para o relatório).</summary>
        long ContarLeituras();
    }
}
