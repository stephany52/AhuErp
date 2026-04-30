using System.Windows;
using System.Windows.Controls;
using AhuErp.UI.ViewModels;

namespace AhuErp.UI.Views
{
    public partial class OrgStructureView : UserControl
    {
        public OrgStructureView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// TreeView не умеет в TwoWay-биндинг SelectedItem, поэтому
        /// прокидываем выбранный узел во ViewModel вручную (A10).
        /// </summary>
        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is OrgStructureViewModel vm)
            {
                vm.SelectedNode = e.NewValue as DepartmentNode;
            }
        }
    }
}
