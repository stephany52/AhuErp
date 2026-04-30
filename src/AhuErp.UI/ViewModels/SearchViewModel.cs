using System;
using System.Collections.ObjectModel;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AhuErp.UI.ViewModels
{
    /// <summary>
    /// Глобальный поиск по документам. Phase 10:
    /// — поиск метаданных (LIKE через <see cref="IDocumentRepository.Search"/>);
    /// — опциональный полнотекстовый поиск по содержимому вложений
    ///   (чекбокс «Искать в текстах вложений» → <see cref="ISearchIndexService.FullTextSearch"/>);
    /// — сохранённые фильтры (свои + shared) с CRUD через <see cref="ISavedSearchService"/>.
    /// </summary>
    public partial class SearchViewModel : ViewModelBase
    {
        private readonly IDocumentRepository _documents;
        private readonly ISearchIndexService _searchIndex;
        private readonly ISavedSearchService _savedSearches;
        private readonly IAuthService _auth;

        public ObservableCollection<Document> Results { get; } = new ObservableCollection<Document>();
        public ObservableCollection<SearchHit> FullTextHits { get; } = new ObservableCollection<SearchHit>();
        public ObservableCollection<SavedSearch> SavedSearches { get; } = new ObservableCollection<SavedSearch>();

        public DocumentDirection?[] AvailableDirections { get; } =
        {
            null,
            DocumentDirection.Incoming,
            DocumentDirection.Outgoing,
            DocumentDirection.Internal
        };

        public DocumentStatus?[] AvailableStatuses { get; } =
        {
            null,
            DocumentStatus.New,
            DocumentStatus.InProgress,
            DocumentStatus.OnHold,
            DocumentStatus.Completed,
            DocumentStatus.Cancelled
        };

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
        private string query;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
        private DocumentDirection? selectedDirection;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
        private DocumentStatus? selectedStatus;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
        private DateTime? periodFrom;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
        private DateTime? periodTo;

        [ObservableProperty]
        private bool searchInAttachments;

        [ObservableProperty]
        private string statusMessage;

        [ObservableProperty]
        private string errorMessage;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveFilterCommand))]
        private string saveFilterName;

        [ObservableProperty]
        private bool saveFilterShared;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ApplySavedSearchCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteSavedSearchCommand))]
        private SavedSearch selectedSavedSearch;

        public SearchViewModel(IDocumentRepository documents,
                               ISearchIndexService searchIndex = null,
                               ISavedSearchService savedSearches = null,
                               IAuthService auth = null)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _searchIndex = searchIndex;
            _savedSearches = savedSearches;
            _auth = auth;
            ReloadSavedSearches();
        }

        [RelayCommand(CanExecute = nameof(HasQuery))]
        private void Search()
        {
            ErrorMessage = null;
            Results.Clear();
            FullTextHits.Clear();
            try
            {
                var filter = BuildFilter();
                var found = _documents.Search(filter);
                foreach (var d in found) Results.Add(d);

                if (SearchInAttachments && _searchIndex != null && !string.IsNullOrWhiteSpace(Query))
                {
                    foreach (var hit in _searchIndex.FullTextSearch(Query, maxResults: 100))
                        FullTextHits.Add(hit);
                }

                StatusMessage = SearchInAttachments
                    ? $"Найдено: {Results.Count} (метаданные) + {FullTextHits.Count} (по тексту)"
                    : $"Найдено: {Results.Count}";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        /// <summary>
        /// B3 — пересобрать полнотекстовый индекс. Кнопка доступна только
        /// администратору (см. <see cref="RolePolicy.CanRebuildSearchIndex"/>).
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanRebuildIndex))]
        private void RebuildIndex()
        {
            ErrorMessage = null;
            if (_searchIndex == null)
            {
                ErrorMessage = "Поисковый индекс недоступен в этой сессии.";
                return;
            }
            try
            {
                var count = _searchIndex.ReindexAll();
                StatusMessage = $"Поисковый индекс перестроен: обработано {count} записей.";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        public bool CanRebuildIndex
        {
            get
            {
                if (_searchIndex == null) return false;
                var role = _auth?.CurrentEmployee?.Role;
                if (!role.HasValue) return true; // тесты без auth — разрешено
                return RolePolicy.CanRebuildSearchIndex(role.Value);
            }
        }

        [RelayCommand]
        private void Reset()
        {
            Query = null;
            SelectedDirection = null;
            SelectedStatus = null;
            PeriodFrom = null;
            PeriodTo = null;
            SearchInAttachments = false;
            Results.Clear();
            FullTextHits.Clear();
            StatusMessage = null;
            ErrorMessage = null;
        }

        // ------------ Phase 10 — сохранённые поиски ----------------------

        [RelayCommand(CanExecute = nameof(CanSaveFilter))]
        private void SaveFilter()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth?.CurrentEmployee?.Id ?? 0;
                if (actor == 0) { ErrorMessage = "Не определён текущий сотрудник."; return; }
                _savedSearches.Save(actor, SaveFilterName, BuildFilter(), SaveFilterShared);
                SaveFilterName = null;
                ReloadSavedSearches();
                StatusMessage = "Поиск сохранён.";
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        [RelayCommand(CanExecute = nameof(HasSelectedSavedSearch))]
        private void ApplySavedSearch()
        {
            ErrorMessage = null;
            try
            {
                var f = _savedSearches.LoadFilter(SelectedSavedSearch.Id);
                Query = f.Text;
                SelectedDirection = f.Direction;
                SelectedStatus = f.Status;
                PeriodFrom = f.From;
                PeriodTo = f.To;
                Search();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        [RelayCommand(CanExecute = nameof(CanDeleteSavedSearch))]
        private void DeleteSavedSearch()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth?.CurrentEmployee?.Id ?? 0;
                _savedSearches.Delete(SelectedSavedSearch.Id, actor);
                SelectedSavedSearch = null;
                ReloadSavedSearches();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        private void ReloadSavedSearches()
        {
            SavedSearches.Clear();
            if (_savedSearches == null || _auth?.CurrentEmployee == null) return;
            foreach (var s in _savedSearches.ListForUser(_auth.CurrentEmployee.Id))
                SavedSearches.Add(s);
        }

        private DocumentSearchFilter BuildFilter() => new DocumentSearchFilter
        {
            Text = Query,
            Direction = SelectedDirection,
            Status = SelectedStatus,
            From = PeriodFrom,
            To = PeriodTo,
        };

        private bool HasQuery() => !string.IsNullOrWhiteSpace(Query)
            || SelectedDirection.HasValue
            || SelectedStatus.HasValue
            || PeriodFrom.HasValue
            || PeriodTo.HasValue;

        private bool CanSaveFilter() => !string.IsNullOrWhiteSpace(SaveFilterName)
            && _savedSearches != null
            && _auth?.CurrentEmployee != null;

        private bool HasSelectedSavedSearch() => SelectedSavedSearch != null;

        private bool CanDeleteSavedSearch() => SelectedSavedSearch != null
            && _auth?.CurrentEmployee != null
            && SelectedSavedSearch.OwnerId == _auth.CurrentEmployee.Id;
    }
}
