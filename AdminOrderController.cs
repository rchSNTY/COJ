using coj.Models;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web.Mvc;

namespace coj.Controllers
{
    public class AdminOrderController : Controller
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["cojappdb"].ConnectionString;

        // ------------------ MANAGE ORDERS PAGE (GET) ------------------
        public ActionResult ManageOrders()
        {
            List<Orders> orders = new List<Orders>();

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();

                // ⭐ FIXED SQL: Changed 'Id' to 'OrderId' in the SELECT statement.
                // It's now selecting the column name directly from the table.
                string query = @"
                    SELECT
                        OrderId, 
                        RecipientName,
                        OrderDate,
                        TotalAmount, 
                        Status
                    FROM
                        orders
                    ORDER BY 
                        OrderDate DESC;";

                using (var cmd = new MySqlCommand(query, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // Mappings are correct based on your Orders.cs model
                        orders.Add(new Orders
                        {
                            OrderId = reader.GetInt32("OrderId"),
                            RecipientName = reader.GetString("RecipientName"),
                            OrderDate = reader.GetDateTime("OrderDate"),
                            Status = reader.GetString("Status"),
                            TotalAmount = reader.GetDecimal("TotalAmount")
                        });
                    }
                }
            }
            return View(orders);
        }

        // ------------------ UPDATE ORDER STATUS (POST - AJAX) ------------------
        [HttpPost]
        public ActionResult UpdateOrderStatus(int orderId, string newStatus)
        {
            try
            {
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // ⭐ FIXED SQL: Changed 'Id' to 'OrderId' in the WHERE clause.
                    string updateQuery = "UPDATE orders SET Status = @NewStatus WHERE OrderId = @OrderId;";
                    using (var cmd = new MySqlCommand(updateQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@NewStatus", newStatus);
                        cmd.Parameters.AddWithValue("@OrderId", orderId);
                        cmd.ExecuteNonQuery();
                    }
                }
                return Json(new { success = true, message = "Order status updated successfully." });
            }
            catch (Exception ex)
            {
                // This will now catch and report any other errors, like connection issues.
                return Json(new { success = false, message = "Error updating order status: " + ex.Message });
            }
        }
    }
}