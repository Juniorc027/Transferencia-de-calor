# Relatório de Performance — N1 × N2 (RASCUNHO)

Simulador de Transferência de Calor — Programação Avançada de Sistemas.
Todos os números abaixo foram **medidos** nesta máquina (seção 4), salvo onde está escrito "não medido".

## 1. Objetivo

A N2 pede: (1) gravar em MySQL os resultados de cada interação; (2) paralelizar o processamento; (3) comparar o desempenho N1 × N2. Este relatório mostra **onde** o paralelismo foi aplicado, **onde não foi**, e se houve ganho ou perda. Perda é resultado válido quando medida e explicada, e aparece aqui.

## 2. O que mudou da N1 para a N2

| Item | N1 (tag `n1-entregue`) | N2 (branch `n2`) |
|---|---|---|
| Motor | `HeatSimulationEngine` (sequencial; só ganhou `: IMotorSimulacao`) | + `HeatSimulationEngineParalelo` (mesma física, 2 fases) |
| `Tick()` | varre pares e acumula energia em 2 corpos por par | **Fase A:** energia de cada aresta (1 vez); **Fase B:** cada corpo soma suas arestas |
| Alocação por tick | 1 `double[n,n]` (LOH a partir de N≈103) | 0 bytes (arrays reaproveitados) |
| `AtingiuEquilibrio()` | 1 chamada por frame | **igual à N1; NÃO paralelizado** |
| Banco | não tinha | MySQL via `MySqlConnector`, gravação em segundo plano |
| UI | — | checkboxes "Gravar no banco" e "Processamento paralelo", status do banco, lista + "Refazer" |

### Onde o paralelismo foi aplicado
Somente em `Tick()`, dividido em **faixas de linhas** (uma por thread) com `Parallel.For`, duas vezes por tick (Fase A e Fase B), em fork-join: o `Tick()` só retorna quando as duas fases terminam, continua sendo chamado pela thread da UI.

### Onde NÃO foi aplicado, e por quê
- **`AtingiuEquilibrio()`**: medido na N1, custa ~0,5% do tempo por tick a 10x e ~5% a 1x (a UI o chama uma vez por frame, não por tick). Pela Lei de Amdahl, paralelizar algo que pesa ~0,5% rende no máximo ~0,5% do total, e acordar threads a cada frame consumiria esse ganho.
- **Snapshot dos corpos** (~1,7 µs em N=15, ≤0,2% do tick): custo desprezível.
- **Gravação no banco**: não é paralela, é *assíncrona* (1 thread de fundo, produtor-consumidor). O gargalo é o servidor (seção 6), não a CPU.

## 3. Desenho sem condição de corrida (e sem lock)

A N1 escreve a energia de **dois** corpos por par (`ProcessarPar`: `a -= energia; b += energia`). Duas threads processando pares vizinhos escreveriam na mesma posição. A N2 evita isso por construção:
1. **Fase A**: `energiaHorizontal[l,c]` e `energiaVertical[l,c]` guardam a energia (q·Δt) de cada aresta. Cada thread escreve só nas arestas das **suas** linhas e só lê temperaturas.
2. `Parallel.For` retorna → todas as arestas estão prontas (barreira).
3. **Fase B**: cada corpo soma as arestas adjacentes e atualiza a **própria** temperatura. Só lê arestas, só escreve no próprio corpo.

**Mesma conta, mesma ordem.** A N1 acumula, em cada corpo, nesta ordem: aresta de cima, esquerda, direita, baixo (por causa da varredura linha a linha). A N2 soma exatamente nessa ordem, com os mesmos sinais, `Math.Pow`, `Math.Min` do menor k e do menor lado. Como soma de ponto flutuante não é associativa, essa ordem é o que garante o mesmo resultado bit a bit.

**Verificação (`Benchmark --verificar`, executado):** N1 × N2-seq × N2-paralelo (16 threads e 3 threads, esta com faixas desiguais), 3 cenários (material único, mistos com lados iguais, mistos com lados diferentes) × N = 5, 15, 20, 60 × 5.000 ticks, comparando **todas** as temperaturas a **cada** tick, mais `AtingiuEquilibrio()` a cada 100 ticks. Resultado: **PASSOU, erro máximo absoluto = 0** (12 casos).

