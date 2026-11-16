# Resultados do benchmark — DESKTOP-6SA7005

Valores medidos pelo programa `Benchmark` (Release, sem debugger). "n/m" = não medido. Tempos em microssegundos (µs) por operação, salvo indicação.

## Ambiente

| Item | Valor |
|---|---|
| maquina | DESKTOP-6SA7005 |
| data_hora | 2026-11-14 15:54:27 |
| cpu | AMD Ryzen 7 5700G with Radeon Graphics |
| nucleos_fisicos | 8 |
| nucleos_logicos | 16 |
| ram | 15.4 GB |
| windows | Microsoft Windows 11 Pro (10.0.26200) |
| dotnet_framework | 4.8.1 ou superior (Release=533509) |
| clr | 4.0.30319.42000 |
| configuracao | Release |
| plataforma | x64 (processo 64 bits; Prefer 32-bit desligado) |
| debugger_anexado | não |
| gc | estação de trabalho (workstation) |
| plano_energia | GUID do Esquema de Energia: 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  (Alto desempenho) |
| N máximo medido | 400 |

## Tempo por operação — MEDIANA (µs)

| N | Tick() | Equilíbrio (misto) | Equilíbrio (varredura completa) | Snapshot (arrays novos) | Snapshot (reuso) | new double[n,n] | Parallel.For vazio (N it.) | Parallel.For vazio (N² it.) |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 5 | 2.131 | 2.025 | 2.490 | 0.184 | 0.090 | 0.071 | 1.790 | 3.205 |
| 10 | 9.461 | 8.955 | 11.0 | 0.754 | 0.340 | 0.119 | 1.893 | 3.339 |
| 15 | 21.6 | 20.5 | 25.9 | 1.677 | 0.776 | 0.117 | 2.349 | 4.449 |
| 20 | 38.9 | 37.2 | 46.9 | 3.289 | 1.335 | 0.179 | 2.835 | 5.543 |
| 30 | 89.7 | 84.9 | 105.4 | 5.969 | 3.129 | 0.306 | 3.795 | 8.933 |
| 60 | 380.0 | 350.0 | 450.2 | 19.8 | 12.4 | 0.859 | 3.204 | 16.1 |
| 100 | 1069.3 | 1011.3 | 1346.1 | 59.9 | 35.4 | 1.837 | 3.668 | 23.9 |
| 200 | 4454.2 | 4230.0 | 5678.0 | 684.1 | 148.8 | 222.3 | 4.365 | 45.1 |
| 400 | 18955.2 | 16824.3 | 22279.8 | 4496.2 | 1550.5 | 478.9 | 5.863 | 127.6 |

## Dispersão — média ± desvio-padrão (µs) e coeficiente de variação

