using System;
using System.Collections.Generic;
using System.Windows;

namespace cvsimporter
{
    public partial class ManualPaymentWindow : Window
    {
        private string databasePath;
        private List<CustomerInfo> customers = new();
        private List<RentalInfo> rentals = new();

        public ManualPaymentWindow(string dbPath)
        {
            InitializeComponent();
            databasePath = dbPath;
        }

        private void txtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string search = txtSearch.Text.Trim().ToLower();
            customers = DatabaseHelper.SearchCustomers(databasePath, search);
            lstCustomers.ItemsSource = customers;
        }

        private void lstCustomers_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (lstCustomers.SelectedItem is CustomerInfo customer)
            {
                rentals = DatabaseHelper.GetRentalsForCustomer(databasePath, customer.CustomerId);
                lstRentals.ItemsSource = rentals;
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (lstCustomers.SelectedItem is not CustomerInfo customer || lstRentals.SelectedItem is not RentalInfo rental)
            {
                MessageBox.Show("Bitte Kunde und Miete auswählen.");
                return;
            }
            if (string.IsNullOrWhiteSpace(txtAmount.Text) || dpDate.SelectedDate == null)
            {
                MessageBox.Show("Bitte Betrag und Datum angeben.");
                return;
            }
            if (!decimal.TryParse(txtAmount.Text, out decimal amount))
            {
                MessageBox.Show("Ungültiger Betrag.");
                return;
            }
            string receiptNumber = txtReceiptNumber.Text.Trim();
            DatabaseHelper.InsertManualPayment(databasePath, rental.RentalID, dpDate.SelectedDate.Value, amount, receiptNumber);
            MessageBox.Show("Zahlung gespeichert.");
            this.Close();
        }
    }
}