using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MySql.Data.MySqlClient;
using coj.Models;

namespace coj.Controllers
{
    public class AdminCustomerController : Controller
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["cojappdb"].ConnectionString;

        [Authorize]
        public ActionResult ManageCustomers()
        {
            if (Session["IsAdmin"] == null || !(bool)Session["IsAdmin"])
            {
                return RedirectToAction("Login", "Home");
            }

            var customers = new List<Customer>();
            string query = "SELECT Id, FirstName, LastName, Email, Address, PhoneNumber, IsActive FROM users WHERE IsAdmin = 0;";

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(query, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var first = reader.IsDBNull(reader.GetOrdinal("FirstName")) ? string.Empty : reader.GetString("FirstName");
                        var last = reader.IsDBNull(reader.GetOrdinal("LastName")) ? string.Empty : reader.GetString("LastName");
                        var isActive = reader.IsDBNull(reader.GetOrdinal("IsActive")) ? false : reader.GetBoolean("IsActive");

                        var cust = new Customer
                        {
                            Id = reader.GetInt32("Id"),
                            FirstName = first,
                            LastName = last,
                            Email = reader.IsDBNull(reader.GetOrdinal("Email")) ? string.Empty : reader.GetString("Email"),
                            Address = reader.IsDBNull(reader.GetOrdinal("Address")) ? string.Empty : reader.GetString("Address"),
                            PhoneNumber = reader.IsDBNull(reader.GetOrdinal("PhoneNumber")) ? string.Empty : reader.GetString("PhoneNumber"),
                            IsActive = isActive
                        };

                        customers.Add(cust);
                    }
                }
            }

            return View(customers);
        }

        // Toggle status (AJAX-friendly)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleCustomerStatus(int id, int statusValue)
        {
            using (var db = new MySqlConnection(ConfigurationManager.ConnectionStrings["cojappdb"].ConnectionString))
            {
                db.Open();
                string query = @"UPDATE users SET IsActive = @status WHERE Id = @id";

                using (var cmd = new MySqlCommand(query, db))
                {
                    cmd.Parameters.AddWithValue("@status", statusValue == 1);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }

            return RedirectToAction("ManageCustomers");
        }

        // Delete customer endpoint (AJAX-friendly)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public ActionResult DeleteCustomer(int id)
        {
            if (Session["IsAdmin"] == null || !(bool)Session["IsAdmin"])
            {
                return new HttpStatusCodeResult(403);
            }

            string query = "DELETE FROM users WHERE Id = @Id AND IsAdmin = 0";
            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    int rowsAffected = cmd.ExecuteNonQuery();

                    if (rowsAffected == 0)
                    {
                        return new HttpStatusCodeResult(404);
                    }
                }
            }

            return new HttpStatusCodeResult(200);
        }
    }
}
