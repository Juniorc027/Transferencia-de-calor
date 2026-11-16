# Simulador de Transferência de Calor

Simulador didático de condução de calor em uma grade **N × N** de cubos, feito em C# / WinForms
(.NET Framework 4.8) para a disciplina **Programação Avançada de Sistemas**.

A **N1** resolve a troca de calor célula a célula na interface. A **N2** acrescenta:

- motor de simulação **paralelo** (`Parallel.For`, sem lock), com resultado idêntico ao sequencial;
- **gravação em MySQL** de cenários, leituras, eventos e metadados de cada execução;
- **Refazer + Conferência**: reexecuta uma execução gravada e compara célula a célula;
- **benchmark** N1 × N2 × banco, com relatório de desempenho.

O relatório completo está em [`Relatorio_Performance_N1_vs_N2.md`](Relatorio_Performance_N1_vs_N2.md).

---

## Requisitos

| Item | Versão / observação |
|---|---|
| Windows | 10 ou 11 |
| .NET Framework | 4.8 (Developer Pack para compilar) |
| Visual Studio | 2022 (17.13+) ou 2026 — a solução usa o formato `.slnx` |
| MSBuild | o do Visual Studio (`dotnet build` não é o caminho recomendado neste projeto legado) |
| MySQL / MariaDB | 8.0+ / 10.3+ — **opcional**, só para gravação e benchmark de banco |
| NuGet | `MySqlConnector` 2.6.2 (restaurado automaticamente no build) |

O simulador **funciona sem banco**. Sem MySQL, a gravação fica indisponível e o rodapé mostra
`Banco: desconectado (...)`, mas a física, a interface e o benchmark de motores rodam normalmente.

---

## Compilar e executar

### Aplicativo (WinForms)

Abra `SimuladorTransferenciaCalor.slnx` no Visual Studio, escolha **Debug** e pressione **F5**.

Pela linha de comando (Developer Command Prompt do Visual Studio):

```bat
msbuild SimuladorTransferenciaCalor.csproj /t:Build /p:Configuration=Debug
bin\Debug\SimuladorTransferenciaCalor.exe
```

### Benchmark (console, projeto separado)

O benchmark **não está na solução**: ele é um projeto à parte (`Benchmark\Benchmark.csproj`) que
reaproveita os mesmos fontes de `Models`, `Estrutura`, `Data`, `Simulation` e `Persistencia` via
"Add as Link" — assim mede exatamente o código do simulador, sem cópia.

```bat
msbuild Benchmark\Benchmark.csproj /t:Build /p:Configuration=Release
Benchmark\bin\Release\BenchmarkSimulador.exe
```

