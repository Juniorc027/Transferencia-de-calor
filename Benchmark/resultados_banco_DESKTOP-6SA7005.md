# Benchmark com banco de dados — DESKTOP-6SA7005

Servidor: **MySQL 8.0.46 Community, instancia local descartavel (porta 3399, configuracao padrao, sem tuning)**. 2000 ticks por rodada; medianas das repetições; Release, sem debugger.
Cenários: A = N1 sem banco; B = N2 paralelo sem banco; C = N1 com gravação; D = N2 paralelo com gravação.

Ambiente: cpu = AMD Ryzen 7 5700G with Radeon Graphics; nucleos_fisicos = 8; nucleos_logicos = 16; ram = 15.4 GB; windows = Microsoft Windows 11 Pro (10.0.26200); dotnet_framework = 4.8.1 ou superior (Release=533509); configuracao = Release; plataforma = x64 (processo 64 bits; Prefer 32-bit desligado)

## Resultados (mediana, ms)

`simulação` = tempo até o último tick (o que o usuário sente); `esvaziar` = espera extra para o banco terminar; `total` = simulação + esvaziar.

| N | grav a cada | Cenário | simulação | esvaziar fila | total | cálculo (Tick) | linhas | linhas/s | fila máx | produtor bloqueado | ms gravação (consumidor) |
|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 20 | — | A | 82.9 | 0.0 | 82.9 | 82.9 | 0 | 0 | 0 | 0.0 | 0 |
| 20 | — | B | 52.3 | 0.0 | 52.3 | 52.3 | 0 | 0 | 0 | 0.0 | 0 |
| 20 | 1 | C | 22354.6 | 3102.8 | 25457.4 | 110.4 | 800400 | 31441 | 256 | 22211.2 | 25446 |
| 20 | 1 | D | 22399.8 | 3254.4 | 25654.2 | 96.2 | 800400 | 31200 | 256 | 22242.6 | 25645 |
| 20 | 10 | C | 104.8 | 2124.1 | 2229.6 | 103.8 | 80400 | 36060 | 193 | 0.0 | 2229 |
| 20 | 10 | D | 53.3 | 2288.5 | 2341.2 | 51.5 | 80400 | 34342 | 203 | 0.0 | 2340 |
| 20 | 100 | C | 100.3 | 139.2 | 239.7 | 100.1 | 8400 | 35051 | 14 | 0.0 | 239 |
| 20 | 100 | D | 58.5 | 239.0 | 299.0 | 58.3 | 8400 | 28091 | 18 | 0.0 | 298 |
| 60 | — | A | 968.4 | 0.0 | 968.4 | 968.4 | 0 | 0 | 0 | 0.0 | 0 |
| 60 | — | B | 175.5 | 0.0 | 175.5 | 175.5 | 0 | 0 | 0 | 0.0 | 0 |
| 60 | 10 | C | 1023.5 | 13014.1 | 14011.2 | 1010.8 | 723600 | 51644 | 188 | 0.0 | 14009 |
| 60 | 10 | D | 203.5 | 13920.5 | 14121.9 | 187.4 | 723600 | 51240 | 203 | 0.0 | 14120 |
| 60 | 100 | C | 982.5 | 1251.7 | 2234.2 | 981.5 | 75600 | 33837 | 14 | 0.0 | 2233 |
| 60 | 100 | D | 175.5 | 2384.4 | 2551.9 | 174.7 | 75600 | 29625 | 23 | 0.0 | 2551 |

## Impacto da gravação sobre a simulação paralela (D contra B)

| N | grav a cada | B: simulação (ms) | D: simulação (ms) | acréscimo no que o usuário sente | D: total até gravar tudo (ms) | acréscimo total |
|---:|---:|---:|---:|---:|---:|---:|
| 20 | 1 | 52.3 | 22399.8 | 42763.8% | 25654.2 | 48991.4% |
| 20 | 10 | 52.3 | 53.3 | 1.9% | 2341.2 | 4380.0% |
| 20 | 100 | 52.3 | 58.5 | 12.0% | 299.0 | 472.2% |
| 60 | 10 | 175.5 | 203.5 | 16.0% | 14121.9 | 7946.9% |
| 60 | 100 | 175.5 | 175.5 | 0.0% | 2551.9 | 1354.1% |

## Tamanho no banco

Ao final do benchmark: **10,142,400** linhas em `leituras`; tabelas do simulador ocupam ≈ **798.7 MB** (dados + índices, `information_schema`).

