using System;
using AhuErp.UI.ViewModels;

namespace AhuErp.UI.Infrastructure
{
    /// <summary>
    /// Реализация <see cref="IDocumentNavigator"/>. Зарегистрирована как
    /// синглтон. <see cref="ViewModels.MainViewModel"/> при создании вызывает
    /// <see cref="AttachMain"/>, чтобы дать навигатору доступ к корневой VM.
    /// </summary>
    public sealed class DocumentNavigator : IDocumentNavigator
    {
        private MainViewModel _main;

        public void AttachMain(MainViewModel main)
        {
            _main = main ?? throw new ArgumentNullException(nameof(main));
        }

        public void OpenDocument(int documentId)
        {
            _main?.NavigateToDocument(documentId);
        }
    }
}
