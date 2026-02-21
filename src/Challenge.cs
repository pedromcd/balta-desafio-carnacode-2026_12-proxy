using System;
using System.Collections.Generic;
using System.Threading;

namespace DesignPatternChallenge
{
    // ============================
    // 1) MODELOS
    // ============================
    public class ConfidentialDocument
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public int SecurityLevel { get; set; }
        public long SizeInBytes { get; set; }

        public ConfidentialDocument(string id, string title, string content, int securityLevel)
        {
            Id = id;
            Title = title;
            Content = content;
            SecurityLevel = securityLevel;
            SizeInBytes = content.Length * 2;
        }
    }

    public class User
    {
        public string Username { get; set; }
        public int ClearanceLevel { get; set; }

        public User(string username, int clearanceLevel)
        {
            Username = username;
            ClearanceLevel = clearanceLevel;
        }
    }

    // ============================
    // 2) SUBJECT (contrato comum)
    // ============================
    public interface IDocumentRepository
    {
        ConfidentialDocument GetDocument(string documentId);
        void UpdateDocument(string documentId, string newContent);
    }

    // ============================
    // 3) REAL SUBJECT (recurso custoso)
    // ============================
    public class DocumentRepository : IDocumentRepository
    {
        private Dictionary<string, ConfidentialDocument> _database;

        public DocumentRepository()
        {
            Console.WriteLine("[Repository] Inicializando conexão com banco de dados...");
            Thread.Sleep(1000);

            _database = new Dictionary<string, ConfidentialDocument>
            {
                ["DOC001"] = new ConfidentialDocument(
                    "DOC001",
                    "Relatório Financeiro Q4",
                    "Conteúdo confidencial do relatório financeiro... (10 MB)",
                    3
                ),
                ["DOC002"] = new ConfidentialDocument(
                    "DOC002",
                    "Estratégia de Mercado 2025",
                    "Planos estratégicos confidenciais... (50 MB)",
                    5
                ),
                ["DOC003"] = new ConfidentialDocument(
                    "DOC003",
                    "Manual do Funcionário",
                    "Políticas e procedimentos... (2 MB)",
                    1
                )
            };
        }

        public ConfidentialDocument GetDocument(string documentId)
        {
            Console.WriteLine($"[Repository] Carregando documento {documentId} do banco...");
            Thread.Sleep(500);

            if (_database.ContainsKey(documentId))
            {
                var doc = _database[documentId];
                Console.WriteLine($"[Repository] Documento carregado: {doc.SizeInBytes / (1024 * 1024)} MB");
                return doc;
            }
            return null;
        }

        public void UpdateDocument(string documentId, string newContent)
        {
            Console.WriteLine($"[Repository] Atualizando documento {documentId}...");
            Thread.Sleep(300);

            if (_database.ContainsKey(documentId))
            {
                _database[documentId].Content = newContent;
            }
        }
    }

    // ============================
    // 4) PROXY (segurança + cache + auditoria + lazy)
    // ============================
    public class DocumentRepositoryProxy
    {
        private IDocumentRepository _realRepository; // lazy
        private readonly Dictionary<string, ConfidentialDocument> _cache = new();
        private readonly List<string> _auditLog = new();

        private IDocumentRepository Real
        {
            get
            {
                if (_realRepository == null)
                    _realRepository = new DocumentRepository(); // criado só quando necessário
                return _realRepository;
            }
        }

        public ConfidentialDocument ViewDocument(string documentId, User user)
        {
            Audit($"{user.Username} tentou VISUALIZAR {documentId}");

            // Cache primeiro
            if (_cache.TryGetValue(documentId, out var cached))
            {
                Console.WriteLine($"[Cache] Documento {documentId} encontrado no cache");
                return AuthorizeAndReturn(cached, user);
            }

            // Lazy load: cria repo e busca quando precisa
            var doc = Real.GetDocument(documentId);

            if (doc == null)
            {
                Console.WriteLine($"❌ Documento {documentId} não encontrado");
                Audit($"DOCUMENTO NÃO ENCONTRADO {documentId}");
                return null;
            }

            // guarda no cache
            _cache[documentId] = doc;

            return AuthorizeAndReturn(doc, user);
        }

        public bool EditDocument(string documentId, User user, string newContent)
        {
            Audit($"{user.Username} tentou EDITAR {documentId}");

            // Busca (cache ou repo)
            var doc = _cache.TryGetValue(documentId, out var cached)
                ? cached
                : Real.GetDocument(documentId);

            if (doc == null)
            {
                Console.WriteLine("❌ Documento não encontrado");
                Audit($"EDIT NEGADO - DOC NÃO ENCONTRADO {documentId}");
                return false;
            }

            if (!HasAccess(user, doc))
            {
                Console.WriteLine($"❌ Operação não autorizada (nível {user.ClearanceLevel} < {doc.SecurityLevel})");
                Audit($"EDIT NEGADO para {user.Username} em {documentId}");
                return false;
            }

            Real.UpdateDocument(documentId, newContent);

            // invalida cache do documento atualizado
            if (_cache.ContainsKey(documentId))
                _cache.Remove(documentId);

            Console.WriteLine("✅ Documento atualizado");
            Audit($"EDIT OK por {user.Username} em {documentId}");
            return true;
        }

        public void ShowAuditLog()
        {
            Console.WriteLine("\n=== Log de Auditoria ===");
            foreach (var entry in _auditLog)
                Console.WriteLine(entry);
        }

        private ConfidentialDocument AuthorizeAndReturn(ConfidentialDocument doc, User user)
        {
            if (!HasAccess(user, doc))
            {
                Console.WriteLine($"❌ Acesso negado! Nível {user.ClearanceLevel} < Requerido {doc.SecurityLevel}");
                Audit($"ACESSO NEGADO para {user.Username} em {doc.Id}");
                return null;
            }

            Console.WriteLine($"✅ Acesso permitido ao documento: {doc.Title}");
            Audit($"ACESSO OK para {user.Username} em {doc.Id}");
            return doc;
        }

        private static bool HasAccess(User user, ConfidentialDocument doc)
            => user.ClearanceLevel >= doc.SecurityLevel;

        private void Audit(string message)
        {
            var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _auditLog.Add(entry);
            Console.WriteLine($"[Audit] {entry}");
        }
    }

    // ============================
    // 5) DEMO
    // ============================
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Sistema de Documentos Confidenciais (Proxy) ===\n");

            // Agora não cria repo imediatamente (lazy)
            var proxy = new DocumentRepositoryProxy();

            var manager = new User("joao.silva", 5);
            var employee = new User("maria.santos", 2);

            Console.WriteLine("\n--- Gerente acessando documento de alto nível ---");
            proxy.ViewDocument("DOC002", manager);

            Console.WriteLine("\n--- Funcionário tentando acessar mesmo documento ---");
            proxy.ViewDocument("DOC002", employee);

            Console.WriteLine("\n--- Gerente acessando novamente (cache) ---");
            proxy.ViewDocument("DOC002", manager);

            Console.WriteLine("\n--- Funcionário acessando documento permitido ---");
            proxy.ViewDocument("DOC003", employee);

            Console.WriteLine("\n--- Gerente editando documento ---");
            proxy.EditDocument("DOC003", manager, "Novo conteúdo atualizado...");

            proxy.ShowAuditLog();

            Console.WriteLine("\n✅ Proxy resolveu:");
            Console.WriteLine("• Controle de acesso centralizado");
            Console.WriteLine("• Cache automático");
            Console.WriteLine("• Auditoria centralizada");
            Console.WriteLine("• Lazy loading do repositório");
        }
    }
}