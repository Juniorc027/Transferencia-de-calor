-- =====================================================================
-- schema.sql - Simulador de Transferencia de Calor (N2)
-- Compativel com MySQL 5.7+ / 8.x e MariaDB 10.3+ (XAMPP usa MariaDB).
-- Idempotente: pode ser executado varias vezes sem apagar dados.
--
-- Ideia central: separar CENARIO (como a grade foi montada) de EXECUCAO
-- (uma rodada daquele cenario). O mesmo cenario pode ser rodado em modo
-- sequencial (N1) e paralelo (N2) e as execucoes ficam lado a lado.
-- =====================================================================

CREATE DATABASE IF NOT EXISTS simulador_calor
  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE simulador_calor;

-- Fotografia do catalogo: se k/densidade/c mudarem no codigo, uma simulacao
-- antiga continua apontando para os valores com que foi executada.
CREATE TABLE IF NOT EXISTS materiais (
  id               SMALLINT UNSIGNED NOT NULL AUTO_INCREMENT,
  nome             VARCHAR(40)  NOT NULL,
  k                DOUBLE       NOT NULL,  -- W/(m.K)
  densidade        DOUBLE       NOT NULL,  -- kg/m3
  calor_especifico DOUBLE       NOT NULL,  -- J/(kg.K)
  PRIMARY KEY (id),
  UNIQUE KEY uq_material (nome, k, densidade, calor_especifico)
) ENGINE=InnoDB;

-- Cenario = a grade como estava no momento do clique em "Iniciar" (tick 0).
CREATE TABLE IF NOT EXISTS cenarios (
  id        INT UNSIGNED      NOT NULL AUTO_INCREMENT,
  nome      VARCHAR(80)       NOT NULL,
  n         SMALLINT UNSIGNED NOT NULL,   -- grade n x n
  criado_em DATETIME          NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id)
) ENGINE=InnoDB;

-- Uma linha por corpo. Sem celulas vazias: material_id e obrigatorio.
CREATE TABLE IF NOT EXISTS cenario_celulas (
  cenario_id          INT UNSIGNED      NOT NULL,
  linha               SMALLINT UNSIGNED NOT NULL,
  coluna              SMALLINT UNSIGNED NOT NULL,
  material_id         SMALLINT UNSIGNED NOT NULL,
  lado_cubo           DOUBLE            NOT NULL,  -- m
  temperatura_inicial DOUBLE            NOT NULL,  -- K
  PRIMARY KEY (cenario_id, linha, coluna),
  CONSTRAINT fk_cc_cenario  FOREIGN KEY (cenario_id)  REFERENCES cenarios(id) ON DELETE CASCADE,
  CONSTRAINT fk_cc_material FOREIGN KEY (material_id) REFERENCES materiais(id)
) ENGINE=InnoDB;

-- Execucao = uma rodada de um cenario. execucao_origem_id preenchido
-- indica um "Refazer" (usado para conferir se o resultado e identico).
CREATE TABLE IF NOT EXISTS execucoes (
  id                 INT UNSIGNED NOT NULL AUTO_INCREMENT,
  cenario_id         INT UNSIGNED NOT NULL,
  execucao_origem_id INT UNSIGNED NULL,
  modo               ENUM('sequencial','paralelo') NOT NULL,
  threads            TINYINT UNSIGNED NOT NULL DEFAULT 1,
  delta_t            DOUBLE       NOT NULL,           -- s por tick
  limiar_equilibrio  DOUBLE       NOT NULL,
  grav_a_cada_n      INT UNSIGNED NOT NULL DEFAULT 100, -- amostragem em TICKS (100 x 0,01 s = 1 s simulado)
  inicio             DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  fim                DATETIME     NULL,               -- NULL = execucao interrompida/incompleta
  ticks              BIGINT UNSIGNED NOT NULL DEFAULT 0, -- tick final exato (o Refazer roda ate aqui)
  atingiu_equilibrio TINYINT(1)   NOT NULL DEFAULT 0,
  ms_calculo         BIGINT UNSIGNED NULL,            -- para o relatorio de performance
  ms_gravacao        BIGINT UNSIGNED NULL,
  PRIMARY KEY (id),
  KEY ix_exec_cenario (cenario_id),
  CONSTRAINT fk_ex_cenario FOREIGN KEY (cenario_id) REFERENCES cenarios(id) ON DELETE CASCADE,
  CONSTRAINT fk_ex_origem  FOREIGN KEY (execucao_origem_id) REFERENCES execucoes(id) ON DELETE SET NULL
) ENGINE=InnoDB;

