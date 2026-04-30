namespace AhuErp.UI.Infrastructure
{
    /// <summary>
    /// Абстракция над <see cref="Microsoft.Win32.SaveFileDialog"/> и
    /// <see cref="Microsoft.Win32.OpenFileDialog"/>, изолирующая ViewModel от
    /// прямого обращения к WPF-диалогам и позволяющая подменять их в
    /// автотестах.
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// Показывает диалог «Сохранить как…» и возвращает выбранный путь
        /// или <c>null</c>, если пользователь отменил операцию.
        /// </summary>
        string PromptSaveFile(string title, string filter, string defaultFileName);

        /// <summary>
        /// Показывает диалог «Открыть файл…» и возвращает выбранный путь
        /// или <c>null</c>, если пользователь отменил операцию.
        /// </summary>
        string PromptOpenFile(string title, string filter);
    }
}