| N | Tick() | Equilíbrio (misto) | Equilíbrio (varredura completa) | Snapshot (arrays novos) | Snapshot (reuso) | new double[n,n] | Parallel.For vazio (N it.) | Parallel.For vazio (N² it.) |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 5 | 2.132 ± 0.012 (0.6%) | 2.039 ± 0.027 (1.3%) | 2.520 ± 0.054 (2.1%) | 0.183 ± 0.006 (3.0%) | 0.091 ± 0.002 (2.5%) | 0.073 ± 0.005 (7.2%) | 1.773 ± 0.110 (6.2%) | 3.217 ± 0.049 (1.5%) |
| 10 | 9.515 ± 0.182 (1.9%) | 9.046 ± 0.376 (4.2%) | 11.1 ± 0.147 (1.3%) | 0.782 ± 0.060 (7.6%) | 0.342 ± 0.005 (1.5%) | 0.119 ± 0.015 (12.6%) | 1.893 ± 0.033 (1.8%) | 3.390 ± 0.209 (6.2%) |
| 15 | 21.6 ± 0.272 (1.3%) | 20.5 ± 0.269 (1.3%) | 25.8 ± 0.331 (1.3%) | 1.738 ± 0.149 (8.6%) | 0.787 ± 0.034 (4.3%) | 0.121 ± 0.010 (8.4%) | 2.344 ± 0.069 (2.9%) | 4.550 ± 0.381 (8.4%) |
| 20 | 39.4 ± 1.142 (2.9%) | 37.2 ± 0.308 (0.8%) | 47.1 ± 0.699 (1.5%) | 3.256 ± 0.123 (3.8%) | 1.377 ± 0.068 (5.0%) | 0.173 ± 0.012 (6.8%) | 2.866 ± 0.183 (6.4%) | 5.632 ± 0.298 (5.3%) |
| 30 | 89.2 ± 0.725 (0.8%) | 85.5 ± 1.260 (1.5%) | 106.2 ± 1.514 (1.4%) | 5.944 ± 0.254 (4.3%) | 3.130 ± 0.044 (1.4%) | 0.305 ± 0.028 (9.1%) | 3.948 ± 0.394 (10.0%) | 9.043 ± 0.410 (4.5%) |
| 60 | 387.5 ± 20.9 (5.4%) | 367.1 ± 26.9 (7.3%) | 459.1 ± 16.8 (3.7%) | 20.1 ± 1.372 (6.8%) | 12.4 ± 0.201 (1.6%) | 0.875 ± 0.170 (19.5%) | 3.274 ± 0.268 (8.2%) | 16.1 ± 0.075 (0.5%) |
| 100 | 1076.5 ± 28.5 (2.6%) | 1012.4 ± 12.5 (1.2%) | 1351.3 ± 18.7 (1.4%) | 60.4 ± 3.485 (5.8%) | 35.7 ± 1.208 (3.4%) | 1.877 ± 0.128 (6.8%) | 3.694 ± 0.094 (2.6%) | 23.9 ± 0.138 (0.6%) |
| 200 | 4445.3 ± 49.5 (1.1%) | 4250.8 ± 117.7 (2.8%) | 5864.4 ± 409.8 (7.0%) | 690.8 ± 31.1 (4.5%) | 152.6 ± 16.1 (10.5%) | 231.3 ± 21.1 (9.1%) | 4.432 ± 0.165 (3.7%) | 46.4 ± 2.299 (5.0%) |
| 400 | 18967.1 ± 577.8 (3.0%) | 16841.9 ± 380.4 (2.3%) | 22570.4 ± 783.3 (3.5%) | 4787.2 ± 529.8 (11.1%) | 1513.0 ± 343.4 (22.7%) | 478.9 ± 5.374 (1.1%) | 5.915 ± 0.277 (4.7%) | 123.5 ± 10.6 (8.6%) |

Repetições por medição: 7; cada repetição executa a operação o número de vezes necessário para durar ≥ ~100 ms (coluna `iteracoes_por_repeticao` do CSV).

## Montagem do texto de INSERT em lote (somente CPU, sem servidor)

| Linhas | Mediana (µs) | Média ± dp (µs) | ns/linha | Caracteres | Bytes alocados/op |
|---:|---:|---:|---:|---:|---:|
| 1000 | 906.1 | 911.5 ± 36.3 | 906.1 | 49832 | 473470 |
| 5000 | 4613.1 | 4636.7 ± 121.1 | 922.6 | 253340 | 2396957 |

## Alocações e coletas de lixo

Coletas por 1.000 operações (gen0 / gen1 / gen2) e bytes alocados por operação, medidos em passada separada com `AppDomain.MonitoringIsEnabled`. O tamanho teórico de `new double[n,n]` é ≈ 8·N² + ~40 bytes; acima de 85.000 bytes (N ≳ 103) o array vai para o LOH (heap de objetos grandes), cuja coleta é gen2.

| N | Tick: bytes/tick | Tick: GC 0/1/2 por 1000 ticks | Equilíbrio (completo): bytes/chamada | Equilíbrio: GC 0/1/2 por 1000 | Snapshot novo: bytes/op | Temperaturas finitas |
|---:|---:|---|---:|---|---:|:---:|
| 5 | 241 | 0.04 / 0.00 / 0.00 | 241 | 0.02 / 0.00 / 0.00 | 714 | sim |
| 10 | 843 | 0.10 / 0.00 / 0.00 | 843 | 0.07 / 0.00 / 0.00 | 2520 | sim |
| 15 | 1847 | 0.21 / 0.00 / 0.00 | 1846 | 0.20 / 0.00 / 0.00 | 5529 | sim |
| 20 | 3251 | 0.36 / 0.00 / 0.00 | 3251 | 0.30 / 0.00 / 0.00 | 9744 | sim |
| 30 | 7269 | 0.79 / 0.00 / 0.00 | 7266 | 1.03 / 0.00 / 0.00 | 21795 | sim |
| 60 | 28864 | 3.13 / 0.00 / 0.00 | 28864 | 4.46 / 0.00 / 0.00 | 86657 | sim |
| 100 | 80064 | 12.35 / 0.00 / 0.00 | 80064 | 11.11 / 0.00 / 0.00 | 240456 | sim |
| 200 | 320040 | 300.00 / 300.00 / 50.00 | 320040 | 45.45 / 45.45 / 45.45 | 961148 | sim |
| 400 | 1280040 | 0.00 / 0.00 / 0.00 | 1280040 | 0.00 / 0.00 / 0.00 | 3841150 | sim |