Os resultados são gravados em `Benchmark\` como `resultados_*.csv` e `.md`
(motores, linha de base e banco; use `--saida <pasta>` para mudar).

---

## Banco de dados (opcional)

1. Garanta um servidor MySQL/MariaDB rodando (XAMPP, MySQL Installer ou serviço do Windows).
2. Copie o modelo e preencha usuário/senha:

   ```bat
   copy ConexaoMySql.config.example ConexaoMySql.config
   ```

   ```xml
   <connectionStrings>
     <add name="SimuladorCalor"
          connectionString="Server=localhost;Port=3306;User ID=root;Password=SUA_SENHA;"
          providerName="MySqlConnector" />
   </connectionStrings>
   ```

   O arquivo real é ignorado pelo git (`.gitignore`), então a senha não vai para o repositório.

3. Compile: o `ConexaoMySql.config` é copiado para `bin\Debug` automaticamente.
4. Ao abrir o app, ele **cria o banco `simulador_calor`, as tabelas e as views sozinho**
   (`Persistencia\schema.sql` vai embutido no executável e é idempotente).

Alternativa manual, se quiser criar antes pelo Workbench ou pela linha de comando:

```bat
mysql -u root -p < Persistencia\schema.sql
```

O usuário precisa ter permissão de `CREATE DATABASE`. O benchmark lê a conexão pelo caminho
passado em `--conexao` (padrão: `ConexaoMySql.config` na pasta atual).

---

## Como usar a interface

1. **Criar Grade**: escolha `N` (1–20), material inicial e lado do cubo (0,003–10 m). Todas as
   células começam a 300 K (≈ 27 °C). O cabeçalho acima da grade mostra
   `Grade N×N · Material: ... · lado ... m`.
2. **Selecionar uma célula**: clique na grade. O painel à direita mostra material, temperatura e
   tendência (`subindo`, `caindo`, `estável`).
3. **Trocar material**: escolha o material em "Trocar material do corpo selecionado" (isso **arma o
   pincel**) e clique na célula. Trocar de material devolve a temperatura ao padrão de 300 K.
4. **Aplicar temperatura**: digite o valor em Kelvin e clique em **Aplicar** (célula selecionada)
   ou simplesmente clique na célula com o pincel armado.
5. **Iniciar / Pausar**: o tempo simulado é Δt × ticks, com Δt = 0,01 s. A velocidade (1x–10x)
   aumenta a **quantidade de ticks por frame**, não o Δt — a física de cada passo não muda.
   Ao atingir o equilíbrio, a simulação para e mostra uma mensagem.
6. **Cores**: escala verde → amarelo → laranja → vermelho (verde = frio, vermelho = quente).
   75% da paleta cobre 250–400 K, onde acontecem as trocas do dia a dia; 400–1000 K usa o restante.
7. **Setas**: vermelha para cima = aquecendo; azul escura para baixo = resfriando, com o ΔT do
   último frame. A borda magenta marca a célula selecionada ou sob o mouse.
8. **Gravar no banco**: marque **antes do primeiro Iniciar**. Ao iniciar, o app grava o cenário
   (tick 0) e amostra o estado a cada 100 ticks (1 s simulado), além da pausa, do equilíbrio e do
   último tick. O rodapé mostra `Banco: gravando - N linhas - fila: X`.
9. **Atualizar lista / Refazer**: lista as execuções gravadas. **Refazer** reexecuta o cenário com
   os mesmos eventos e ticks e grava uma nova execução ligada à original; em seguida o rodapé
   mostra a **Conferência** (diferença máxima entre as duas).

---

## Modelo físico

Implementado em `Simulation/` e comentado no código:

- **Fluxo entre vizinhos**: `q = k_min · A_min · ΔT`, sem o Δx (corpos encostados), conforme o
  enunciado. `k_min` é a menor condutividade do par e `A_min` é a face do cubo menor
  (`min(lado)²`).
- **Sentido do fluxo**: definido pelo **calor sensível** `Q = m · c · T` — quem tem maior `Q` cede
  calor. Com materiais diferentes, isso não equivale a "quem tem maior temperatura".
- **Atualização**: `T_novo = (Q_atual ± q · Δt) / (m · c)`, com `m = ρ · V`.
  Todos os fluxos do tick são calculados antes de qualquer temperatura mudar (sem depender da
  ordem de varredura).
- **Equilíbrio**: um par está em equilíbrio quando
  `|Q_a − Q_b| ≤ max(1e-4 · max(Q_a, Q_b), oscilação do par no tick)`.
  A parcela de oscilação é necessária porque `q = k·A·ΔT` não diminui quando os calores sensíveis
  se aproximam com materiais diferentes; sem ela, o critério nunca fecharia.
- **Δt = 0,01 s** e limiar **1e-4** (0,01%) são os mesmos na N1, na N2 e no `Refazer`.

---

## Arquitetura

```
SimuladorTransferenciaCalor.slnx   solução (app)
SimuladorTransferenciaCalor.csproj app WinForms (.NET Framework 4.8)
Program.cs                         ponto de entrada da UI

UI/                                formulário, desenho da grade, cores, controles
Simulation/                        HeatGrid, motor sequencial (N1) e paralelo (N2)
Models/                            Body (cubo) e Material
Data/                              catálogo fixo de materiais (read-only)
Estrutura/                         lista encadeada própria (usada na vizinhança)

