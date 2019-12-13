using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using FakeItEasy;
using FileSender.Dependencies;
using FluentAssertions;
using NUnit.Framework;

namespace FileSender
{
    public class FileSender
    {
        private readonly ICryptographer cryptographer;
        private readonly ISender sender;
        private readonly IRecognizer recognizer;

        public FileSender(
            ICryptographer cryptographer,
            ISender sender,
            IRecognizer recognizer)
        {
            this.cryptographer = cryptographer;
            this.sender = sender;
            this.recognizer = recognizer;
        }

        public Result SendFiles(File[] files, X509Certificate certificate)
        {
            return new Result
            {
                SkippedFiles = files
                    .Where(file => !TrySendFile(file, certificate))
                    .ToArray()
            };
        }

        private bool TrySendFile(File file, X509Certificate certificate)
        {
            Document document;
            if (!recognizer.TryRecognize(file, out document))
                return false;
            if (!CheckFormat(document) || !CheckActual(document))
                return false;
            var signedContent = cryptographer.Sign(document.Content, certificate);
            return sender.TrySend(signedContent);
        }

        private bool CheckFormat(Document document)
        {
            return document.Format == "4.0" ||
                   document.Format == "3.1";
        }

        private bool CheckActual(Document document)
        {
            return document.Created.AddMonths(1) > DateTime.Now;
        }

        public class Result
        {
            public File[] SkippedFiles { get; set; }
        }
    }

    //TODO: реализовать недостающие тесты
    [TestFixture]
    public class FileSender_Should
    {
        private FileSender fileSender;
        private ICryptographer cryptographer;
        private ISender sender;
        private IRecognizer recognizer;

        private readonly X509Certificate certificate = new X509Certificate();
        private File file;
        private byte[] signedContent;
        private Document defaultDocument;

        [SetUp]
        public void SetUp()
        {
            // Постарайтесь вынести в SetUp всё неспецифическое конфигурирование так,
            // чтобы в конкретных тестах осталась только специфика теста,
            // без конфигурирования "обычного" сценария работы

            file = new File("someFile", new byte[] {1, 2, 3});
            signedContent = new byte[] {1, 7};

            cryptographer = A.Fake<ICryptographer>();
            sender = A.Fake<ISender>();
            recognizer = A.Fake<IRecognizer>();
            fileSender = new FileSender(cryptographer, sender, recognizer);
            defaultDocument = new Document(file.Name, file.Content, DateTime.Now, "4.0");
            
            A.CallTo(() => sender.TrySend(signedContent))
                .Returns(true);
            A.CallTo(() => recognizer.TryRecognize(file, out defaultDocument))
                .Returns(true);
            A.CallTo(() => cryptographer.Sign(defaultDocument.Content, certificate))
                .WithAnyArguments()
                .Returns(signedContent);
        }

        [TestCase("4.0")]
        [TestCase("3.1")]
        public void Send_WhenGoodFormat(string format)
        {
            defaultDocument.Format = format;
            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Should().BeEmpty();
        }

        [Test]
        public void Skip_WhenBadFormat()
        {
            defaultDocument.Format = "###";
            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Should().Contain(file);
        }

        [Test]
        public void Skip_WhenOlderThanAMonth()
        {
            defaultDocument.Created = DateTime.Today.AddMonths(-1);
            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Should().Contain(file);
        }

        [Test]
        public void Send_WhenYoungerThanAMonth()
        {
            defaultDocument.Created = DateTime.Now.AddMonths(-1).AddMinutes(1);
            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Should().BeEmpty();
        }

        [Test]
        public void Skip_WhenSendFails()
        {
            A.CallTo(() => sender.TrySend(signedContent))
                .Returns(false);
            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Should().Contain(file);
        }

        [Test]
        public void Skip_WhenNotRecognized()
        {
            A.CallTo(() => recognizer.TryRecognize(file, out defaultDocument))
                .Returns(false);
            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Should().Contain(file);
        }

        [Test]
        public void IndependentlySend_WhenSeveralFilesAndSomeAreInvalid()
        {
            var file1 = new File("otherFile", new byte[] {1, 2, 3});
            
            A.CallTo(() => recognizer.TryRecognize(file1, out defaultDocument))
                .Returns(false);
            
            fileSender.SendFiles(new[] {file, file1}, certificate)
                .SkippedFiles.Should().Contain(file1);
        }

        [Test]
        public void IndependentlySend_WhenSeveralFilesAndSomeCouldNotSend()
        {
            var otherContent = new byte[] {2, 3};
            var file1 = new File("otherFile", new byte[] {3, 3, 3});
            var otherDocument = new Document(file1.Name, file1.Content, DateTime.Now, "4.0");
            
            A.CallTo(() => recognizer.TryRecognize(file1, out otherDocument))
                .Returns(true);
            A.CallTo(() => cryptographer.Sign(file1.Content, certificate))
                .Returns(otherContent);
            A.CallTo(() => sender.TrySend(otherContent))
                .Returns(false);
            
            fileSender.SendFiles(new[] {file, file1}, certificate)
                .SkippedFiles.Should().Contain(file1);
        }
    }
}
