using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TransferenciaCalor.Models;
using TransferenciaCalor.Persistencia;

namespace TransferenciaCalor.Benchmark
{
    /// <summary>
    /// Repositório FALSO, só em memória, usado para testar a lógica de gravação (amostragem,
    /// ordem dos eventos, fila limitada, falha do banco) sem depender de servidor.
    /// Imita as regras do MySQL que importam: chave primária de leituras rejeita tick repetido
    /// e a conferência segue a mesma lógica da view v_conferencia.
    /// Permite injetar latência (banco lento) e falha (banco fora do ar).
    /// </summary>
    internal sealed class RepositorioEmMemoria : IRepositorioSimulacao
    {
        private sealed class Leitura
        {
            private readonly double temperatura;
            public Leitura(double temperatura) { this.temperatura = temperatura; }
            public double Temperatura { get { return temperatura; } }
        }

        private sealed class RegistroExecucao
        {
            public long Id { get; set; }
            public long CenarioId { get; set; }
            public long? Origem { get; set; }
            public double DeltaT { get; set; }
            public double Limiar { get; set; }
            public int GravN { get; set; }
            public long Ticks { get; set; }
            public bool Fim { get; set; }
            public bool Equilibrio { get; set; }
        }

        private readonly object trava = new object();
        private readonly Dictionary<long, string> nomes = new Dictionary<long, string>();
        private readonly Dictionary<long, int> tamanhos = new Dictionary<long, int>();
        private readonly Dictionary<long, IList<CelulaCenario>> celulas = new Dictionary<long, IList<CelulaCenario>>();
        private readonly Dictionary<long, RegistroExecucao> execucoes = new Dictionary<long, RegistroExecucao>();
        private readonly Dictionary<long, List<EventoSimulacao>> eventos = new Dictionary<long, List<EventoSimulacao>>();
        // chave: execucao, tick, linha, coluna
        private readonly Dictionary<Tuple<long, long, int, int>, Leitura> leituras = new Dictionary<Tuple<long, long, int, int>, Leitura>();
        private long proximoId = 1;

        private int latenciaPorLoteMs;
        private bool falharAoGravarLeituras;

        public void ConfigurarLatencia(int ms) { latenciaPorLoteMs = ms; }
        public void ConfigurarFalha(bool falhar) { falharAoGravarLeituras = falhar; }

        public bool ExecucaoFinalizada(long execucaoId) { lock (trava) { return execucoes[execucaoId].Fim; } }
        public long TotalLeituras { get { lock (trava) { return leituras.Count; } } }

        /// <summary>Ticks gravados (distintos) de uma execução, em ordem.</summary>
        public List<long> TicksGravados(long execucaoId)
        {
            lock (trava)
                return leituras.Keys.Where(k => k.Item1 == execucaoId).Select(k => k.Item2).Distinct().OrderBy(t => t).ToList();
        }

        public void GarantirEsquema() { }

        public long SalvarCenario(string nome, int tamanho, IList<CelulaCenario> cels)
        {
            lock (trava)
            {
                long id = proximoId++;
                nomes[id] = nome; tamanhos[id] = tamanho; celulas[id] = cels.ToList();
                return id;
            }
        }

        public long CriarExecucao(long cenarioId, DadosExecucao dados)
        {
            lock (trava)
            {
                long id = proximoId++;
                execucoes[id] = new RegistroExecucao { Id = id, CenarioId = cenarioId, Origem = dados.ExecucaoOrigemId, DeltaT = dados.DeltaT, Limiar = dados.LimiarEquilibrio, GravN = dados.GravACadaN };
                eventos[id] = new List<EventoSimulacao>();
                return id;
            }
        }

        public void GravarLeituras(long execucaoId, IList<SnapshotEstado> lote)
        {
            if (latenciaPorLoteMs > 0) Thread.Sleep(latenciaPorLoteMs);
            if (falharAoGravarLeituras) throw new InvalidOperationException("Falha injetada: servidor fora do ar.");

            lock (trava)
            {
                // Valida tudo antes de gravar (transação: tudo ou nada)
                List<KeyValuePair<Tuple<long, long, int, int>, Leitura>> novas = new List<KeyValuePair<Tuple<long, long, int, int>, Leitura>>();
                HashSet<Tuple<long, long, int, int>> vistas = new HashSet<Tuple<long, long, int, int>>();
                foreach (SnapshotEstado s in lote)
                    for (int i = 0; i < s.Quantidade; i++)
                    {
                        Tuple<long, long, int, int> chave = Tuple.Create(execucaoId, s.Tick, i / s.Tamanho, i % s.Tamanho);
                        if (leituras.ContainsKey(chave) || !vistas.Add(chave))
                            throw new InvalidOperationException("Duplicate entry (chave primaria de leituras): tick " + s.Tick);
                        novas.Add(new KeyValuePair<Tuple<long, long, int, int>, Leitura>(chave, new Leitura(s.Temperatura(i))));
                    }
                foreach (var kv in novas) leituras[kv.Key] = kv.Value;
            }
        }

