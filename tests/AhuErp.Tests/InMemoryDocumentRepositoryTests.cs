using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    public class InMemoryDocumentRepositoryTests
    {
        [Fact]
        public void Add_assigns_incremental_id_when_missing()
        {
            var repo = new InMemoryDocumentRepository();
            var a = new Document { Type = DocumentType.Incoming, Title = "A" };
            var b = new Document { Type = DocumentType.Internal, Title = "B" };

            repo.Add(a);
            repo.Add(b);

            Assert.Equal(1, a.Id);
            Assert.Equal(2, b.Id);
        }

        [Fact]
        public void ListByType_filters_and_excludes_archive_requests()
        {
            var repo = new InMemoryDocumentRepository();
            repo.Add(new Document { Type = DocumentType.Incoming, Title = "in-1" });
            repo.Add(new Document { Type = DocumentType.Internal, Title = "int-1" });
            repo.Add(new ArchiveRequest { Title = "arch-1" });

            var incoming = repo.ListByType(DocumentType.Incoming);

            Assert.Single(incoming);
            Assert.Equal("in-1", incoming[0].Title);
        }

        [Fact]
        public void ListArchiveRequests_returns_only_archive_subtype()
        {
            var repo = new InMemoryDocumentRepository();
            repo.Add(new Document { Type = DocumentType.Incoming, Title = "reg" });
            repo.Add(new ArchiveRequest { Title = "arch-1" });
            repo.Add(new ArchiveRequest { Title = "arch-2" });

            var archives = repo.ListArchiveRequests();

            Assert.Equal(2, archives.Count);
            Assert.All(archives, a => Assert.IsType<ArchiveRequest>(a));
        }

        [Fact]
        public void Update_replaces_existing_document()
        {
            var repo = new InMemoryDocumentRepository();
            var doc = new Document { Type = DocumentType.Incoming, Title = "old" };
            repo.Add(doc);

            doc.Title = "new";
            repo.Update(doc);

            Assert.Equal("new", repo.GetById(doc.Id).Title);
        }

        [Fact]
        public void Remove_drops_document_by_id()
        {
            var repo = new InMemoryDocumentRepository();
            var doc = new Document { Type = DocumentType.Internal, Title = "x" };
            repo.Add(doc);

            repo.Remove(doc.Id);

            Assert.Null(repo.GetById(doc.Id));
        }

        [Theory]
        [InlineData("СЗ-{YYYY}-{NNNNN}")]
        [InlineData("ИСХ-2026-{00001}")]
        public void Add_rejects_registration_number_templates(string registrationNumber)
        {
            var repo = new InMemoryDocumentRepository();
            var doc = new Document
            {
                Type = DocumentType.Internal,
                Title = "x",
                RegistrationNumber = registrationNumber
            };

            Assert.Throws<System.InvalidOperationException>(() => repo.Add(doc));
        }
    }
}