## 4. Metodologia e ambiente

Release, x64 (Prefer 32-bit desligado), sem debugger, aquecimento (JIT) descartado, **7 repetições** por medição, cada uma com **≥ ~100 ms**, `Stopwatch.GetTimestamp`, mediana/média/desvio-padrão, GC coletado antes de cada repetição, processo em prioridade alta, plano de energia "Alto desempenho". Grade inicial: materiais sorteados do catálogo + gradiente de temperatura 300–800 K, semente fixa. O paralelo foi medido com `tamanhoMinimoParaParalelizar = 1` (força o caminho paralelo em todo N, inclusive onde perde).

| Item | Valor |
|---|---|
| CPU | AMD Ryzen 7 5700G, 8 núcleos físicos / 16 lógicos |
| RAM | 15,4 GB |
| Windows | 11 Pro 10.0.26200 |
| .NET Framework | 4.8.1 (projeto compila para 4.8) |
| MySQL (só Fase 5) | 8.0.46 Community, **instância local descartável** (porta 3399, configuração padrão, sem tuning), no mesmo PC |

Dados brutos: `Benchmark/resultados_n2_DESKTOP-6SA7005.csv/.md` (motores), `Benchmark/resultados_banco_DESKTOP-6SA7005.csv/.md` (banco), `Benchmark/resultados_DESKTOP-6SA7005.*` (linha de base da N1).

## 5. Resultados: motores

Tempo por `Tick()` — **mediana, µs** (dispersão completa no `.md`; CV típico 1–9%):

| N | N1 | N2-seq | par T1 | par T2 | par T4 | par T8 | par T16 |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 5 | 2,08 | 2,02 | 5,21 | 6,20 | 6,23 | 6,52 | 6,57 |
| 10 | 10,8 | 9,16 | 14,0 | 11,3 | 11,0 | 12,0 | 12,1 |
| 15 | 21,9 | 21,3 | 27,5 | 20,7 | 17,5 | 17,4 | 18,0 |
| 20 | 41,8 | 39,2 | 47,3 | 31,3 | 23,8 | 22,7 | 23,8 |
| 30 | 91,7 | 86,4 | 99,2 | 66,8 | 48,1 | 37,8 | 33,7 |
| 60 | 387 | 361 | 433 | 259 | 167 | 109 | 83,5 |
| 100 | 1.060 | 1.021 | 1.107 | 701 | 478 | 257 | 211 |
| 200 | 4.587 | 4.172 | 4.259 | 2.833 | 1.588 | 961 | 717 |
| 400 | 18.646 | 17.962 | 18.071 | 11.314 | 6.222 | 3.748 | 2.884 |

**Speedup sobre a N2 sequencial** (isola o efeito das threads) e **eficiência** (speedup ÷ threads, 16 threads):

| N | T2 | T4 | T8 | T16 | eficiência T16 |
|---:|---:|---:|---:|---:|---:|
| 5 | 0,32× | 0,32× | 0,31× | 0,31× | 0,02 |
| 10 | 0,81× | 0,83× | 0,77× | 0,76× | 0,05 |
| 15 | 1,03× | 1,22× | 1,22× | 1,19× | 0,07 |
| 20 | 1,25× | 1,65× | 1,73× | 1,65× | 0,10 |
| 30 | 1,29× | 1,80× | 2,29× | 2,56× | 0,16 |
| 60 | 1,39× | 2,17× | 3,31× | 4,32× | 0,27 |
| 100 | 1,46× | 2,14× | 3,97× | 4,84× | 0,30 |
| 200 | 1,47× | 2,63× | 4,34× | 5,82× | 0,36 |
| 400 | 1,59× | 2,89× | 4,79× | 6,23× | 0,39 |

Speedup sobre a **N1** com 16 threads: 0,32× (N=5), 0,89× (N=10), 1,22× (15), 1,76× (20), 2,72× (30), 4,64× (60), 5,02× (100), 6,40× (200), 6,47× (400).

