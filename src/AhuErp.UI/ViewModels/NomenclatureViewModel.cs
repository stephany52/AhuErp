using System;
using System.Collections.ObjectModel;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AhuErp.UI.ViewModels
{
    /// <summary>
    /// Управление номенклатурой дел и видами документов.
    /// Минимально-необходимый CRUD-интерфейс на двух вкладках.
    /// </summary>
    public partial class NomenclatureViewModel : ViewModelBase
    {
        private readonly INomenclatureService _service;

        public ObservableCollection<NomenclatureCase> Cases { get; }
            = new ObservableCollection<NomenclatureCase>();

        public ObservableCollection<DocumentTypeRef> Types { get; }
            = new ObservableCollection<DocumentTypeRef>();

        public DocumentDirection[] Directions { get; } =
            (DocumentDirection[])Enum.GetValues(typeof(DocumentDirection));

        [ObservableProperty]
        private NomenclatureCase selectedCase;

        [ObservableProperty]
        private DocumentTypeRef selectedType;

        [ObservableProperty]
        private string newCaseIndex;

        [ObservableProperty]
        private string newCaseTitle;

        [ObservableProperty]
        private int newCaseRetention = 5;

        [ObservableProperty]
        private string newCaseArticle;

        [ObservableProperty]
        private int newCaseYear = DateTime.Now.Year;

        [ObservableProperty]
        private bool newCaseIsActive = true;

        [ObservableProperty]
        private string newTypeName;

        [ObservableProperty]
        private string newTypeShortCode;

        [ObservableProperty]
        private int newTypeRetention = 5;

        [ObservableProperty]
        private DocumentDirection newTypeDirection = DocumentDirection.Internal;

        [ObservableProperty]
        private bool newTypeIsActive = true;

        [ObservableProperty]
        private string newTypeTemplate = "{Код}-{ИндексДела}-{Год}-{Номер0000}";

        [ObservableProperty]
        private string errorMessage;

        public NomenclatureViewModel(INomenclatureService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            Reload();
        }

        [RelayCommand]
        private void Reload()
        {
            ErrorMessage = null;
            Cases.Clear();
            foreach (var c in _service.ListCases(activeOnly: false)) Cases.Add(c);
            Types.Clear();
            foreach (var t in _service.ListTypes(activeOnly: false)) Types.Add(t);
        }

        [RelayCommand]
        private void AddCase()
        {
            ErrorMessage = null;
            try
            {
                _service.AddCase(new NomenclatureCase
                {
                    Index = NewCaseIndex,
                    Title = NewCaseTitle,
                    RetentionPeriodYears = NewCaseRetention,
                    Article = NewCaseArticle,
                    Year = NewCaseYear,
                    IsActive = NewCaseIsActive
                });
                NewCaseIndex = null;
                NewCaseTitle = null;
                NewCaseRetention = 5;
                NewCaseArticle = null;
                NewCaseYear = DateTime.Now.Year;
                NewCaseIsActive = true;
                Reload();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        [RelayCommand]
        private void AddType()
        {
            ErrorMessage = null;
            try
            {
                _service.AddType(new DocumentTypeRef
                {
                    Name = NewTypeName,
                    ShortCode = NewTypeShortCode,
                    DefaultDirection = NewTypeDirection,
                    DefaultRetentionYears = NewTypeRetention,
                    RegistrationNumberTemplate = NormalizeTemplate(NewTypeTemplate),
                    IsActive = NewTypeIsActive
                });
                NewTypeName = null;
                NewTypeShortCode = null;
                NewTypeRetention = 5;
                NewTypeDirection = DocumentDirection.Internal;
                NewTypeIsActive = true;
                NewTypeTemplate = "{Код}-{ИндексДела}-{Год}-{Номер0000}";
                Reload();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        private static string NormalizeTemplate(string template)
        {
            if (string.IsNullOrWhiteSpace(template)) return template;
            return template
                .Replace("{Код}", "{Code}")
                .Replace("{ИндексДела}", "{CaseIndex}")
                .Replace("{Год}", "{Year}")
                .Replace("{Номер0000}", "{Sequence:0000}")
                .Replace("{Номер00000}", "{Sequence:00000}");
        }
    }
}
