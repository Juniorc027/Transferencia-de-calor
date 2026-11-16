using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace TransferenciaCalor.Persistencia
{
    /// <summary>
    /// Gravação em segundo plano no padrão PRODUTOR-CONSUMIDOR.
    ///
    /// Produtor = a thread da simulação/UI: só COPIA o estado (snapshot imutável, ~1,7 us em
    /// N=15) e coloca na fila. Nunca fala com o banco.
    /// Consumidor = UMA thread criada UMA vez (não uma thread por registro: criar thread custa
    /// ~dezenas de us e milhares delas esgotariam a máquina). Ela agrupa snapshots em lotes e
    /// grava com poucos INSERTs grandes dentro de transação.
    ///
    /// Fila limitada: se o banco não acompanha, a fila enche e o produtor BLOQUEIA (preserva
    /// os dados em vez de descartar) — o tempo bloqueado é contado para o relatório.
    /// Falha do banco: o consumidor registra o erro, marca a execução como incompleta
    /// (fim fica NULL) e passa a descartar itens; a simulação nunca trava nem cai.
    /// </summary>
    public sealed class GravadorSimulacao : IDisposable
    {
        // ---- itens da fila (tipos privados: só este arquivo conhece o protocolo) ----
        private abstract class Item { }

        private sealed class ItemIniciar : Item
        {
            private readonly DadosExecucao dados;
            private readonly int tamanho;
            private readonly IList<CelulaCenario> celulas;
            public ItemIniciar(DadosExecucao dados, int tamanho, IList<CelulaCenario> celulas) { this.dados = dados; this.tamanho = tamanho; this.celulas = celulas; }
            public DadosExecucao Dados { get { return dados; } }
            public int Tamanho { get { return tamanho; } }
            public IList<CelulaCenario> Celulas { get { return celulas; } }
        }

        private sealed class ItemLeitura : Item
        {
            private readonly SnapshotEstado snapshot;
            public ItemLeitura(SnapshotEstado snapshot) { this.snapshot = snapshot; }
            public SnapshotEstado Snapshot { get { return snapshot; } }
        }

        private sealed class ItemEvento : Item
        {
            private readonly EventoSimulacao evento;
            public ItemEvento(EventoSimulacao evento) { this.evento = evento; }
            public EventoSimulacao Evento { get { return evento; } }
        }

        private sealed class ItemFinalizar : Item
        {
            private readonly long ticks;
            private readonly bool equilibrio;
            private readonly long msCalculo;
            public ItemFinalizar(long ticks, bool equilibrio, long msCalculo) { this.ticks = ticks; this.equilibrio = equilibrio; this.msCalculo = msCalculo; }
            public long Ticks { get { return ticks; } }
            public bool Equilibrio { get { return equilibrio; } }
            public long MsCalculo { get { return msCalculo; } }
        }

        // Marcador: o consumidor "levanta a mão" quando chegou até aqui (tudo antes já foi tratado)
        private sealed class ItemSinal : Item
        {
            private readonly ManualResetEventSlim sinal;
            public ItemSinal(ManualResetEventSlim sinal) { this.sinal = sinal; }
            public ManualResetEventSlim Sinal { get { return sinal; } }
        }

        // ---- estado ----
        private readonly IRepositorioSimulacao repositorio;
        private readonly BlockingCollection<Item> fila;
        private readonly Thread consumidor;
        private readonly int linhasPorLote;

        // Escritos pelo consumidor e lidos pela UI: acesso via Interlocked/Volatile
        private long linhasGravadas;
        private long ticksCronometroGravacao;   // Stopwatch ticks gastos executando comandos no banco
        private long ticksCronometroBloqueio;   // Stopwatch ticks que o produtor ficou preso na fila cheia
        private int maximoDaFila;
        private long execucaoAtualId = -1;      // lida pela UI, escrita só pelo consumidor
        private volatile bool falhou;
        private volatile string ultimoErro = "";
        private long itensDescartados;

        // Só o consumidor mexe nestes (sem lock)
        private readonly List<SnapshotEstado> leiturasPendentes = new List<SnapshotEstado>();
        private int linhasPendentes;

        /// <param name="capacidadeFila">Quantidade máxima de itens aguardando (snapshots). Acima disso o produtor bloqueia.</param>
        /// <param name="linhasPorLote">Linhas (corpos) acumuladas antes de gravar um lote.</param>
        public GravadorSimulacao(IRepositorioSimulacao repositorio, int capacidadeFila = 256, int linhasPorLote = 2000)
        {
            if (repositorio == null)
                throw new ArgumentNullException("repositorio");

            this.repositorio = repositorio;
            this.linhasPorLote = linhasPorLote;
            fila = new BlockingCollection<Item>(capacidadeFila);

            // Criada uma única vez, aqui. IsBackground: não impede o app de fechar.
            consumidor = new Thread(LacoConsumidor);
            consumidor.IsBackground = true;
            consumidor.Name = "GravadorSimulacao";
            consumidor.Start();
        }

        // ---------------------------------------------------------------- estatísticas (para a UI e o benchmark)

        public long LinhasGravadas { get { return Interlocked.Read(ref linhasGravadas); } }
        public int TamanhoDaFila { get { return fila.Count; } }
        public int MaximoDaFila { get { return Volatile.Read(ref maximoDaFila); } }
        public long ExecucaoAtualId { get { return Interlocked.Read(ref execucaoAtualId); } }
        public bool Falhou { get { return falhou; } }
        public string UltimoErro { get { return ultimoErro; } }
        public long ItensDescartados { get { return Interlocked.Read(ref itensDescartados); } }

        /// <summary>Tempo (ms) que o consumidor passou executando comandos no banco.</summary>
        public long MsGravacao { get { return CronometroParaMs(Interlocked.Read(ref ticksCronometroGravacao)); } }

        /// <summary>Tempo (ms) que o produtor ficou bloqueado porque a fila estava cheia.</summary>
        public double MsBloqueadoProdutor { get { return Interlocked.Read(ref ticksCronometroBloqueio) * 1000.0 / Stopwatch.Frequency; } }

        // ---------------------------------------------------------------- lado produtor

        /// <summary>
        /// Pede a criação do cenário (se for novo) e da execução. Roda no consumidor: se o banco
        /// estiver fora do ar, a UI não congela esperando a conexão.
        /// </summary>
        public void IniciarExecucao(DadosExecucao dados, int tamanho, IList<CelulaCenario> celulas)
        {
            falhou = false; // nova tentativa; o consumidor volta a marcar se falhar de novo
            ultimoErro = "";
            Enfileirar(new ItemIniciar(dados, tamanho, celulas));
        }

        public void EnfileirarLeitura(SnapshotEstado snapshot)
        {
            Enfileirar(new ItemLeitura(snapshot));
        }

        public void EnfileirarEvento(EventoSimulacao evento)
        {
            Enfileirar(new ItemEvento(evento));
        }

        public void FinalizarExecucao(long ticks, bool atingiuEquilibrio, long msCalculo)
        {
            Enfileirar(new ItemFinalizar(ticks, atingiuEquilibrio, msCalculo));
        }

        /// <summary>
        /// Espera a fila esvaziar (tudo que foi enfileirado até agora já gravado ou descartado).
        /// Devolve false se o tempo acabou antes. Usado ao fechar o app e no benchmark.
        /// </summary>
        public bool EsperarEsvaziar(int timeoutMs)
        {
            using (ManualResetEventSlim sinal = new ManualResetEventSlim(false))
            {
                if (!TentarEnfileirar(new ItemSinal(sinal), timeoutMs))
                    return false;
                return sinal.Wait(timeoutMs);
            }
        }

        private void Enfileirar(Item item)
        {
            // Falha anterior: não faz sentido empilhar o que será descartado (e bloquear a simulação)
            if (falhou && !(item is ItemIniciar))
            {
                Interlocked.Increment(ref itensDescartados);
                return;
            }
            TentarEnfileirar(item, Timeout.Infinite);
        }

        private bool TentarEnfileirar(Item item, int timeoutMs)
        {
            if (fila.IsAddingCompleted)
                return false;

            try
            {
                // Caminho rápido: há espaço, não mede nada
                if (fila.TryAdd(item))
                {
                    AtualizarMaximo();
                    return true;
                }

                // Fila cheia: o produtor espera (preserva os dados) e o tempo é contado
                long t0 = Stopwatch.GetTimestamp();
                bool ok = fila.TryAdd(item, timeoutMs);
                Interlocked.Add(ref ticksCronometroBloqueio, Stopwatch.GetTimestamp() - t0);
                if (ok) AtualizarMaximo();
                return ok;
            }
            catch (InvalidOperationException)
            {
                return false; // CompleteAdding foi chamado durante o Dispose
            }
        }

        private void AtualizarMaximo()
        {
            int atual = fila.Count;
            int visto;
            while (atual > (visto = Volatile.Read(ref maximoDaFila)))
                Interlocked.CompareExchange(ref maximoDaFila, atual, visto);
        }

        // ---------------------------------------------------------------- lado consumidor

        private void LacoConsumidor()
        {
            foreach (Item item in fila.GetConsumingEnumerable())
            {
                try
                {
                    Processar(item);
                }
                catch (Exception ex)
                {
                    RegistrarFalha(ex);
                }
            }
        }

        private void Processar(Item item)
        {
            ItemSinal sinal = item as ItemSinal;
            if (sinal != null)
            {
                DescarregarLeituras();
                sinal.Sinal.Set();
                return;
            }

            ItemIniciar iniciar = item as ItemIniciar;
            if (iniciar != null)
            {
                IniciarNoBanco(iniciar);
                return;
            }

            // Depois de uma falha (ou sem execução aberta) o resto é descartado: a execução
            // fica incompleta (fim NULL) e a simulação segue sem gravar.
            if (Interlocked.Read(ref execucaoAtualId) < 0)
            {
                Interlocked.Increment(ref itensDescartados);
                return;
            }

            ItemLeitura leitura = item as ItemLeitura;
            if (leitura != null)
            {
                leiturasPendentes.Add(leitura.Snapshot);
                linhasPendentes += leitura.Snapshot.Quantidade;
                // Grava quando o lote encheu OU quando a fila esvaziou (nada mais para agrupar)
                if (linhasPendentes >= linhasPorLote || fila.Count == 0)
                    DescarregarLeituras();
                return;
            }

            // Eventos e fim vêm DEPOIS das leituras anteriores: descarrega antes para manter a ordem
            DescarregarLeituras();

            ItemEvento evento = item as ItemEvento;
            if (evento != null)
            {
                Cronometrar(() => repositorio.GravarEventos(Interlocked.Read(ref execucaoAtualId), new[] { evento.Evento }));
                return;
            }

            ItemFinalizar fim = item as ItemFinalizar;
            if (fim != null)
            {
                long msGravacaoAteAgora = MsGravacao;
                Cronometrar(() => repositorio.FinalizarExecucao(Interlocked.Read(ref execucaoAtualId), fim.Ticks, fim.Equilibrio, fim.MsCalculo, msGravacaoAteAgora));
            }
        }

        private void IniciarNoBanco(ItemIniciar iniciar)
        {
            // Zera o que sobrou de uma execução anterior (pendências nunca atravessam execuções)
            leiturasPendentes.Clear();
            linhasPendentes = 0;
            Interlocked.Exchange(ref execucaoAtualId, -1);

            Cronometrar(() =>
            {
                long cenarioId = iniciar.Dados.CenarioExistenteId.HasValue
                    ? iniciar.Dados.CenarioExistenteId.Value
                    : repositorio.SalvarCenario(iniciar.Dados.NomeCenario, iniciar.Tamanho, iniciar.Celulas);
                long execucaoId = repositorio.CriarExecucao(cenarioId, iniciar.Dados);
                Interlocked.Exchange(ref execucaoAtualId, execucaoId);
            });
            falhou = false;
        }

        private void DescarregarLeituras()
        {
            if (leiturasPendentes.Count == 0)
                return;

            List<SnapshotEstado> lote = new List<SnapshotEstado>(leiturasPendentes);
            int linhas = linhasPendentes;
            leiturasPendentes.Clear();
            linhasPendentes = 0;

            long execucaoId = Interlocked.Read(ref execucaoAtualId);
            Cronometrar(() => repositorio.GravarLeituras(execucaoId, lote));
            Interlocked.Add(ref linhasGravadas, linhas);
        }

        // Mede o tempo gasto no banco (ms_gravacao da execução)
        private void Cronometrar(Action acao)
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                acao();
            }
            finally
            {
                Interlocked.Add(ref ticksCronometroGravacao, Stopwatch.GetTimestamp() - t0);
            }
        }

        private void RegistrarFalha(Exception ex)
        {
            ultimoErro = ex.Message;
            falhou = true;
            leiturasPendentes.Clear();
            linhasPendentes = 0;
            // Sem execução aberta: tudo que chegar até o próximo "Iniciar" é descartado
            Interlocked.Exchange(ref execucaoAtualId, -1);
        }

        private static long CronometroParaMs(long ticks)
        {
            return ticks * 1000 / Stopwatch.Frequency;
        }

        /// <summary>Encerra a thread de gravação depois de gravar o que estiver na fila.</summary>
        public void Dispose()
        {
            fila.CompleteAdding();
            consumidor.Join(10000);
            fila.Dispose();
        }
    }
}
