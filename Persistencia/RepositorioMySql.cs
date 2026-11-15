using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using MySqlConnector;
using TransferenciaCalor.Models;

namespace TransferenciaCalor.Persistencia
{
    /// <summary>
    /// Implementação da persistência em MySQL / MariaDB (driver MySqlConnector).
    ///
    /// Regras de segurança e portabilidade:
    ///  - Textos vindos do usuário/catálogo (nome de cenário, nome de material) vão SEMPRE como
    ///    parâmetros, nunca concatenados no SQL.
    ///  - Valores numéricos dos lotes (inteiros e doubles gerados pelo programa) vão no texto
    ///    do INSERT para montar milhares de linhas em um único comando (muito mais rápido que
    ///    um comando por linha). Os doubles usam "G17" + InvariantCulture: 17 dígitos
    ///    significativos garantem que o valor lido de volta é BIT A BIT o mesmo (o "Refazer"
    ///    compara com 0 de tolerância), e a cultura invariante evita "300,5" (vírgula) em pt-BR.
    /// </summary>
    public sealed class RepositorioMySql : IRepositorioSimulacao
    {
        private const string NomeBanco = "simulador_calor";

        // Linhas por comando INSERT: algumas centenas a poucos milhares ficam bem abaixo
        // do max_allowed_packet (4 MB no MySQL 5.7; ~100 bytes por linha => ~100 KB aqui).
        private const int LinhasPorComando = 1000;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly string conexaoServidor; // sem banco escolhido: usada para criar o banco
        private readonly string conexaoBanco;

        // Material (objeto) -> id no banco. A chave é a REFERÊNCIA: o catálogo tem objetos fixos,
        // então depois da primeira gravação não há mais consulta ao banco para materiais.
        private readonly Dictionary<Material, long> cacheMateriais = new Dictionary<Material, long>();
        private readonly object travaCache = new object();

        public RepositorioMySql(string stringConexao)
        {
            if (string.IsNullOrWhiteSpace(stringConexao))
                throw new ArgumentException("String de conexão vazia.", "stringConexao");

            MySqlConnectionStringBuilder b = new MySqlConnectionStringBuilder(stringConexao);
            b.Database = "";
            b.ConnectionTimeout = 5;           // servidor fora do ar: falha rápido, sem prender a gravação por muito tempo
            b.DefaultCommandTimeout = 120;
            conexaoServidor = b.ConnectionString;
            b.Database = NomeBanco;
            conexaoBanco = b.ConnectionString;
        }

        // ---------------------------------------------------------------- esquema

        public void GarantirEsquema()
        {
            string script = LerSchemaEmbutido();
            using (MySqlConnection conexao = new MySqlConnection(conexaoServidor))
            {
                conexao.Open();
                foreach (string comando in DividirComandos(script))
                {
                    using (MySqlCommand cmd = new MySqlCommand(comando, conexao))
                        cmd.ExecuteNonQuery();
                }
            }
        }

