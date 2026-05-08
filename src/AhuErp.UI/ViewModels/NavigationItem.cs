using System;
using AhuErp.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AhuErp.UI.ViewModels
{
    /// <summary>
    /// Описывает один пункт навигационного меню главного окна. Реализован как
    /// <see cref="ObservableObject"/>, т.к. видимость пункта зависит от роли
    /// текущего пользователя и может обновляться после перелогина.
    /// </summary>
    public partial class NavigationItem : ObservableObject
    {
        public string Title { get; }

        public string ModuleKey { get; }

        public ViewModelBase ViewModel { get; }

        /// <summary>
        /// Bug #7. Если задан — пункт меню работает как «сохранённый пресет
        /// фильтров РКК»: при выборе MainViewModel переключает CurrentViewModel
        /// на <see cref="RkkViewModel"/> и применяет соответствующий фильтр
        /// через <see cref="RkkViewModel.ApplyPreset(RkkPreset)"/>.
        /// </summary>
        public RkkPreset? Preset { get; }

        [ObservableProperty]
        private bool isAllowed;

        public NavigationItem(string title, string moduleKey, ViewModelBase viewModel)
            : this(title, moduleKey, viewModel, null)
        {
        }

        public NavigationItem(string title, string moduleKey, ViewModelBase viewModel, RkkPreset? preset)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Название пункта меню не может быть пустым.", nameof(title));
            if (string.IsNullOrWhiteSpace(moduleKey))
                throw new ArgumentException("ModuleKey не может быть пустым.", nameof(moduleKey));
            Title = title;
            ModuleKey = moduleKey;
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            Preset = preset;
        }
    }
}
