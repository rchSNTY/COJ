using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using coj.Models;
using MySql.Data.MySqlClient;
using System.Configuration;
using System.IO;
using System.Web;

namespace coj.Controllers
{
    public class AdminProductsController : Controller
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["cojappdb"].ConnectionString;

        // ------------------ LOAD PRODUCTS + SIZES ------------------
        private List<Product> GetProductsWithSizes()
        {
            var products = new List<Product>();

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                // FIX: Removed "WHERE IsActive = 1" to show ALL products for the admin screen.
                // The query is now "SELECT * FROM products ORDER BY Id;" to fetch everything.
                string query =
                    "SELECT * FROM products ORDER BY Id;";

                using (var cmd = new MySqlCommand(query, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        products.Add(new Product
                        {
                            Id = reader.GetInt32("Id"),
                            Name = reader.GetString("Name"),
                            BasePrice = reader.GetDecimal("BasePrice"),
                            Stock = reader.GetInt32("Stock"),
                            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? "" : reader.GetString("Description"),
                            ImageUrl = reader.IsDBNull(reader.GetOrdinal("ImageUrl")) ? "" : reader.GetString("ImageUrl"),
                            IsActive = reader.GetBoolean("IsActive"),
                            IsFeatured = reader.GetBoolean("IsFeatured"),
                            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? "" : reader.GetString("Category"),
                            ProductSizes = new List<ProductSize>()
                        });
                    }
                }
            }

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                string sizeQuery =
                    "SELECT Id, Product_Id, SizeName, Price FROM product_sizes;";

