using System.Windows;
using Microsoft.Win32;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace cvsimporter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Liste aller importierten Rechnungsdatensätze
        private List<InvoiceRecord> allRecords = new();
        private List<OutstandingPaymentInfo> allOutstandingPayments = new();
        private List<PaymentInfo> allPayments = new();
        private List<MultipleRentalCustomer> multipleRentalCustomers = new();
        private bool showingAllPayments = false;
        private bool showingMultipleRentalCustomers = false;
        private string databasePath = "database.db"; // Standard-Datenbankdatei

        public MainWindow()
        {
            InitializeComponent();
            DatabaseHelper.CheckOrCreateDatabase(databasePath);

            // Monat/Jahr-Auswahl initialisieren
            comboMonat.ItemsSource = Enumerable.Range(1, 12).Select(m => m.ToString("D2"));
            comboMonat.SelectedIndex = DateTime.Now.Month - 1;
            comboJahr.ItemsSource = Enumerable.Range(2014, DateTime.Now.Year - 2014 + 2).Select(y => y.ToString());
            comboJahr.SelectedItem = DateTime.Now.Year.ToString();
        }

        /// <summary>
        /// Öffnet einen Dateidialog, um eine andere Datenbankdatei auszuwählen und zu verwenden.
        /// </summary>
        private void OpenOtherDatabase_Click(object sender, RoutedEventArgs e)
        {
            // Explicitly specify the internal method to resolve ambiguity
            databasePath = DatabaseHelper.SelectAndPrepareDatabase(this, databasePath);
        }

        /// <summary>
        /// Zeigt alle ausstehenden Zahlungen bis zu einem bestimmten Datum im DataGrid an.
        /// </summary>
        private void btnShowOutstandingPayments_Click(object sender, RoutedEventArgs e)
        {
            if (comboMonat.SelectedItem == null || comboJahr.SelectedItem == null)
            {
                MessageBox.Show("Bitte Monat und Jahr auswählen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            int monat = int.Parse(comboMonat.SelectedItem.ToString());
            int jahr = int.Parse(comboJahr.SelectedItem.ToString());
            var bisDatum = new DateTime(jahr, monat, DateTime.DaysInMonth(jahr, monat));
            allOutstandingPayments = DatabaseHelper.GetOutstandingPayments(databasePath, bisDatum);
            showingAllPayments = false;
            showingMultipleRentalCustomers = false;
            SetDataGridColumnsForOutstandingPayments();
            ApplyFilter();
        }

        /// <summary>
        /// Zeigt alle Zahlungen im DataGrid an.
        /// </summary>
        private void btnShowAllPayments_Click(object sender, RoutedEventArgs e)
        {
            allPayments = DatabaseHelper.GetAllPayments(databasePath);
            showingAllPayments = true;
            showingMultipleRentalCustomers = false;
            SetDataGridColumnsForAllPayments();
            ApplyFilter();
        }

        private void btnShowMultipleRentals_Click(object sender, RoutedEventArgs e)
        {
            multipleRentalCustomers = DatabaseHelper.GetCustomersWithMultipleRentals(databasePath);
            showingMultipleRentalCustomers = true;
            showingAllPayments = false;
            SetDataGridColumnsForMultipleRentals();
            ApplyFilter();
        }

        private void SetDataGridColumnsForOutstandingPayments()
        {
            dataGrid.Columns.Clear();
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Kunde", Binding = new System.Windows.Data.Binding("CustomerName") });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Kundennr.", Binding = new System.Windows.Data.Binding("CustomerId") });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Instrument", Binding = new System.Windows.Data.Binding("Instrument") });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Anzahl Zahlungen", Binding = new System.Windows.Data.Binding("AnzahlZahlungen") });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Letzter Zahlungseingang", Binding = new System.Windows.Data.Binding("LetzterZahlungsmonat") });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Insgesamt Bezahlt", Binding = new System.Windows.Data.Binding("InsgesamtBezahlt") { StringFormat = "0.00 €" } });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Erwartete Zahlungen", Binding = new System.Windows.Data.Binding("ErwarteteZahlungen") { StringFormat = "0.00 €" } });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Offener Betrag", Binding = new System.Windows.Data.Binding("OffenerBetrag") { StringFormat = "0.00 €" } });
        }

        private void SetDataGridColumnsForAllPayments()
        {
            dataGrid.Columns.Clear();
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Kunde", Binding = new System.Windows.Data.Binding("CustomerName") });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Kundennr.", Binding = new System.Windows.Data.Binding("CustomerId") });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Instrument", Binding = new System.Windows.Data.Binding("Instrument") });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Zahlungsdatum", Binding = new System.Windows.Data.Binding("PaymentDate") { StringFormat = "dd.MM.yyyy" } });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Betrag", Binding = new System.Windows.Data.Binding("Amount") { StringFormat = "0.00 €" } });
        }

        private void SetDataGridColumnsForMultipleRentals()
        {
            dataGrid.Columns.Clear();
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Kunde", Binding = new System.Windows.Data.Binding("CustomerName") });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Kundennr.", Binding = new System.Windows.Data.Binding("CustomerId") });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Anzahl Instrumente", Binding = new System.Windows.Data.Binding("InstrumentCount") });
        }

        private void FilterChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (showingAllPayments)
            {
                var filtered = allPayments.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(filterCustomerName.Text))
                    filtered = filtered.Where(x => x.CustomerName.Contains(filterCustomerName.Text, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(filterCustomerId.Text))
                    filtered = filtered.Where(x => x.CustomerId.ToString().Contains(filterCustomerId.Text, StringComparison.OrdinalIgnoreCase));
                dataGrid.ItemsSource = filtered.ToList();
            }
            else if (showingMultipleRentalCustomers)
            {
                var filtered = multipleRentalCustomers.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(filterCustomerName.Text))
                    filtered = filtered.Where(x => x.CustomerName.Contains(filterCustomerName.Text, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(filterCustomerId.Text))
                    filtered = filtered.Where(x => x.CustomerId.ToString().Contains(filterCustomerId.Text, StringComparison.OrdinalIgnoreCase));
                dataGrid.ItemsSource = filtered.ToList();
            }
            else
            {
                var filtered = allOutstandingPayments.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(filterCustomerName.Text))
                    filtered = filtered.Where(x => x.CustomerName.Contains(filterCustomerName.Text, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(filterCustomerId.Text))
                    filtered = filtered.Where(x => x.CustomerId.ToString().Contains(filterCustomerId.Text, StringComparison.OrdinalIgnoreCase));
                dataGrid.ItemsSource = filtered.ToList();
            }
        }

        /// <summary>
        /// Importiert Kunden- und Instrumentendaten aus einer CSV-Datei und fügt sie der Datenbank hinzu.
        /// </summary>
        private void InitialDatabaseImport_Click(object sender, RoutedEventArgs e)
        {
            DatabaseHelper.ImportInitialData(this, databasePath);
        }

        /// <summary>
        /// Importiert Zahlungsdaten aus einer CSV-Datei und fügt sie der Datenbank hinzu.
        /// </summary>
        private void InitialPaymentsImport_Click(object sender, RoutedEventArgs e)
        {
            DatabaseHelper.ImportPayments(this, databasePath);
        }
    }

    /// <summary>
    /// Repräsentiert einen Rechnungsdatensatz mit Kunden- und Rechnungsinformationen.
    /// </summary>
    public class InvoiceRecord
    {
        public string CustomerName { get; set; }    // Name des Kunden
        public string CustomerId { get; set; }      // Kundennummer
        public string InvoiceNumber { get; set; }   // Rechnungsnummer
        public DateTime InvoiceDate { get; set; }   // Rechnungsdatum
        public bool Paid { get; set; }              // Status: bezahlt (true) oder nicht bezahlt (false)
    }

    public class MultipleRentalCustomer
    {
        public string CustomerName { get; set; }
        public int CustomerId { get; set; }
        public int InstrumentCount { get; set; }
    }
}