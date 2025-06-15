using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

public partial class SelectRentalWindow : Window
{
    public int? SelectedRentalId { get; private set; }
    public List<int> SelectedRentalIds { get; private set; } = new();
    public bool SplitPayment { get; private set; } = false;
    public bool SkipPayment { get; private set; } = false;

    private decimal paymentAmount;
    private string receiptNumber;
    private DateTime paymentDate;

    private List<RentalDisplay> displayList = new();

    public SelectRentalWindow(
        List<(int RentalID, string OrderNumber, DateTime RentalStart, DateTime? RentalEnd, string Instrument, decimal RentalPrice)> rentals,
        string bestellNr, string name, DateTime zahlungsDatum, decimal betrag, string belegnummer = null)
    {
        InitializeComponent();

        paymentAmount = betrag;
        receiptNumber = belegnummer;
        paymentDate = zahlungsDatum;

        foreach (var r in rentals)
        {
            displayList.Add(new RentalDisplay
            {
                RentalID = r.RentalID,
                RentalPrice = r.RentalPrice,
                Display = $"Miete ab {r.RentalStart:dd.MM.yyyy}" +
                          (r.RentalEnd.HasValue ? $" bis {r.RentalEnd:dd.MM.yyyy}" : "") +
                          (!string.IsNullOrWhiteSpace(r.OrderNumber) ? $" (BestellNr: {r.OrderNumber})" : "") +
                          $" | Instrument: {r.Instrument} | Mietpreis: {r.RentalPrice:N2} €"
            });
        }
        lstRentals.ItemsSource = displayList;

        DataContext = new
        {
            InfoText = $"CSV: Kunde: {name}, BestellNr: {bestellNr}, Betrag: {betrag:0.00} €, Datum: {zahlungsDatum:dd.MM.yyyy}",
            DetailsText = rentals.Count == 1
                ? "Es gibt nur eine Miete für diesen Kunden."
                : "Mehrere Mieten gefunden. Bitte wählen Sie die passende(n) aus."
        };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (lstRentals.SelectedItem is RentalDisplay selected)
        {
            SelectedRentalId = selected.RentalID;
            DialogResult = true;
        }
        else
        {
            MessageBox.Show("Bitte wählen Sie eine Miete aus.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void SplitPayment_Click(object sender, RoutedEventArgs e)
    {
        var selected = lstRentals.SelectedItems.Cast<RentalDisplay>().ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Bitte wählen Sie mindestens eine Miete aus.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        decimal sum = selected.Sum(x => x.RentalPrice);
        if (Math.Abs(sum - paymentAmount) > 0.01m)
        {
            MessageBox.Show($"Die Summe der Mietpreise ({sum:N2} €) stimmt nicht mit dem Zahlungsbetrag ({paymentAmount:N2} €) überein.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        SelectedRentalIds = selected.Select(x => x.RentalID).ToList();
        SplitPayment = true;
        DialogResult = true;
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        SkipPayment = true;
        DialogResult = false;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private class RentalDisplay
    {
        public int RentalID { get; set; }
        public decimal RentalPrice { get; set; }
        public string Display { get; set; }
    }
}