                using (var cmd = new MySqlCommand(sizeQuery, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var size = new ProductSize
                        {
                            Id = reader.GetInt32("Id"),
                            Product_Id = reader.GetInt32("Product_Id"),
                            SizeName = reader.GetString("SizeName"),
                            Price = reader.GetDecimal("Price")
                        };

                        var parent = products.FirstOrDefault(p => p.Id == size.Product_Id);
                        parent?.ProductSizes.Add(size);
                    }
                }
            }

            return products;
        }

        // ------------------ MAIN TABLE VIEW ------------------

        // ------------------ ADMIN INDEX (GET) ------------------
        public ActionResult Admin_Index()
        {
            var products = new List<Product>();

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                // FIX: Select ALL products (Active=1 and Inactive=0) for full admin control.
                // Order by IsActive DESC so active products appear at the top.
                string query = "SELECT * FROM products ORDER BY IsActive DESC, Id ASC;";

                using (var cmd = new MySqlCommand(query, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        products.Add(new Product
                        {
                            Id = reader.GetInt32("Id"),
                            Name = reader.GetString("Name"),
                            BasePrice = reader.GetDecimal("BasePrice"),
                            Stock = reader.GetInt32("Stock"),
                            IsActive = reader.GetBoolean("IsActive"),
                            Category = reader.GetString("Category"),
                            // NOTE: ProductSizes list should be loaded separately or via a join 
                            // if you need it here. Assuming it is loaded via a separate method 
                            // or lazy loading for the view to work.
                        });
                    }
                }
            }
            return View(products);
        }

        // ------------------ LOAD CREATE MODAL ------------------
        public ActionResult CreatePartial()
        {
            return PartialView("_CreateProductModalContent", new Product());
        }

        // ------------------ LOAD VIEW MODAL ------------------
        public ActionResult ViewPartial(int id)
        {
            // Note: Using GetProductsWithSizes() to fetch all products for admin view
            var product = GetProductsWithSizes().FirstOrDefault(p => p.Id == id);
            if (product == null)
            {
                return HttpNotFound();
            }
            return PartialView("_ViewProductModalContent", product);
        }

        // ------------------ LOAD EDIT MODAL (GET) ------------------
        public ActionResult EditPartial(int id)
        {
            // Note: Using GetProductsWithSizes() to fetch all products for admin view
            var product = GetProductsWithSizes().FirstOrDefault(p => p.Id == id);

            if (product == null)
            {
                return HttpNotFound();
            }
            return PartialView("_EditProductModalContent", product);
        }

        // ------------------ DELETE MODAL (GET) ------------------
        // Loads the confirmation modal content.
        public ActionResult DeletePartial(int id)
        {
            Product product = GetProductById(id);

            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction("Admin_Index");
            }

            // ⭐ NEW CHECK HERE: 
            if (HasActiveOrders(id))
            {
                // If the product is part of an active order, show a restricted modal.
                ViewBag.CanDelete = false;
                ViewBag.RestrictionMessage = "This product cannot be deleted because it is associated with pending, processing, or shipped orders. You must wait until all associated orders are 'Delivered' or 'Canceled'.";
            }
            else
            {
                // If no active orders, allow deletion (setting IsActive = 0)
                ViewBag.CanDelete = true;
            }

            ViewBag.ProductName = product.Name;
            return PartialView("_DeleteProductModalContent", product);
        }

        // ------------------ DELETE PRODUCT (POST) ------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            // ⭐ NEW CHECK HERE: Re-check the active order status
            if (HasActiveOrders(id))
            {
                TempData["Error"] = "Deletion failed: Product is still part of active orders. Wait until orders are 'Delivered' or 'Canceled'.";
                return RedirectToAction("Admin_Index");
            }

            try
            {
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Query to set IsActive = 0 (Safe Deletion/Deactivation)
                    string deleteQuery = @"
                        UPDATE products
                        SET IsActive = 0
                        WHERE Id = @Id;";

                    using (var cmd = new MySqlCommand(deleteQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
                TempData["Message"] = "Product successfully deactivated (safe-deleted).";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error deactivating product: " + ex.Message;
            }

            return RedirectToAction("Admin_Index");
        }

        // ------------------ REACTIVATE PRODUCT (GET/POST) ------------------
        // Since this is a simple, non-destructive action, we can use a GET request
        // or a simple modal (loaded by the existing JS `load-modal` mechanism)
        public ActionResult ReactivatePartial(int id)
        {
            Product product = GetProductById(id);

            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction("Admin_Index");
            }

            ViewBag.ProductName = product.Name;

            // Reusing a similar modal structure for confirmation
            return PartialView("_ReactivateProductModalContent", product);
        }

        // ------------------ REACTIVATE PRODUCT (POST) ------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Reactivate(int id)
        {
            try
            {
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Query to set IsActive back to 1
                    string reactivateQuery = @"
                        UPDATE products
                        SET IsActive = 1
                        WHERE Id = @Id;";

                    using (var cmd = new MySqlCommand(reactivateQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
                TempData["Message"] = "Product successfully reactivated and is now visible on the store.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error reactivating product: " + ex.Message;
            }

            return RedirectToAction("Admin_Index");
        }

        // ------------------ PERMANENTLY DELETE PRODUCT (GET) ------------------
        // Loads the modal content for permanent deletion confirmation
        public ActionResult PermanentlyDeletePartial(int id)
        {
            Product product = GetProductById(id);

            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction("Admin_Index");
            }

            // NOTE: You might want to add a check here too: if (HasActiveOrders(id)) 
            // to ensure no order history is lost if you delete a product that was only deactivated temporarily.
            // For now, we assume if it's inactive, it can be permanently deleted.

            ViewBag.ProductName = product.Name;
            ViewBag.CanDelete = true; // Assume true since it's already inactive

            // Reusing a similar modal structure for confirmation
            return PartialView("_PermanentlyDeleteProductModalContent", product);
        }

        // ------------------ PERMANENTLY DELETE PRODUCT (POST) ------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PermanentlyDelete(int id)
        {
            try
            {
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // ⭐ 1. Delete associated product_sizes first (Foreign Key constraint)
                    string deleteSizesQuery = @"
                        DELETE FROM product_sizes
                        WHERE Product_Id = @Id;";

                    using (var sizeCmd = new MySqlCommand(deleteSizesQuery, conn))
                    {
                        sizeCmd.Parameters.AddWithValue("@Id", id);
                        sizeCmd.ExecuteNonQuery();
                    }

                    // ⭐ 2. Delete the product record permanently
                    string deleteProductQuery = @"
                        DELETE FROM products
                        WHERE Id = @Id;";

                    using (var cmd = new MySqlCommand(deleteProductQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
                TempData["Message"] = "Product successfully and PERMANENTLY deleted from the database.";
            }
            catch (Exception ex)
            {
                // This is a critical action, report the error clearly.
                TempData["Error"] = "Error permanently deleting product: " + ex.Message;
            }

            return RedirectToAction("Admin_Index");
        }

        // ------------------ UPDATE PRODUCT (POST) ------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Product model, HttpPostedFileBase productImage, List<string> SizeNames, List<decimal> Prices)
        {
            // Set BasePrice to the price of the first size
            model.BasePrice = Prices != null && Prices.Count > 0 ? Prices[0] : 0;

            if (!ModelState.IsValid)
            {
                return PartialView("_EditProductModalContent", model);
            }

            string imageUrl = model.ImageUrl;

            // --- 1. HANDLE NEW IMAGE UPLOAD / OLD IMAGE DELETION ---
            if (productImage != null && productImage.ContentLength > 0)
            {
                try
                {
                    // 1a. Delete old image if it exists
                    if (!string.IsNullOrEmpty(model.ImageUrl))
                    {
                        string oldFullPath = Server.MapPath(model.ImageUrl);
                        if (System.IO.File.Exists(oldFullPath))
                        {
                            System.IO.File.Delete(oldFullPath);
                        }
                    }

                    // 1b. Define new unique file name
                    string extension = Path.GetExtension(productImage.FileName);
                    string baseName = Path.GetFileNameWithoutExtension(productImage.FileName);

                    // 🔥 FIX: Sanitize the base name to remove spaces and problematic URL characters
                    baseName = baseName.Replace(" ", "_").Replace("#", "").Replace("&", "").Replace("+", "");

                    string fileName = baseName + "_" + DateTime.Now.ToString("yymmddHHmmssfff") + extension;

                    // 1c. Define path and save the new file
                    string folder = Server.MapPath("~/Uploads/Images/");
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);

                    string fullPath = Path.Combine(folder, fileName);
                    productImage.SaveAs(fullPath);

                    // 1d. Set the new database URL
                    imageUrl = "/Uploads/Images/" + fileName;
                }
                catch (Exception ex)
                {
                    TempData["Message"] = "Image Update Failed! Reason: " + ex.Message;
                    return RedirectToAction("Admin_Index");
                }
            }


            // --- 2. UPDATE PRODUCT, DELETE OLD SIZES, INSERT NEW SIZES IN MYSQL ---
            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();

                // 2a. UPDATE PRODUCT MAIN TABLE (Includes IsFeatured and Category)
                string updateQuery = @"
                    UPDATE products
                    SET 
                        Name = @Name, 
                        BasePrice = @BasePrice, 
                        Stock = @Stock, 
                        Description = @Description, 
                        ImageUrl = @ImageUrl,
                        IsFeatured = @IsFeatured,
                        Category = @Category
                    WHERE 
                        Id = @Id;";

                using (var cmd = new MySqlCommand(updateQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", model.Id);
                    cmd.Parameters.AddWithValue("@Name", model.Name);
                    cmd.Parameters.AddWithValue("@BasePrice", model.BasePrice);
                    cmd.Parameters.AddWithValue("@Stock", model.Stock);

                    cmd.Parameters.AddWithValue("@Description", model.Description ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ImageUrl", imageUrl ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsFeatured", model.IsFeatured);
                    cmd.Parameters.AddWithValue("@Category", model.Category);

                    cmd.ExecuteNonQuery();
                }

                // -------------------------------------------------------------
                // ⭐ 2b/2c. SAFE DELETION AND INSERT LOGIC (REPLACEMENT) ⭐
                // -------------------------------------------------------------

                // Check if the product has ever been ordered
                bool productHasOrders = HasProductOrders(model.Id, connectionString);
                List<ProductSizeData> existingSizes = GetExistingProductSizes(model.Id, connectionString);

                if (productHasOrders)
                {
                    // Case 1: Product has orders. Allow UPDATES to price/name and INSERTION of new sizes, but block DELETION.

                    // Use a set to track which existing sizes were matched and updated by the form data
                    var updatedExistingSizeNames = new HashSet<string>();

                    if (SizeNames != null && Prices != null && SizeNames.Count == Prices.Count)
                    {
                        for (int i = 0; i < SizeNames.Count; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(SizeNames[i]) && Prices[i] > 0)
                            {
                                string newSizeName = SizeNames[i];
                                decimal newPrice = Prices[i];

                                // Find existing size by name (Since the form doesn't provide Size IDs, name is the only match key)
                                var existingSize = existingSizes.FirstOrDefault(s =>
                                    string.Equals(s.SizeName, newSizeName, StringComparison.OrdinalIgnoreCase));

                                if (existingSize != null)
                                {
                                    // Action: UPDATE existing size's price/name (This is safe due to ON UPDATE CASCADE)
                                    string updateSizeQuery = @"
                            UPDATE product_sizes
                            SET SizeName = @SizeName, Price = @Price
                            WHERE Id = @Id;";

                                    using (var sizeCmd = new MySqlCommand(updateSizeQuery, conn))
                                    {
                                        sizeCmd.Parameters.AddWithValue("@Id", existingSize.Id);
                                        sizeCmd.Parameters.AddWithValue("@SizeName", newSizeName);
                                        sizeCmd.Parameters.AddWithValue("@Price", newPrice);
                                        sizeCmd.ExecuteNonQuery();
                                    }
                                    updatedExistingSizeNames.Add(newSizeName); // Mark as handled
                                }
                                else
                                {
                                    // Action: INSERT new size (This is safe)
                                    string insertSizeQuery = @"
                            INSERT INTO product_sizes (Product_Id, SizeName, Price)
                            VALUES (@ProductId, @SizeName, @Price);";

                                    using (var sizeCmd = new MySqlCommand(insertSizeQuery, conn))
                                    {
                                        sizeCmd.Parameters.AddWithValue("@ProductId", model.Id);
                                        sizeCmd.Parameters.AddWithValue("@SizeName", newSizeName);
                                        sizeCmd.Parameters.AddWithValue("@Price", newPrice);
                                        sizeCmd.ExecuteNonQuery();
                                    }
                                }
                            }
                        }
                    }

                    // Check for sizes that were present in the database but are missing from the form data (i.e., attempted deletion).
                    var deletedSizes = existingSizes
                        .Where(s => !updatedExistingSizeNames.Contains(s.SizeName))
                        .ToList();

                    if (deletedSizes.Any())
                    {
                        // Alert the admin that the deletion was blocked, but don't crash the application.
                        string deletedSizeNames = string.Join(", ", deletedSizes.Select(s => s.SizeName));
                        TempData["Error"] = $"WARNING: You attempted to remove size(s) ({deletedSizeNames}), but deletion was blocked because this product has existing customer orders. These sizes remain in the database. Please set their stock or price to zero if they are no longer available to prevent them from being purchased.";
                    }
                }
                else
                {
                    // Case 2: Product has NO orders. Use the simple, fast DELETE ALL and INSERT ALL.

                    // DELETE ALL EXISTING PRODUCT SIZES
                    string deleteSizesQuery = "DELETE FROM product_sizes WHERE Product_Id = @ProductId;";
                    using (var deleteCmd = new MySqlCommand(deleteSizesQuery, conn))
                    {
                        deleteCmd.Parameters.AddWithValue("@ProductId", model.Id);
                        deleteCmd.ExecuteNonQuery();
                    }

                    // INSERT NEW PRODUCT SIZES
                    if (SizeNames != null && Prices != null && SizeNames.Count == Prices.Count)
                    {
                        for (int i = 0; i < SizeNames.Count; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(SizeNames[i]) && Prices[i] > 0)
                            {
                                string sizeInsertQuery = @"
                        INSERT INTO product_sizes
                            (Product_Id, SizeName, Price)
                        VALUES
                            (@ProductId, @SizeName, @Price);";

                                using (var sizeCmd = new MySqlCommand(sizeInsertQuery, conn))
                                {
                                    sizeCmd.Parameters.AddWithValue("@ProductId", model.Id);
                                    sizeCmd.Parameters.AddWithValue("@SizeName", SizeNames[i]);
                                    sizeCmd.Parameters.AddWithValue("@Price", Prices[i]);
                                    sizeCmd.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                }
                // -------------------------------------------------------------

            } // End of MySqlConnection using block

            TempData["Message"] = "Product successfully updated!";
            return RedirectToAction("Admin_Index");
        }


        private bool HasProductOrders(int productId, string connectionString)
        {
            string query = @"
        SELECT EXISTS (
            SELECT 1 
            FROM cojappdb.orderitems oi
            INNER JOIN cojappdb.product_sizes ps ON oi.SizeId = ps.Id
            WHERE ps.Product_Id = @ProductId
            LIMIT 1
        ) AS HasOrders;";

            using (var conn = new MySqlConnection(connectionString))
            using (var cmd = new MySqlCommand(query, conn))
            {
                conn.Open();
                cmd.Parameters.AddWithValue("@ProductId", productId);
                // ExecuteScalar returns the first column of the first row (the count/exists)
                return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
            }
        }

        private bool HasActiveOrders(int productId)
        {
            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();

                // Query to find if *any* order detail for this product has a status
                // that is NOT 'Delivered' AND NOT 'Canceled'.
                // Assuming:
                // 1. 'order_details' links products (Product_Id) to orders (OrderId).
                // 2. 'orders' table holds the Status field.
                string query = @"
                    SELECT 1
                    FROM orderitems od
                    INNER JOIN orders o ON od.OrderId = o.OrderId
                    WHERE od.ProductId = @ProductId
                    AND o.Status NOT IN ('Delivered', 'Canceled')
                    LIMIT 1;";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ProductId", productId);

                    // ExecuteScalar returns the first column of the first row, or null if no rows are returned.
                    return cmd.ExecuteScalar() != null;
                }
            }
        }

        private Product GetProductById(int id)
        {
            Product product = null;

            // This is a placeholder; you should implement actual database fetching here
            // Example structure:
            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT Id, Name, BasePrice, Stock, IsActive, Category FROM products WHERE Id = @Id;";
                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            product = new Product
                            {
                                Id = reader.GetInt32("Id"),
                                Name = reader.GetString("Name"),
                                // Map other necessary fields...
                                IsActive = reader.GetBoolean("IsActive")
                            };
                        }
                    }
                }
            }

            return product;
        }

        private List<ProductSizeData> GetExistingProductSizes(int productId, string connectionString)
        {
            var existingSizes = new List<ProductSizeData>();
            string query = "SELECT Id, SizeName, Price FROM product_sizes WHERE Product_Id = @ProductId;";

            using (var conn = new MySqlConnection(connectionString))
            using (var cmd = new MySqlCommand(query, conn))
            {
                conn.Open();
                cmd.Parameters.AddWithValue("@ProductId", productId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        existingSizes.Add(new ProductSizeData
                        {
                            Id = reader.GetInt32("Id"),
                            SizeName = reader.GetString("SizeName"),
                            Price = reader.GetDecimal("Price")
                        });
                    }
                }
            }
            return existingSizes;
        }


        // ------------------ CREATE PRODUCT (UPDATED) ------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Product model, HttpPostedFileBase productImage, List<string> SizeNames, List<decimal> Prices)
        {
            // Set BasePrice to the price of the first size
            model.BasePrice = Prices != null && Prices.Count > 0 ? Prices[0] : 0;

            if (!ModelState.IsValid)
            {
                return PartialView("_CreateProductModalContent", model);
            }

            string imageUrl = null;
            int newProductId = 0;

            // --- 1. IMAGE UPLOAD ---
            if (productImage != null && productImage.ContentLength > 0)
            {
                string extension = Path.GetExtension(productImage.FileName);
                string baseName = Path.GetFileNameWithoutExtension(productImage.FileName);

                // 🔥 FIX: Sanitize the base name to remove spaces and problematic URL characters
                baseName = baseName.Replace(" ", "_").Replace("#", "").Replace("&", "").Replace("+", "");

                string fileName = baseName + "_" + DateTime.Now.ToString("yymmddHHmmssfff") + extension;

                // Path is consolidated to ~/Uploads/Images/
                string folder = Server.MapPath("~/Uploads/Images/");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string fullPath = Path.Combine(folder, fileName);

                try
                {
                    productImage.SaveAs(fullPath);
                    imageUrl = "/Uploads/Images/" + fileName;
                }
                catch (Exception ex)
                {
                    TempData["Message"] = "Image Upload Failed! Reason: " + ex.Message + ". Please check folder permissions on: " + folder;
                    return RedirectToAction("Admin_Index");
                }
            }

            // --- 2. INSERT PRODUCT AND GET ID (Includes IsFeatured and Category) ---
            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();

                string insertQuery = @"
                    INSERT INTO products
                        (Name, BasePrice, Stock, Description, ImageUrl, IsActive, IsFeatured, Category)  
                    VALUES
                        (@Name, @BasePrice, 
                        @Stock, @Description, @ImageUrl, 1, @IsFeatured, @Category);
                    SELECT LAST_INSERT_ID();";

                using (var cmd = new MySqlCommand(insertQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Name", model.Name);
                    cmd.Parameters.AddWithValue("@BasePrice", model.BasePrice);
                    cmd.Parameters.AddWithValue("@Stock", model.Stock);

                    cmd.Parameters.AddWithValue("@Description", model.Description ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ImageUrl", imageUrl ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsFeatured", model.IsFeatured);
                    cmd.Parameters.AddWithValue("@Category", model.Category);

                    newProductId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                // --- 3. INSERT PRODUCT SIZES ---
                if (newProductId > 0 && SizeNames != null && Prices != null && SizeNames.Count == Prices.Count)
                {
                    for (int i = 0; i < SizeNames.Count; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(SizeNames[i]) && Prices[i] > 0)
                        {
                            string sizeInsertQuery = @"
                                INSERT INTO product_sizes
                                    (Product_Id, SizeName, Price)
                                VALUES
                                    (@ProductId, @SizeName, @Price);";

                            using (var sizeCmd = new MySqlCommand(sizeInsertQuery, conn))
                            {
                                sizeCmd.Parameters.AddWithValue("@ProductId", newProductId);
                                sizeCmd.Parameters.AddWithValue("@SizeName", SizeNames[i]);
                                sizeCmd.Parameters.AddWithValue("@Price", Prices[i]);
                                sizeCmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }

            TempData["Message"] = "Product successfully added and sizes saved!";
            return RedirectToAction("Admin_Index");
        }
    }
}