        // O schema.sql vai DENTRO do executável (EmbeddedResource): o app não depende de
        // achar o arquivo em alguma pasta no PC do laboratório.
        private static string LerSchemaEmbutido()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream fluxo = assembly.GetManifestResourceStream("schema.sql"))
            {
                if (fluxo == null)
                    throw new InvalidOperationException("Recurso schema.sql não encontrado no executável.");
                using (StreamReader leitor = new StreamReader(fluxo, Encoding.UTF8))
                    return leitor.ReadToEnd();
            }
        }

        // Divide o script em comandos por ';', ignorando comentários "--". O schema não usa
        // procedures/DELIMITER, então esta divisão simples basta (e evita depender do
        // MySqlScript, cujo comportamento varia entre versões do driver).
        internal static IList<string> DividirComandos(string script)
        {
            StringBuilder semComentarios = new StringBuilder();
            foreach (string linha in script.Replace("\r", "").Split('\n'))
            {
                string l = linha;
                int pos = l.IndexOf("--", StringComparison.Ordinal);
                // só corta se o "--" não estiver dentro de um literal 'texto' (aspas em número par antes dele)
                if (pos >= 0 && ContarAspas(l, pos) % 2 == 0)
                    l = l.Substring(0, pos);
                semComentarios.AppendLine(l);
            }

            List<string> comandos = new List<string>();
            foreach (string parte in semComentarios.ToString().Split(';'))
            {
                string c = parte.Trim();
                if (c.Length > 0)
                    comandos.Add(c);
            }
            return comandos;
        }

        private static int ContarAspas(string texto, int ate)
        {
            int total = 0;
            for (int i = 0; i < ate; i++)
                if (texto[i] == '\'') total++;
            return total;
        }

        // ---------------------------------------------------------------- escrita

        public long SalvarCenario(string nome, int tamanho, IList<CelulaCenario> celulas)
        {
            try { return SalvarCenarioInterno(nome, tamanho, celulas); }
            catch { LimparCache(); throw; }
        }

        private long SalvarCenarioInterno(string nome, int tamanho, IList<CelulaCenario> celulas)
        {
            using (MySqlConnection conexao = AbrirBanco())
            using (MySqlTransaction tx = conexao.BeginTransaction())
            {
                long cenarioId;
                using (MySqlCommand cmd = new MySqlCommand("INSERT INTO cenarios (nome, n) VALUES (@nome, @n)", conexao, tx))
                {
                    cmd.Parameters.AddWithValue("@nome", Truncar(nome, 80));
                    cmd.Parameters.AddWithValue("@n", tamanho);
                    cmd.ExecuteNonQuery();
                    cenarioId = cmd.LastInsertedId;
                }

                StringBuilder sb = new StringBuilder();
                int naLinha = 0;
                foreach (CelulaCenario c in celulas)
                {
                    if (naLinha == 0)
                        sb.Append("INSERT INTO cenario_celulas (cenario_id, linha, coluna, material_id, lado_cubo, temperatura_inicial) VALUES ");
                    else
                        sb.Append(',');

                    long materialId = ObterOuCriarMaterial(conexao, tx, c.Material);
                    sb.Append('(').Append(cenarioId.ToString(Inv)).Append(',').Append(c.Linha.ToString(Inv)).Append(',')
                      .Append(c.Coluna.ToString(Inv)).Append(',').Append(materialId.ToString(Inv)).Append(',')
                      .Append(D(c.LadoCubo)).Append(',').Append(D(c.TemperaturaInicial)).Append(')');
                    naLinha++;

                    if (naLinha == LinhasPorComando)
                    {
                        Executar(conexao, tx, sb.ToString());
                        sb.Clear();
                        naLinha = 0;
                    }
                }
                if (naLinha > 0)
                    Executar(conexao, tx, sb.ToString());

                tx.Commit();
                return cenarioId;
            }
        }

        public long CriarExecucao(long cenarioId, DadosExecucao dados)
        {
            using (MySqlConnection conexao = AbrirBanco())
            {
                string sql = "INSERT INTO execucoes (cenario_id, execucao_origem_id, modo, threads, delta_t, limiar_equilibrio, grav_a_cada_n) VALUES (" +
                    cenarioId.ToString(Inv) + "," +
                    (dados.ExecucaoOrigemId.HasValue ? dados.ExecucaoOrigemId.Value.ToString(Inv) : "NULL") + "," +
                    (dados.Modo == ModoExecucao.Paralelo ? "'paralelo'" : "'sequencial'") + "," +
                    Math.Max(1, Math.Min(255, dados.Threads)).ToString(Inv) + "," +
                    D(dados.DeltaT) + "," + D(dados.LimiarEquilibrio) + "," + dados.GravACadaN.ToString(Inv) + ")";
                using (MySqlCommand cmd = new MySqlCommand(sql, conexao))
                {
                    cmd.ExecuteNonQuery();
                    return cmd.LastInsertedId;
                }
            }
        }

        public void GravarLeituras(long execucaoId, IList<SnapshotEstado> lote)
        {
            try { GravarLeiturasInterno(execucaoId, lote); }
            catch { LimparCache(); throw; }
        }

        private void GravarLeiturasInterno(long execucaoId, IList<SnapshotEstado> lote)
        {
            if (lote.Count == 0)
                return;

            using (MySqlConnection conexao = AbrirBanco())
            using (MySqlTransaction tx = conexao.BeginTransaction())
            {
                string execTexto = execucaoId.ToString(Inv);
                StringBuilder sb = new StringBuilder(LinhasPorComando * 100);
                int naLinha = 0;

                foreach (SnapshotEstado s in lote)
                {
                    string tickTexto = s.Tick.ToString(Inv);
                    int n = s.Tamanho;
                    for (int i = 0; i < s.Quantidade; i++)
                    {
                        if (naLinha == 0)
                            sb.Append("INSERT INTO leituras (execucao_id, tick, linha, coluna, material_id, temperatura, calor_sensivel) VALUES ");
                        else
                            sb.Append(',');

                        long materialId = ObterOuCriarMaterial(conexao, tx, s.MaterialDe(i));
                        sb.Append('(').Append(execTexto).Append(',').Append(tickTexto).Append(',')
                          .Append((i / n).ToString(Inv)).Append(',').Append((i % n).ToString(Inv)).Append(',')
                          .Append(materialId.ToString(Inv)).Append(',')
                          .Append(D(s.Temperatura(i))).Append(',').Append(D(s.CalorSensivel(i))).Append(')');
                        naLinha++;

                        if (naLinha == LinhasPorComando)
                        {
                            Executar(conexao, tx, sb.ToString());
                            sb.Clear();
                            naLinha = 0;
                        }
                    }
                }
                if (naLinha > 0)
                    Executar(conexao, tx, sb.ToString());

                tx.Commit();
            }
        }

        public void GravarEventos(long execucaoId, IList<EventoSimulacao> eventos)
        {
            try { GravarEventosInterno(execucaoId, eventos); }
            catch { LimparCache(); throw; }
        }

        private void GravarEventosInterno(long execucaoId, IList<EventoSimulacao> eventos)
        {
            if (eventos.Count == 0)
                return;

            using (MySqlConnection conexao = AbrirBanco())
            using (MySqlTransaction tx = conexao.BeginTransaction())
            {
                StringBuilder sb = new StringBuilder("INSERT INTO eventos (execucao_id, tick, tipo, linha, coluna, material_id, temperatura) VALUES ");
                for (int i = 0; i < eventos.Count; i++)
                {
                    EventoSimulacao e = eventos[i];
                    if (i > 0) sb.Append(',');
                    string material = e.Material == null ? "NULL" : ObterOuCriarMaterial(conexao, tx, e.Material).ToString(Inv);
                    string temperatura = e.Temperatura.HasValue ? D(e.Temperatura.Value) : "NULL";
                    sb.Append('(').Append(execucaoId.ToString(Inv)).Append(',').Append(e.Tick.ToString(Inv)).Append(',')
                      .Append(e.Tipo == TipoEvento.TrocouMaterial ? "'trocou_material'" : "'aplicou_temperatura'").Append(',')
                      .Append(e.Linha.ToString(Inv)).Append(',').Append(e.Coluna.ToString(Inv)).Append(',')
                      .Append(material).Append(',').Append(temperatura).Append(')');
                }
                Executar(conexao, tx, sb.ToString());
                tx.Commit();
            }
        }

        public void FinalizarExecucao(long execucaoId, long ticks, bool atingiuEquilibrio, long msCalculo, long msGravacao)
        {
            using (MySqlConnection conexao = AbrirBanco())
            {
                string sql = "UPDATE execucoes SET fim = NOW(), ticks = " + ticks.ToString(Inv) +
                    ", atingiu_equilibrio = " + (atingiuEquilibrio ? "1" : "0") +
                    ", ms_calculo = " + msCalculo.ToString(Inv) + ", ms_gravacao = " + msGravacao.ToString(Inv) +
                    " WHERE id = " + execucaoId.ToString(Inv);
                using (MySqlCommand cmd = new MySqlCommand(sql, conexao))
                    cmd.ExecuteNonQuery();
            }
        }

        // ---------------------------------------------------------------- leitura

        public IList<ResumoExecucao> ListarExecucoesOriginais()
        {
            List<ResumoExecucao> lista = new List<ResumoExecucao>();
            using (MySqlConnection conexao = AbrirBanco())
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT x.id, x.cenario_id, c.nome, c.n, x.ticks, x.modo, x.fim FROM execucoes x " +
                "JOIN cenarios c ON c.id = x.cenario_id WHERE x.execucao_origem_id IS NULL ORDER BY x.id DESC LIMIT 200", conexao))
            using (MySqlDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    lista.Add(new ResumoExecucao(
                        Convert.ToInt64(r.GetValue(0)), Convert.ToInt64(r.GetValue(1)), r.GetString(2),
                        Convert.ToInt32(r.GetValue(3)), Convert.ToInt64(r.GetValue(4)),
                        r.GetString(5) == "paralelo" ? ModoExecucao.Paralelo : ModoExecucao.Sequencial,
                        !r.IsDBNull(6)));
                }
            }
            return lista;
        }

        public CenarioCarregado CarregarCenario(long cenarioId)
        {
            string nome;
            int tamanho;
            using (MySqlConnection conexao = AbrirBanco())
            {
                using (MySqlCommand cmd = new MySqlCommand("SELECT nome, n FROM cenarios WHERE id = " + cenarioId.ToString(Inv), conexao))
                using (MySqlDataReader r = cmd.ExecuteReader())
                {
                    if (!r.Read())
                        throw new InvalidOperationException("Cenário " + cenarioId + " não encontrado.");
                    nome = r.GetString(0);
                    tamanho = Convert.ToInt32(r.GetValue(1));
                }

                // Um objeto Material por id (como o catálogo: vários corpos compartilham o mesmo objeto)
                Dictionary<long, Material> materiais = new Dictionary<long, Material>();
                List<CelulaCenario> celulas = new List<CelulaCenario>();
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT cc.linha, cc.coluna, m.id, m.nome, m.k, m.densidade, m.calor_especifico, cc.lado_cubo, cc.temperatura_inicial " +
                    "FROM cenario_celulas cc JOIN materiais m ON m.id = cc.material_id " +
                    "WHERE cc.cenario_id = " + cenarioId.ToString(Inv) + " ORDER BY cc.linha, cc.coluna", conexao))
                using (MySqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        long materialId = Convert.ToInt64(r.GetValue(2));
                        Material material;
                        if (!materiais.TryGetValue(materialId, out material))
                        {
                            material = new Material(r.GetString(3), r.GetDouble(4), r.GetDouble(5), r.GetDouble(6));
                            materiais[materialId] = material;
                        }
                        celulas.Add(new CelulaCenario(Convert.ToInt32(r.GetValue(0)), Convert.ToInt32(r.GetValue(1)),
                            material, r.GetDouble(7), r.GetDouble(8)));
                    }
                }
                return new CenarioCarregado(cenarioId, nome, tamanho, celulas);
            }
        }

        public ExecucaoCarregada CarregarExecucao(long execucaoId)
        {
            using (MySqlConnection conexao = AbrirBanco())
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT cenario_id, delta_t, limiar_equilibrio, grav_a_cada_n, ticks, fim FROM execucoes WHERE id = " + execucaoId.ToString(Inv), conexao))
            using (MySqlDataReader r = cmd.ExecuteReader())
            {
                if (!r.Read())
                    throw new InvalidOperationException("Execução " + execucaoId + " não encontrada.");
                return new ExecucaoCarregada(execucaoId, Convert.ToInt64(r.GetValue(0)), r.GetDouble(1), r.GetDouble(2),
                    Convert.ToInt32(r.GetValue(3)), Convert.ToInt64(r.GetValue(4)), !r.IsDBNull(5));
            }
        }

        public IList<EventoSimulacao> CarregarEventos(long execucaoId)
        {
            List<EventoSimulacao> lista = new List<EventoSimulacao>();
            using (MySqlConnection conexao = AbrirBanco())
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT e.tick, e.tipo, e.linha, e.coluna, m.nome, m.k, m.densidade, m.calor_especifico, e.temperatura " +
                "FROM eventos e LEFT JOIN materiais m ON m.id = e.material_id " +
                "WHERE e.execucao_id = " + execucaoId.ToString(Inv) + " ORDER BY e.tick, e.id", conexao))
            using (MySqlDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    Material material = r.IsDBNull(4) ? null : new Material(r.GetString(4), r.GetDouble(5), r.GetDouble(6), r.GetDouble(7));
                    double? temperatura = r.IsDBNull(8) ? (double?)null : r.GetDouble(8);
                    lista.Add(new EventoSimulacao(Convert.ToInt64(r.GetValue(0)),
                        r.GetString(1) == "trocou_material" ? TipoEvento.TrocouMaterial : TipoEvento.AplicouTemperatura,
                        Convert.ToInt32(r.GetValue(2)), Convert.ToInt32(r.GetValue(3)), material, temperatura));
                }
            }
            return lista;
        }

        public ResultadoConferencia Conferir(long execucaoRefeitaId)
        {
            using (MySqlConnection conexao = AbrirBanco())
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT pontos_comparados, diferenca_maxima_k FROM v_conferencia WHERE execucao_refeita = " + execucaoRefeitaId.ToString(Inv), conexao))
            using (MySqlDataReader r = cmd.ExecuteReader())
            {
                if (!r.Read())
                    return new ResultadoConferencia(0, double.NaN);
                return new ResultadoConferencia(Convert.ToInt64(r.GetValue(0)), r.IsDBNull(1) ? double.NaN : Convert.ToDouble(r.GetValue(1)));
            }
        }

        public double TamanhoTabelasMb()
        {
            using (MySqlConnection conexao = AbrirBanco())
            {
                // MySQL 8 guarda em cache as estatísticas do information_schema (por padrão 24 h) e
                // devolveria um tamanho antigo. Zerar a validade vale só nesta sessão; o MariaDB não
                // tem essa variável (e já devolve valor atual), então o erro é ignorado.
                try
                {
                    using (MySqlCommand ajuste = new MySqlCommand("SET SESSION information_schema_stats_expiry = 0", conexao))
                        ajuste.ExecuteNonQuery();
                }
                catch (MySqlException) { }

                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT COALESCE(SUM(data_length + index_length), 0) / 1048576 FROM information_schema.tables WHERE table_schema = '" + NomeBanco + "'", conexao))
                    return Convert.ToDouble(cmd.ExecuteScalar(), Inv);
            }
        }

        public long ContarLeituras()
        {
            using (MySqlConnection conexao = AbrirBanco())
            using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*) FROM leituras", conexao))
                return Convert.ToInt64(cmd.ExecuteScalar(), Inv);
        }

        // ---------------------------------------------------------------- auxiliares

        private MySqlConnection AbrirBanco()
        {
            MySqlConnection conexao = new MySqlConnection(conexaoBanco);
            try { conexao.Open(); }
            catch { conexao.Dispose(); throw; }
            return conexao;
        }

        private static void Executar(MySqlConnection conexao, MySqlTransaction tx, string sql)
        {
            using (MySqlCommand cmd = new MySqlCommand(sql, conexao, tx))
                cmd.ExecuteNonQuery();
        }

        // Devolve o id do material, inserindo-o se ainda não existir. O "ON DUPLICATE KEY UPDATE
        // id = LAST_INSERT_ID(id)" faz LastInsertedId valer também quando a linha já existia.
        private long ObterOuCriarMaterial(MySqlConnection conexao, MySqlTransaction tx, Material material)
        {
            lock (travaCache)
            {
                long id;
                if (cacheMateriais.TryGetValue(material, out id))
                    return id;

                using (MySqlCommand cmd = new MySqlCommand(
                    "INSERT INTO materiais (nome, k, densidade, calor_especifico) VALUES (@nome, @k, @d, @c) " +
                    "ON DUPLICATE KEY UPDATE id = LAST_INSERT_ID(id)", conexao, tx))
                {
                    cmd.Parameters.AddWithValue("@nome", Truncar(material.Nome, 40));
                    cmd.Parameters.AddWithValue("@k", material.K);
                    cmd.Parameters.AddWithValue("@d", material.Densidade);
                    cmd.Parameters.AddWithValue("@c", material.CalorEspecifico);
                    cmd.ExecuteNonQuery();
                    id = cmd.LastInsertedId;
                }
                // O id entra no cache já dentro da transação. Se ela for desfeita (rollback), o
                // material pode ter deixado de existir: por isso quem faz a transação chama
                // LimparCache() em caso de falha (veja os métodos de gravação).
                cacheMateriais[material] = id;
                return id;
            }
        }

        private void LimparCache()
        {
            lock (travaCache)
                cacheMateriais.Clear();
        }

        // double -> texto para o SQL. G17 = ida e volta exata; Invariant = ponto decimal.
        private static string D(double valor)
        {
            if (double.IsNaN(valor) || double.IsInfinity(valor))
                throw new InvalidOperationException("Valor não finito não pode ser gravado no banco (a simulação divergiu).");
            return valor.ToString("G17", Inv);
        }

        private static string Truncar(string texto, int max)
        {
            return texto.Length <= max ? texto : texto.Substring(0, max);
        }
    }
}