## Capacidade em tempo real (cálculo sobre as medições)

Valores reais do código: `TicksPorFrameBase` = 20 (MainForm.cs:449); velocidade 1x–10x (TrackBar); Timer a 50 ms (Designer); 1 chamada de `AtingiuEquilibrio()` por frame (MainForm.cs:490). Pior caso da UI = 200 ticks por frame = 4000 ticks/s.
Custo do frame = ticks_por_frame × Tick() + 1 × AtingiuEquilibrio() (varredura completa, caso pessimista). **Não inclui** `AtualizarTendencias`, `Invalidate`/`Paint` (GDI+) nem o próprio Timer do Windows, que não foram medidos; portanto a folga real da UI é MENOR que a mostrada.

| N | Ticks/s sustentados pelo motor | Frame 1x (ms) | Frame 5x (ms) | Frame 10x (ms) | % do orçamento de 50 ms a 10x | Acompanha 10x? |
|---:|---:|---:|---:|---:|---:|:---:|
| 5 | 469205 | 0.045 | 0.216 | 0.429 | 0.9% | sim |
| 10 | 105701 | 0.200 | 0.957 | 1.903 | 3.8% | sim |
| 15 | 46328 | 0.458 | 2.184 | 4.343 | 8.7% | sim |
| 20 | 25715 | 0.825 | 3.936 | 7.824 | 15.6% | sim |
| 30 | 11151 | 1.899 | 9.073 | 18.041 | 36.1% | sim |
| 60 | 2631 | 8.051 | 38.453 | 76.455 | 152.9% | NÃO |
| 100 | 935 | 22.732 | 108.277 | 215.207 | 430.4% | NÃO |
| 200 | 225 | 94.762 | 451.100 | 896.522 | 1793.0% | NÃO |
| 400 | 53 | 401.384 | 1917.803 | 3813.326 | 7626.7% | NÃO |

**Estimativa** (custo ≈ proporcional a N²; ≈ 22413.0 ns por célula no frame a 10x, mediana dos N ≥ 60): o motor sozinho deixa de caber nos 50 ms a 10x perto de **N ≈ 47**. A UI hoje limita N a 20 (numTamanho.Maximum). Com o desenho + GDI+ (não medidos) o limite prático é menor.

## Fatia do custo por tick (cálculo sobre as medições)

Custo amortizado por tick = Tick() + AtingiuEquilibrio()/(ticks entre chamadas) + Snapshot/100 + montagem do INSERT/100 (INSERT = N² linhas × ns/linha medido com 5.000 linhas). Cenários: **1x** (20 ticks entre chamadas de equilíbrio), **10x** (200) e **cada tick** (se o equilíbrio fosse verificado a todo tick, como o laço antigo).

| N | Cenário | Tick() | Equilíbrio | Snapshot | INSERT | Total (µs/tick) | Domina |
|---:|---|---:|---:|---:|---:|---:|---|
| 5 | 1x | 85.7% | 5.0% | 0.1% | 9.3% | 2.488 | Tick() |
| 5 | 10x | 89.7% | 0.5% | 0.1% | 9.7% | 2.376 | Tick() |
| 5 | cada tick | 43.9% | 51.3% | 0.0% | 4.8% | 4.854 | Equilíbrio |
| 10 | 1x | 86.5% | 5.0% | 0.1% | 8.4% | 10.9 | Tick() |
| 10 | 10x | 90.6% | 0.5% | 0.1% | 8.8% | 10.4 | Tick() |
| 10 | cada tick | 44.1% | 51.5% | 0.0% | 4.3% | 21.4 | Equilíbrio |
| 15 | 1x | 86.4% | 5.2% | 0.1% | 8.3% | 25.0 | Tick() |
| 15 | 10x | 90.7% | 0.5% | 0.1% | 8.7% | 23.8 | Tick() |
| 15 | cada tick | 43.6% | 52.2% | 0.0% | 4.2% | 49.5 | Equilíbrio |
| 20 | 1x | 86.5% | 5.2% | 0.1% | 8.2% | 45.0 | Tick() |
| 20 | 10x | 90.8% | 0.5% | 0.1% | 8.6% | 42.8 | Tick() |
| 20 | cada tick | 43.4% | 52.4% | 0.0% | 4.1% | 89.5 | Equilíbrio |
| 30 | 1x | 86.8% | 5.1% | 0.1% | 8.0% | 103.3 | Tick() |
| 30 | 10x | 91.0% | 0.5% | 0.1% | 8.4% | 98.6 | Tick() |
| 30 | cada tick | 44.1% | 51.8% | 0.0% | 4.1% | 203.5 | Equilíbrio |
| 60 | 1x | 87.2% | 5.2% | 0.0% | 7.6% | 435.9 | Tick() |
| 60 | 10x | 91.4% | 0.5% | 0.0% | 8.0% | 415.7 | Tick() |
| 60 | cada tick | 44.0% | 52.1% | 0.0% | 3.8% | 863.7 | Equilíbrio |
| 100 | 1x | 87.0% | 5.5% | 0.0% | 7.5% | 1229.5 | Tick() |
| 100 | 10x | 91.5% | 0.6% | 0.1% | 7.9% | 1168.9 | Tick() |
| 100 | cada tick | 42.6% | 53.7% | 0.0% | 3.7% | 2508.3 | Equilíbrio |
| 200 | 1x | 87.1% | 5.6% | 0.1% | 7.2% | 5114.0 | Tick() |
| 200 | 10x | 91.7% | 0.6% | 0.1% | 7.6% | 4858.5 | Tick() |
| 200 | cada tick | 42.4% | 54.0% | 0.1% | 3.5% | 10508.1 | Equilíbrio |
| 400 | 1x | 87.8% | 5.2% | 0.2% | 6.8% | 21590.4 | Tick() |
| 400 | 10x | 92.1% | 0.5% | 0.2% | 7.2% | 20587.8 | Tick() |
| 400 | cada tick | 44.3% | 52.1% | 0.1% | 3.5% | 42756.2 | Equilíbrio |