**Ponto de cruzamento** (primeiro N medido em que o paralelo vence com margem estatística): **N = 15** para 4, 8 e 16 threads; **N = 20** para 2 threads; 1 thread (só overhead) nunca vence. O cruzamento real fica **entre N=10 e N=15**. Por isso o padrão de `tamanhoMinimoParaParalelizar` ficou em **15** (provisório era 40). Em N=15 o ganho é pequeno (1,2×, ≈4 µs por tick).

**Alocações e GC:** N1 aloca 241 B/tick (N=5) a 1,28 MB/tick (N=400); a partir de N=200 o array vai para o LOH e aparecem coletas gen1/gen2 (≈87 de cada por 1.000 ticks em N=200). A N2 sequencial aloca **0 B/tick** em todos os N. A N2 paralela aloca 2–9 KB/tick — é infraestrutura do `Parallel.For` (não são arrays da simulação) — sem coletas gen1/gen2.

## 6. Resultados: impacto do banco (Fase 5)

Mesma grade e 2.000 ticks nos 4 cenários (A = N1 sem banco, B = N2 paralelo sem banco, C = N1 com gravação, D = N2 paralelo com gravação). Medianas, ms. "simulação" = tempo até o último tick (o que o usuário sente); "esvaziar" = tempo extra até o banco terminar.

| N | grav a cada | A | B | C sim. | D sim. | D esvaziar | D total | linhas | linhas/s (D) | produtor bloqueado (D) |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 20 | 100 | 82,9 | 52,3 | 100 | 58,5 | 239 | 299 | 8.400 | 28.091 | 0 ms |
| 20 | 10 | 82,9 | 52,3 | 105 | 53,3 | 2.289 | 2.341 | 80.400 | 34.342 | 0 ms |
| 20 | 1 | 82,9 | 52,3 | 22.355 | 22.400 | 3.254 | 25.654 | 800.400 | 31.200 | 22.243 ms |
| 60 | 100 | 968 | 175,5 | 983 | 175,5 | 2.384 | 2.552 | 75.600 | 29.625 | 0 ms |
| 60 | 10 | 968 | 175,5 | 1.024 | 203,5 | 13.921 | 14.122 | 723.600 | 51.240 | 0 ms |

(N=60 com grav a cada 1 tick foi pulado: 7,2 milhões de linhas.) Total gravado no benchmark: **10.142.400** linhas em `leituras`, ≈ **799 MB** (dados + índices), ~79 bytes/linha.

Leitura honesta:
- **Gravar a cada 100 ticks (padrão) quase não atrapalha a simulação** (D contra B: +12% em N=20, 0% em N=60), mas o servidor ainda leva 0,2–2,4 s depois do último tick para terminar.
- **O banco é o gargalo**: ~30–50 mil linhas/s, praticamente independente do motor (C e D gravam à mesma velocidade). O consumidor ficou ocupado quase o tempo todo (`ms_gravacao` ≈ total).
- **Gravar a cada tick (N=20) é inviável neste setup**: a fila (256) enche, o produtor bloqueia por ~22 s e o ganho do motor paralelo some (D ≈ C). É exatamente o comportamento "fila cheia bloqueia o produtor" projetado para **não perder dados**.
- Duas execuções do mesmo benchmark deram vazão diferente (≈50 mil linhas/s na primeira, ≈31–36 mil na segunda para o mesmo caso): há variação de disco/SO, então os valores de banco têm **incerteza maior** que os de CPU (sem desvio-padrão aqui: 3 repetições, mediana).
- Servidor local, padrão (`innodb_flush_log_at_trx_commit=1`), sem tuning, **não é o servidor do laboratório**.

## 7. Análise

