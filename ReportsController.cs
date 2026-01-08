using coj.Models;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web.Mvc;

namespace coj.Controllers
{
    public class ReportsController : Controller
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["cojappdb"].ConnectionString;

        public ActionResult SalesReport()
        {
            return View();
        }

        // ============================================
        // GET: Daily or Monthly Sales Data for Chart
        // ============================================
        [HttpGet]
        public JsonResult GetSalesData(string type)
        {
            List<SalesReportItem> data = new List<SalesReportItem>();

            using (MySqlConnection con = new MySqlConnection(connectionString))
            {
                con.Open();

                string query = type == "monthly"
                    ? @"SELECT DATE_FORMAT(OrderDate,'%Y-%m') AS label, SUM(TotalAmount) AS total
                        FROM orders
                        WHERE Status='Delivered'
                        GROUP BY DATE_FORMAT(OrderDate,'%Y-%m')
                        ORDER BY label"
                    : @"SELECT DATE(OrderDate) AS label, SUM(TotalAmount) AS total
                        FROM orders
                        WHERE Status='Delivered'
                        GROUP BY DATE(OrderDate)
                        ORDER BY DATE(OrderDate)";

                using (MySqlCommand cmd = new MySqlCommand(query, con))
                using (MySqlDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        data.Add(new SalesReportItem
                        {
                            Label = dr["label"].ToString(),
                            TotalSales = Convert.ToDecimal(dr["total"])
                        });
                    }
                }

                con.Close();
            }

            return Json(data, JsonRequestBehavior.AllowGet);
        }

        // ================================================
        // GET: Summary Cards (Today Sales, Month Sales, etc.)
        // ================================================
        [HttpGet]
        public JsonResult GetSalesSummary()
        {
            SalesSummary summary = new SalesSummary();

            using (MySqlConnection con = new MySqlConnection(connectionString))
            {
                con.Open();

                // Today Sales
                using (MySqlCommand cmd = new MySqlCommand(
                    @"SELECT IFNULL(SUM(TotalAmount),0)
                      FROM orders
                      WHERE DATE(OrderDate) = CURDATE()
                      AND Status='Delivered'", con))
                {
                    summary.TodaySales = Convert.ToDecimal(cmd.ExecuteScalar());
                }

                // Month Sales
                using (MySqlCommand cmd = new MySqlCommand(
                    @"SELECT IFNULL(SUM(TotalAmount),0)
                      FROM orders
                      WHERE MONTH(OrderDate) = MONTH(CURDATE())
                      AND YEAR(OrderDate) = YEAR(CURDATE())
                      AND Status='Delivered'", con))
                {
                    summary.MonthSales = Convert.ToDecimal(cmd.ExecuteScalar());
                }

                // Delivered Orders
                using (MySqlCommand cmd = new MySqlCommand(
                    @"SELECT COUNT(*) FROM orders WHERE Status='Delivered'", con))
                {
                    summary.DeliveredOrders = Convert.ToInt32(cmd.ExecuteScalar());
                }

                // Cancelled Orders
                using (MySqlCommand cmd = new MySqlCommand(
                    @"SELECT COUNT(*) FROM orders WHERE Status='Canceled'", con))
                {
                    summary.CancelledOrders = Convert.ToInt32(cmd.ExecuteScalar());
                }

                con.Close();
            }

            return Json(summary, JsonRequestBehavior.AllowGet);
        }
    }
}
