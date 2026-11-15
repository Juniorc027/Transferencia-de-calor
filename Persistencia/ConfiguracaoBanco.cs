using System;
using System.Configuration;
using System.IO;
using System.Xml.Linq;

namespace TransferenciaCalor.Persistencia
{
    /// <summary>
    /// Lê a string de conexão do MySQL. Ela NUNCA fica no código: vem de ConexaoMySql.config,
    /// um arquivo à parte (referenciado pelo App.config via configSource) que NÃO é versionado
    /// no git, porque contém usuário e senha. O repositório traz só o ConexaoMySql.config.example.
    /// </summary>
    public static class ConfiguracaoBanco
    {
        private const string NomeConexao = "SimuladorCalor";

        /// <summary>
        /// Lê pelo mecanismo padrão do .NET (App.config -> configSource). Lança
        /// InvalidOperationException com mensagem clara se o arquivo não existir, para a UI
        /// mostrar "sem configuração" em vez de quebrar: o app deve funcionar sem banco.
        /// </summary>
        public static string ObterStringConexao()
        {
            try
            {
                ConnectionStringSettings cs = ConfigurationManager.ConnectionStrings[NomeConexao];
                if (cs == null || string.IsNullOrWhiteSpace(cs.ConnectionString))
                    throw new InvalidOperationException("Conexão '" + NomeConexao + "' não encontrada. Copie ConexaoMySql.config.example para ConexaoMySql.config e preencha usuário e senha.");
                return cs.ConnectionString;
            }
            catch (ConfigurationErrorsException ex)
            {
                throw new InvalidOperationException("Arquivo ConexaoMySql.config ausente ou inválido (copie o .example e preencha).", ex);
            }
        }

        /// <summary>Lê direto de um arquivo no formato do ConexaoMySql.config (usado pelo Benchmark, que é console).</summary>
        public static string LerDeArquivo(string caminho)
        {
            if (!File.Exists(caminho))
                throw new InvalidOperationException("Arquivo de conexão não encontrado: " + caminho);

            XDocument documento = XDocument.Load(caminho);
            foreach (XElement item in documento.Root.Elements("add"))
            {
                if ((string)item.Attribute("name") == NomeConexao)
                    return (string)item.Attribute("connectionString");
            }
            throw new InvalidOperationException("Conexão '" + NomeConexao + "' não encontrada em " + caminho);
        }
    }
}
