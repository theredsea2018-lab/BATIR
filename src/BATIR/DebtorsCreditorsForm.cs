using Microsoft.Data.Sqlite;
using System.Data;

namespace BATIR;

public class DebtorsCreditorsForm : Form
{
    readonly DataGridView customerGrid = new();
    readonly DataGridView supplierGrid = new();
    readonly Label summary = new();

    public DebtorsCreditorsForm()
    {
        Text = "گزارش بدهکاران و بستانکاران | BATIR";
        Width = 1100; Height = 680;
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        Build(); LoadReport();
    }

    void Build()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var debtors = new TabPage("بدهکاران مشتریان");
        ConfigureGrid(customerGrid);
        debtors.Controls.Add(customerGrid);
        var creditors = new TabPage("بستانکاران تأمین‌کنندگان");
        ConfigureGrid(supplierGrid);
        creditors.Controls.Add(supplierGrid);
        tabs.TabPages.Add(debtors); tabs.TabPages.Add(creditors);
        var top = new Panel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(8) };
        summary.Dock = DockStyle.Fill; summary.TextAlign = ContentAlignment.MiddleRight;
        var refresh = new Button { Text = "به‌روزرسانی", Dock = DockStyle.Left, Width = 110 };
        refresh.Click += (_, _) => LoadReport();
        top.Controls.Add(summary); top.Controls.Add(refresh);
        Controls.Add(tabs); Controls.Add(top);
    }

    static void ConfigureGrid(DataGridView g)
    {
        g.Dock = DockStyle.Fill; g.ReadOnly = true; g.AllowUserToAddRows = false;
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
    }

    void LoadReport()
    {
        var customers = Database.Query(@"SELECT c.Id AS [شناسه], c.Name AS [مشتری], c.Phone AS [تلفن],
c.CreditLimit AS [سقف اعتبار], c.Balance AS [مانده بدهکار],
COALESCE((SELECT SUM(i.Total) FROM Invoices i WHERE i.CustomerId=c.Id AND i.Type='Sale'),0) AS [کل فروش],
COALESCE((SELECT SUM(i.Paid) FROM Invoices i WHERE i.CustomerId=c.Id AND i.Type='Sale'),0) AS [پرداخت فاکتوری],
COALESCE((SELECT SUM(r.Total) FROM SalesReturns r WHERE r.CustomerId=c.Id),0) AS [برگشت از فروش],
COALESCE((SELECT SUM(ct.Amount) FROM CashTransactions ct WHERE ct.CustomerId=c.Id AND ct.Type='CustomerReceipt'),0) AS [دریافت مستقیم]
FROM Customers c WHERE c.Balance>0 ORDER BY c.Balance DESC;");

        var suppliers = Database.Query(@"SELECT s.Id AS [شناسه], s.Name AS [تأمین‌کننده], s.Phone AS [تلفن],
s.Balance AS [مانده بستانکار],
COALESCE((SELECT SUM(i.Total) FROM Invoices i WHERE i.SupplierId=s.Id AND i.Type='Purchase'),0) AS [کل خرید],
COALESCE((SELECT SUM(i.Paid) FROM Invoices i WHERE i.SupplierId=s.Id AND i.Type='Purchase'),0) AS [پرداخت فاکتوری],
COALESCE((SELECT SUM(r.Total) FROM PurchaseReturns r WHERE r.SupplierId=s.Id),0) AS [برگشت از خرید],
COALESCE((SELECT SUM(ct.Amount) FROM CashTransactions ct WHERE ct.SupplierId=s.Id AND ct.Type='SupplierPayment'),0) AS [پرداخت مستقیم]
FROM Suppliers s WHERE s.Balance>0 ORDER BY s.Balance DESC;");

        customerGrid.DataSource = customers; supplierGrid.DataSource = suppliers;
        long customerTotal = customers.Rows.Cast<DataRow>().Sum(r => Convert.ToInt64(r["مانده بدهکار"]));
        long supplierTotal = suppliers.Rows.Cast<DataRow>().Sum(r => Convert.ToInt64(r["مانده بستانکار"]));
        summary.Text = "جمع بدهکار مشتریان: " + customerTotal.ToString("N0") + " ریال  |  جمع بستانکار تأمین‌کنندگان: " + supplierTotal.ToString("N0") + " ریال";
    }
}
