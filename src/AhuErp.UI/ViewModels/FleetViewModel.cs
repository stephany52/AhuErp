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
    /// ViewModel раздела «Транспортное обслуживание». Разделён на три секции:
    /// список ТС, расписание поездок выбранного ТС, форма бронирования.
    /// Бронирование проходит через <see cref="IFleetService.BookVehicle(int, int, DateTime, DateTime, string)"/>,
    /// который применяет Allen-overlap к существующим поездкам.
    /// </summary>
    public partial class FleetViewModel : ViewModelBase
    {
        private readonly IVehicleRepository _vehicles;
        private readonly IFleetService _fleet;
        private readonly IDocumentRepository _documents;

        public ObservableCollection<Vehicle> Vehicles { get; }
        public ObservableCollection<VehicleTrip> SelectedVehicleTrips { get; }
        public ObservableCollection<Document> TransportRequests { get; }

        public VehicleStatus[] VehicleStatuses { get; } =
            (VehicleStatus[])Enum.GetValues(typeof(VehicleStatus));

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(BookCommand))]
        private Vehicle selectedVehicle;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(BookCommand))]
        private DateTime startDate = DateTime.Today.AddHours(9);

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(BookCommand))]
        private DateTime endDate = DateTime.Today.AddHours(17);

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(BookCommand))]
        private string driverName;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(BookCommand))]
        private Document selectedDocument;

        [ObservableProperty]
        private string errorMessage;

        [ObservableProperty]
        private string statusMessage;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddVehicleCommand))]
        private string newVehicleLicensePlate;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddVehicleCommand))]
        private string newVehicleModel;

        [ObservableProperty]
        private VehicleStatus newVehicleStatus = VehicleStatus.Available;

        [ObservableProperty]
        private string newVehicleDriverName;

        public FleetViewModel(IVehicleRepository vehicles,
                              IFleetService fleet,
                              IDocumentRepository documents)
        {
            _vehicles = vehicles ?? throw new ArgumentNullException(nameof(vehicles));
            _fleet = fleet ?? throw new ArgumentNullException(nameof(fleet));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));

            Vehicles = new ObservableCollection<Vehicle>();
            SelectedVehicleTrips = new ObservableCollection<VehicleTrip>();
            TransportRequests = new ObservableCollection<Document>();
            Reload();
        }

        partial void OnSelectedVehicleChanged(Vehicle value)
        {
            ReloadTrips();
        }

        [RelayCommand(CanExecute = nameof(CanBook))]
        private void Book()
        {
            ErrorMessage = null;
            StatusMessage = null;
            try
            {
                var trip = _fleet.BookVehicle(
                    vehicleId: SelectedVehicle.Id,
                    documentId: SelectedDocument.Id,
                    startDate: StartDate,
                    endDate: EndDate,
                    driverName: DriverName);

                StatusMessage = $"Забронировано: {SelectedVehicle.LicensePlate}, " +
                                $"{trip.StartDate:dd.MM HH:mm} → {trip.EndDate:dd.MM HH:mm}.";
                ReloadTrips();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        [RelayCommand]
        private void Refresh() => Reload();

        [RelayCommand(CanExecute = nameof(CanAddVehicle))]
        private void AddVehicle()
        {
            ErrorMessage = null;
            StatusMessage = null;
            try
            {
                _vehicles.AddVehicle(new Vehicle
                {
                    LicensePlate = NewVehicleLicensePlate?.Trim(),
                    Model = NewVehicleModel?.Trim(),
                    CurrentStatus = NewVehicleStatus
                });
                StatusMessage = $"Добавлено ТС {NewVehicleLicensePlate}.";
                NewVehicleLicensePlate = null;
                NewVehicleModel = null;
                NewVehicleStatus = VehicleStatus.Available;
                NewVehicleDriverName = null;
                Reload();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        private bool CanBook() =>
            SelectedVehicle != null
            && SelectedDocument != null
            && !string.IsNullOrWhiteSpace(DriverName)
            && EndDate > StartDate;

        private bool CanAddVehicle() =>
            !string.IsNullOrWhiteSpace(NewVehicleLicensePlate)
            && !string.IsNullOrWhiteSpace(NewVehicleModel);

        private void Reload()
        {
            var vehicleId = SelectedVehicle?.Id;
            var docId = SelectedDocument?.Id;

            Vehicles.Clear();
            foreach (var v in _vehicles.ListVehicles().OrderBy(v => v.LicensePlate))
                Vehicles.Add(v);

            TransportRequests.Clear();
            foreach (var d in _documents.ListByType(DocumentType.Fleet)
                                        .OrderByDescending(d => d.CreationDate))
                TransportRequests.Add(d);

            SelectedVehicle = Vehicles.FirstOrDefault(v => v.Id == vehicleId) ?? Vehicles.FirstOrDefault();
            SelectedDocument = TransportRequests.FirstOrDefault(d => d.Id == docId);
            ReloadTrips();
        }

        private void ReloadTrips()
        {
            SelectedVehicleTrips.Clear();
            if (SelectedVehicle == null) return;
            foreach (var t in _vehicles.ListTrips(SelectedVehicle.Id))
                SelectedVehicleTrips.Add(t);
        }
    }
}