        public void GravarEventos(long execucaoId, IList<EventoSimulacao> evs)
        {
            lock (trava) eventos[execucaoId].AddRange(evs);
        }

        public void FinalizarExecucao(long execucaoId, long ticks, bool atingiuEquilibrio, long msCalculo, long msGravacao)
        {
            lock (trava)
            {
                RegistroExecucao r = execucoes[execucaoId];
                r.Fim = true; r.Ticks = ticks; r.Equilibrio = atingiuEquilibrio;
            }
        }

        public IList<ResumoExecucao> ListarExecucoesOriginais()
        {
            lock (trava)
                return execucoes.Values.Where(e => !e.Origem.HasValue).OrderByDescending(e => e.Id)
                    .Select(e => new ResumoExecucao(e.Id, e.CenarioId, nomes[e.CenarioId], tamanhos[e.CenarioId], e.Ticks, ModoExecucao.Sequencial, e.Fim)).ToList();
        }

        // Cada Material é devolvido como objeto NOVO por id de cenário (como o banco faria)
        public CenarioCarregado CarregarCenario(long cenarioId)
        {
            lock (trava)
            {
                Dictionary<string, Material> unicos = new Dictionary<string, Material>();
                List<CelulaCenario> copia = new List<CelulaCenario>();
                foreach (CelulaCenario c in celulas[cenarioId])
                {
                    string chave = c.Material.Nome + "|" + c.Material.K + "|" + c.Material.Densidade + "|" + c.Material.CalorEspecifico;
                    Material m;
                    if (!unicos.TryGetValue(chave, out m))
                        unicos[chave] = m = new Material(c.Material.Nome, c.Material.K, c.Material.Densidade, c.Material.CalorEspecifico);
                    copia.Add(new CelulaCenario(c.Linha, c.Coluna, m, c.LadoCubo, c.TemperaturaInicial));
                }
                return new CenarioCarregado(cenarioId, nomes[cenarioId], tamanhos[cenarioId], copia);
            }
        }

        public ExecucaoCarregada CarregarExecucao(long execucaoId)
        {
            lock (trava)
            {
                RegistroExecucao e = execucoes[execucaoId];
                return new ExecucaoCarregada(e.Id, e.CenarioId, e.DeltaT, e.Limiar, e.GravN, e.Ticks, e.Fim);
            }
        }

        public IList<EventoSimulacao> CarregarEventos(long execucaoId)
        {
            lock (trava)
                return eventos[execucaoId].Select(e => new EventoSimulacao(e.Tick, e.Tipo, e.Linha, e.Coluna,
                    e.Material == null ? null : new Material(e.Material.Nome, e.Material.K, e.Material.Densidade, e.Material.CalorEspecifico), e.Temperatura))
                    .OrderBy(e => e.Tick).ToList(); // OrderBy é estável: mantém a ordem de inserção dentro do tick (como "ORDER BY tick, id")
        }

        // Mesma lógica da view v_conferencia (junção por tick, linha, coluna)
        public ResultadoConferencia Conferir(long execucaoRefeitaId)
        {
            lock (trava)
            {
                long origem = execucoes[execucaoRefeitaId].Origem.Value;
                long pontos = 0; double max = 0;
                foreach (var kv in leituras.Where(k => k.Key.Item1 == execucaoRefeitaId))
                {
                    Leitura original;
                    if (leituras.TryGetValue(Tuple.Create(origem, kv.Key.Item2, kv.Key.Item3, kv.Key.Item4), out original))
                    {
                        pontos++;
                        max = Math.Max(max, Math.Abs(original.Temperatura - kv.Value.Temperatura));
                    }
                }
                return new ResultadoConferencia(pontos, max);
            }
        }

        public double TamanhoTabelasMb() { return 0; }
        public long ContarLeituras() { return TotalLeituras; }

        // Reprodução é uma função da tela com MySQL; o benchmark em memória não a usa.
        // Só existe para a classe continuar implementando a interface.
        public IList<ExecucaoGravada> ListarExecucoesComLeituras() { throw new NotSupportedException(); }
        public IList<long> ListarTicksGravados(long execucaoId) { throw new NotSupportedException(); }
        public SnapshotGravado CarregarSnapshot(long execucaoId, long tick, int tamanho) { throw new NotSupportedException(); }
    }
}