Persistencia/                      MySQL, gravação assíncrona, schema, Refazer
  schema.sql                       tabelas + views (embutido no exe)
  GravadorSimulacao.cs             produtor-consumidor (fila limitada + 1 thread)
  SessaoGravacao.cs                regras de amostragem (em ticks)
  ExecutorSimulacao.cs             laço de ticks compartilhado (UI e Refazer)

Benchmark/                         projeto console separado (não está na .slnx)
  BenchMotores.cs                  N1 × N2-seq × N2-par
  BenchBanco.cs                    impacto da gravação
  Verificacao.cs                   equivalência N1 × N2, tick a tick
  VerificacaoGravacao.cs           gravação + Refazer (memória e MySQL)
  DiagnosticoEquilibrio.cs         análise do critério de equilíbrio
```

### Paralelismo em duas fases

O motor paralelo não usa lock porque separa o tick em duas fases com barreira entre elas:

1. **Fase A** — cada thread calcula as arestas das suas linhas e grava o fluxo em arrays próprios
   (`fluxoHorizontal` / `fluxoVertical`), apenas lendo temperaturas.
2. **Fase B** — cada corpo soma as suas arestas e atualiza a própria temperatura.

As somas respeitam a mesma ordem da N1, o que garante resultado **idêntico bit a bit**.
A partir de `N = 15` o paralelo passa a compensar; abaixo disso o fork-join custa mais que o cálculo.

---

## Verificações e benchmark

```bat
:: equivalência N1 x N2 (todos os ticks, 12 casos) — deve imprimir PASSOU, erro 0
BenchmarkSimulador.exe --verificar

:: gravação + Refazer (memória e, se passar --conexao, MySQL de verdade)
BenchmarkSimulador.exe --verificar-gravacao --conexao ConexaoMySql.config

:: impacto do banco (precisa de servidor)
BenchmarkSimulador.exe --banco --conexao ConexaoMySql.config --ns 20,60 --ticks 2000 --gravs 100,10,1

:: leitura do critério de equilíbrio (somente leitura)
BenchmarkSimulador.exe --diagnostico-equilibrio

:: benchmark de motores (padrão), com opções
BenchmarkSimulador.exe --ns 5,10,15,20,30,60,100,200,400 --reps 7 --saida .
```

Resultados de referência (Ryzen 7 5700G, 8 núcleos / 16 threads):

| Métrica | Resultado |
|---|---|
| Equivalência N1 × N2 | **erro máximo absoluto = 0** (12 casos, até N=60 × 5.000 ticks) |
| Ganho do paralelo em N=400 | **6,2×** sobre a N2 sequencial (16 threads) |
| Ganho em N=10 | **0,76–0,83×** (paralelo perde; o benchmark documenta a perda) |
| Alocação por tick | N1: 241 B a 1,28 MB · N2-seq: **0 B** |
| Refazer (execução gravada) | **0 K** de diferença em 3.150 pontos |
| Vazão de gravação | ~30–50 mil linhas/s (localhost, configuração padrão) |

Os números completos, a metodologia e as limitações estão no
[relatório de performance](Relatorio_Performance_N1_vs_N2.md).

---

## Limitações conhecidas

- A tabela `transferencias` existe no schema, mas **não é preenchida** (era opcional).
- A tela de **Reproduzir** (assistir a uma execução gravada) não foi implementada.
- O teste automatizado cobre a lógica (`--verificar`, `--verificar-gravacao`), **não os cliques da
  interface**.
- O benchmark foi medido em **uma máquina**; o ponto de cruzamento (N≈15) depende do número de
  núcleos e deve ser remedido no PC da apresentação.
- O banco foi medido em servidor **local**, padrão, no mesmo PC — os valores têm incerteza maior
  que os de CPU.
- Janela com layout fixo (a grade não acompanha o redimensionamento) e limite de N = 20 na UI.

---

## Licença / uso

Trabalho acadêmico da disciplina Programação Avançada de Sistemas. Uso livre para fins didáticos.