**Quando o paralelo ganha:** a partir de N≈15 e cada vez mais com N grande (até 6,5× sobre a N1 e 6,2× sobre a N2-seq em N=400, com 16 threads).
**Quando perde:** N ≤ 10, onde o `Tick()` custa 2–10 µs e acordar threads duas vezes por tick custa mais que o cálculo (0,3× em N=5). Não é desprezível: em N=5 o paralelo é ~3× **mais lento**.
**Por que a eficiência é baixa:** 0,39 com 16 threads em N=400 (0,60 com 8). Causas medidas/plausíveis: (a) o overhead de 2 fork-joins por tick pesa em N pequeno; (b) 16 threads são 8 núcleos físicos com SMT, que não rende 2×; (c) o laço lê muita memória por conta (corpos são objetos e `CalcularCalorSensivel` é recalculado por aresta), então em N grande a **banda de memória/cache** limita; (d) Lei de Amdahl: a parte serial (criação da Parallel.For, fork-join) é pequena mas não zero. Estas causas **não foram isoladas uma a uma** (não medido).
**Ganho vem do paralelismo, não da reorganização:** a N2 sequencial é só 1,03–1,18× mais rápida que a N1 (menos alocação e acesso por vetor plano). Quase todo o ganho da N2 vem das threads.
**Para a UI atual não faz diferença prática:** a UI limita N a 20, onde a N1 já sustenta ~25 mil ticks/s (a UI pede no máximo ~4 mil) e o paralelo daria ~1,7× em N=20. O benefício real do paralelismo só aparece em N grande, via Benchmark.
**Impacto do banco:** custa pouco à simulação se amostrado a cada 100 ticks; vira o gargalo dominante se amostrado a cada tick.

## 8. Fidelidade e Refazer (verificados)

- Gravação + Refazer testados **no MySQL 8.0.46 real** (`Benchmark --verificar-gravacao --conexao ...`): execução ao vivo com eventos (inclusive dois no mesmo tick) e pausa em tick não múltiplo de 100; Refazer com N1, N2-seq e N2-par (3 e 16 threads): **diferença máxima = 0 K sobre 3.150 pontos**, em todos.
- A conferência compara só ticks que existem nas duas execuções; o tick de uma pausa que não é múltiplo de 100 só existe na original, então não entra na comparação (ex.: 14 ticks × 225 corpos, não 15).
- Doubles gravados com `G17` + `InvariantCulture` (2.000.400 valores, ida e volta idêntica).
- O app cria o banco e as tabelas sozinho ao abrir (verificado derrubando o banco e abrindo o executável).

## 9. Limitações

- **Uma única máquina** (Ryzen 7 5700G). Falta rodar no PC do laboratório; o cruzamento (N≈15) e a eficiência dependem do nº de núcleos (com 2 threads o ganho em N=15 foi 1,03×).
- Banco medido num servidor **local descartável, padrão, no mesmo PC** que roda o simulador (CPU e disco compartilhados). Versão do servidor do laboratório: desconhecida.
- Teste automatizado da **interface gráfica não foi feito**: a UI foi compilada, aberta e fechada, e criou o banco, mas cliques (Iniciar com "Gravar no banco", editar célula durante a gravação, Refazer pela tela) **não foram exercitados** por automação. A lógica que a UI usa (`SessaoGravacao`, `ExecutorSimulacao`, `GravadorSimulacao`) é a mesma testada pelo `--verificar-gravacao`.
- A tabela `transferencias` existe no schema mas **não é preenchida** (era opcional, "se ligada por flag").
- A tela "Reproduzir" (opcional) **não foi feita**.
- Se o app cair no meio de uma execução já pausada uma vez, a linha de `execucoes` mantém o `fim` do último Pausar (não volta a NULL ao retomar).
- Cada gravação reutiliza 1 conexão por lote; não foi testado servidor remoto/lento por rede.

## 10. Conclusão

A N2 mantém o **resultado idêntico** ao da N1 (erro 0) e remove a alocação por tick. O paralelismo **compensa a partir de N≈15** e chega a ~6× em N=400, mas **perde** em grades pequenas e tem eficiência modesta (0,4–0,6 em N grande). Para os tamanhos permitidos hoje na UI (N ≤ 20), o paralelismo não é necessário. A gravação no banco é viável como amostragem (a cada 100 ticks) e o banco, não a CPU, é o gargalo quando se tenta gravar a cada tick.