-- Estado de TODOS os corpos nos ticks gravados (0, multiplos de grav_a_cada_n,
-- pausa e ultimo tick). Chave composta no lugar de id: economiza espaco e a
-- ordem de insercao (tick crescente) combina com o indice agrupado do InnoDB.
CREATE TABLE IF NOT EXISTS leituras (
  execucao_id    INT UNSIGNED      NOT NULL,
  tick           BIGINT UNSIGNED   NOT NULL,
  linha          SMALLINT UNSIGNED NOT NULL,
  coluna         SMALLINT UNSIGNED NOT NULL,
  material_id    SMALLINT UNSIGNED NOT NULL,  -- pode mudar no meio da execucao
  temperatura    DOUBLE            NOT NULL,  -- K
  calor_sensivel DOUBLE            NOT NULL,  -- J (m.c.T)
  PRIMARY KEY (execucao_id, tick, linha, coluna),
  CONSTRAINT fk_le_exec FOREIGN KEY (execucao_id) REFERENCES execucoes(id) ON DELETE CASCADE,
  CONSTRAINT fk_le_mat  FOREIGN KEY (material_id) REFERENCES materiais(id)
) ENGINE=InnoDB;

-- Log da fisica: quanto calor passou de um corpo (origem = quem cedeu)
-- para outro. energia_acumulada_j soma o que foi trocado desde a ultima
-- gravacao, para nao perder as trocas entre as amostras.
CREATE TABLE IF NOT EXISTS transferencias (
  execucao_id         INT UNSIGNED      NOT NULL,
  tick                BIGINT UNSIGNED   NOT NULL,
  linha_orig          SMALLINT UNSIGNED NOT NULL,
  coluna_orig         SMALLINT UNSIGNED NOT NULL,
  linha_dest          SMALLINT UNSIGNED NOT NULL,
  coluna_dest         SMALLINT UNSIGNED NOT NULL,
  fluxo_w             DOUBLE            NOT NULL,  -- taxa q no tick gravado
  energia_j           DOUBLE            NOT NULL,  -- q x delta_t naquele tick
  energia_acumulada_j DOUBLE            NOT NULL,  -- desde a gravacao anterior
  PRIMARY KEY (execucao_id, tick, linha_orig, coluna_orig, linha_dest, coluna_dest),
  CONSTRAINT fk_tr_exec FOREIGN KEY (execucao_id) REFERENCES execucoes(id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- Acoes do usuario DEPOIS do Iniciar (trocar material, aplicar temperatura),
-- com o tick exato. O "Refazer" reaplica cada uma no mesmo tick; a ordem
-- dentro do mesmo tick e dada pelo id.
CREATE TABLE IF NOT EXISTS eventos (
  id          BIGINT UNSIGNED   NOT NULL AUTO_INCREMENT,
  execucao_id INT UNSIGNED      NOT NULL,
  tick        BIGINT UNSIGNED   NOT NULL,
  tipo        ENUM('trocou_material','aplicou_temperatura') NOT NULL,
  linha       SMALLINT UNSIGNED NOT NULL,
  coluna      SMALLINT UNSIGNED NOT NULL,
  material_id SMALLINT UNSIGNED NULL,
  temperatura DOUBLE            NULL,
  PRIMARY KEY (id),
  KEY ix_ev_exec_tick (execucao_id, tick),
  CONSTRAINT fk_ev_exec FOREIGN KEY (execucao_id) REFERENCES execucoes(id) ON DELETE CASCADE,
  CONSTRAINT fk_ev_mat  FOREIGN KEY (material_id) REFERENCES materiais(id)
) ENGINE=InnoDB;

-- ---------------------------------------------------------------------
-- Views para a demonstracao
-- ---------------------------------------------------------------------

-- Energia total e faixa de temperatura por tick gravado (conservacao de energia).
CREATE OR REPLACE VIEW v_energia_por_tick AS
SELECT execucao_id, tick,
       SUM(calor_sensivel) AS energia_total_j,
       MIN(temperatura)    AS temp_min_k,
       MAX(temperatura)    AS temp_max_k,
       AVG(temperatura)    AS temp_media_k
FROM leituras
GROUP BY execucao_id, tick;

-- Conferencia: compara cada execucao refeita com a original, tick a tick.
-- diferenca_maxima_k esperada: 0 (ou ~1e-12 no modo paralelo).
CREATE OR REPLACE VIEW v_conferencia AS
SELECT e.id AS execucao_refeita,
       e.execucao_origem_id AS execucao_original,
       COUNT(*) AS pontos_comparados,
       MAX(ABS(a.temperatura - b.temperatura)) AS diferenca_maxima_k
FROM execucoes e
JOIN leituras a ON a.execucao_id = e.execucao_origem_id
JOIN leituras b ON b.execucao_id = e.id
               AND b.tick = a.tick AND b.linha = a.linha AND b.coluna = a.coluna
WHERE e.execucao_origem_id IS NOT NULL
GROUP BY e.id, e.execucao_origem_id;

-- Base para o relatorio de performance N1 x N2.
CREATE OR REPLACE VIEW v_performance AS
SELECT x.id AS execucao_id, c.n AS tamanho_grade, x.modo, x.threads, x.ticks,
       x.ms_calculo, x.ms_gravacao,
       ROUND(x.ms_calculo / NULLIF(x.ticks, 0), 6) AS ms_por_tick
FROM execucoes x
JOIN cenarios c ON c.id = x.cenario_id;
