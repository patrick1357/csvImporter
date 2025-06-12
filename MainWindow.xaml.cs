using System.Windows;
using Microsoft.Win32;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace cvsimporter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Liste aller importierten Rechnungsdatensätze
        private List<InvoiceRecord> allRecords = new();
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
        /// Öffnet einen Dateidialog zum Importieren einer CSV-Datei.
        /// Die Daten werden eingelesen und im DataGrid angezeigt.
        /// </summary>
        private void btnImport_Click(object sender, RoutedEventArgs e)
        {
            var records = CsvInvoiceImporter.ImportInvoices(this);
            if (records != null)
            {
                allRecords = records;
                dataGrid.ItemsSource = allRecords;
            }
        }

        /// <summary>
        /// Filtert alle Rechnungen, die im letzten Monat ausgestellt und noch nicht bezahlt wurden,
        /// und zeigt sie im DataGrid an.
        /// </summary>
        private void btnShowUnpaid_Click(object sender, RoutedEventArgs e)
        {
            var lastMonth = DateTime.Now.AddMonths(-1);
            var unpaid = allRecords
                .Where(r => !r.Paid && r.InvoiceDate >= new DateTime(lastMonth.Year, lastMonth.Month, 1) && r.InvoiceDate < new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1))
                .ToList();
            dataGrid.ItemsSource = unpaid;
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
            var results = DatabaseHelper.GetOutstandingPayments(databasePath, bisDatum);
            dataGrid.ItemsSource = results;
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

    
}