## Paralelismo — ponto de cruzamento e Lei de Amdahl (ESTIMATIVAS, não medições)

O desenho proposto precisa de 2 regiões paralelas por tick (fluxo por aresta; depois soma por corpo/atualização). Referência de custo de agendar: `Parallel.For` vazio com N iterações (uma por linha) × 2.

| N | Tick() (µs) | Overhead de 2× Parallel.For vazio (µs) | Tick / overhead | Equilíbrio / overhead |
|---:|---:|---:|---:|---:|
| 5 | 2.131 | 3.581 | 0.60× | 0.70× |
| 10 | 9.461 | 3.786 | 2.50× | 2.92× |
| 15 | 21.6 | 4.697 | 4.60× | 5.50× |
| 20 | 38.9 | 5.670 | 6.86× | 8.28× |
| 30 | 89.7 | 7.590 | 11.82× | 13.89× |
| 60 | 380.0 | 6.409 | 59.30× | 70.25× |
| 100 | 1069.3 | 7.336 | 145.76× | 183.50× |
| 200 | 4454.2 | 8.729 | 510.25× | 650.44× |
| 400 | 18955.2 | 11.7 | 1616.43× | 1899.94× |

- Tick() passa a custar 1× o overhead de agendamento em **N ≈ 6** (interpolação log-log entre os N medidos) — abaixo disso, paralelizar PERDE tempo.
- Tick() passa a custar 4× o overhead de agendamento em **N ≈ 14** (interpolação log-log entre os N medidos) — ali o overhead já é ≤ 25% do trabalho, mínimo razoável para haver ganho.

Modelo: fração paralelizável p = 1 − (parte serial). Parte serial considerada = só `new double[n,n]` (medida), pois o restante é laço sobre células independentes **se** o fluxo for calculado por aresta. Amdahl: S = 1/((1−p)+p/P). Com overhead: T_par = (1−p)·T + p·T/P + 2·Parallel.For(N). P = 8 núcleos físicos / 16 lógicos (só o P físico é usado: hyper-threading não rende 2× e extrapolar para 16 daria ganhos irreais). Não considera largura de banda de memória nem falso compartilhamento de cache, então é um teto otimista.

| N | p (Tick) | Amdahl P=8 | Modelo c/ overhead P=8 | Equilíbrio: modelo c/ overhead P=8 |
|---:|---:|---:|---:|---:|
| 5 | 0.966 | 6.48× | 0.55× | 0.63× |
| 10 | 0.987 | 7.35× | 1.86× | 2.10× |
| 15 | 0.995 | 7.71× | 2.88× | 3.22× |
| 20 | 0.995 | 7.75× | 3.64× | 4.01× |
| 30 | 0.997 | 7.81× | 4.70× | 5.01× |
| 60 | 0.998 | 7.88× | 6.95× | 7.10× |
| 100 | 0.998 | 7.90× | 7.50× | 7.60× |
| 200 | 0.950 | 5.93× | 5.86× | 6.22× |
| 400 | 0.975 | 6.80× | 6.77× | 6.93× |

Valores < 1× no modelo significam que paralelizar daria PERDA. Confirmar só com a implementação real da N2.

