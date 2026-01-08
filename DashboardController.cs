using coj.Models;
// ⭐ ADD this for your ViewModel classes
// ⭐ ADD this for MySQL database access
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web.Mvc;

namespace coj.Controllers
{
    // Optional: Add [Authorize] attribute if only admins should access this
    // [Authorize(Roles = "Admin")] 
    public class DashboardController : Controller
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["cojappdb"].ConnectionString;

        // GET: Dashboard
        public ActionResult Admin_Dashboard()
        {
            var vm = new DashboardViewModel();

            // Establish connection and run queries
            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();

                // -----------------------------------------------------------
                // 1. STATS CALCULATIONS (Total Sales, Orders, Customers)
                // -----------------------------------------------------------
                string statsQuery = @"
                    SELECT
                        (SELECT SUM(TotalAmount) FROM Orders WHERE Status = 'Delivered') AS TotalSales,
                        (SELECT COUNT(OrderId) FROM Orders) AS TotalOrders,
                        (SELECT COUNT(Id) FROM Users) AS TotalCustomers;";

                using (var cmd = new MySqlCommand(statsQuery, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        // Safely handle NULL values for SUM (if no Delivered orders exist)
                        vm.TotalSales = reader.IsDBNull(reader.GetOrdinal("TotalSales")) ? 0.00m : reader.GetDecimal("TotalSales");
                        vm.TotalOrders = reader.GetInt32("TotalOrders");
                        vm.TotalCustomers = reader.GetInt32("TotalCustomers");
                    }
                }

                // -----------------------------------------------------------
                // 2. MONTHLY SALES (CHART DATA) - Use SQL Grouping
                // -----------------------------------------------------------

                // IMPORTANT: This query uses standard MySQL/MariaDB date functions
                string monthlySalesQuery = @"
                    SELECT
                        YEAR(OrderDate) AS SaleYear,
                        MONTH(OrderDate) AS SaleMonth,
                        SUM(TotalAmount) AS Total
                    FROM
                        Orders
                    WHERE
                        OrderDate >= DATE_SUB(CURDATE(), INTERVAL 6 MONTH)
                    GROUP BY
                        SaleYear, SaleMonth
                    ORDER BY
                        SaleYear, SaleMonth;";

                // Close the previous reader and open a new one
                // You must ensure the previous reader is closed before executing a new command.

                // Re-open connection if it was closed by the previous reader's disposal
                if (conn.State != System.Data.ConnectionState.Open) conn.Open();

                using (var cmd = new MySqlCommand(monthlySalesQuery, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int year = reader.GetInt32("SaleYear");
                        int month = reader.GetInt32("SaleMonth");
                        decimal total = reader.GetDecimal("Total");

                        // Format month label (e.g., "Jan", "Feb")
                        string monthLabel = new DateTime(year, month, 1).ToString("MMM");

                        vm.Months.Add(monthLabel);
                        vm.SalesValues.Add(total);
                    }
                }

                // -----------------------------------------------------------
                // 3. RECENT ORDERS
                // -----------------------------------------------------------

                // Close the previous reader and open a new one
                if (conn.State != System.Data.ConnectionState.Open) conn.Open();

                string recentOrdersQuery = @"
                    SELECT 
                        OrderId, 
                        RecipientName AS Customer, 
                        Status 
                    FROM 
                        Orders 
                    ORDER BY 
                        OrderId DESC 
                    LIMIT 4;";

                using (var cmd = new MySqlCommand(recentOrdersQuery, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        vm.RecentOrders.Add(new RecentOrderVM
                        {
                            OrderId = reader.GetInt32("OrderId"),
                            // Assuming RecipientName is available on the Orders table
                            Customer = reader.GetString("Customer"),
                            Status = reader.GetString("Status")
                        });
                    }
                }
            } // Connection is automatically closed and disposed here (due to 'using')

            return View(vm);
        }
    }
}