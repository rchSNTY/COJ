using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using coj.Models;
using MySql.Data.MySqlClient;
using System.Configuration;
using System.Web;
using System.Web.Security;
using System.Net.Mail;
using System.Net;
using BCrypt.Net;        

namespace coj.Controllers
{
    public class HomeController : Controller
    {
        // Add database connection string
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["cojappdb"].ConnectionString;

        // Data Access Method (No changes needed here for login system)
        private List<Product> GetProductsWithSizes()
        {
            var products = new List<Product>();

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                // Select only products that are FEATURED (IsFeatured = 1)
                // You may want to also filter by IsActive = 1
                string query =
                    "SELECT * FROM products WHERE IsFeatured = 1 AND IsActive = 1 ORDER BY Id;";

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

            // Load sizes for the fetched products
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

        // ====================================================
        // Get Product Details for Modal
        // ====================================================
        [HttpGet]
        public JsonResult GetProductDetails(int id)
        {
            try
            {
                Product product = null;

                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // 1. Fetch Product (includes Stock)
                    string pQuery = "SELECT Id, Name, Description, ImageUrl, Stock FROM products WHERE Id = @id";
                    using (var cmd = new MySqlCommand(pQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                product = new Product
                                {
                                    Id = reader.GetInt32("Id"),
                                    Name = reader.GetString("Name"),
                                    Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? "" : reader.GetString("Description"),
                                    ImageUrl = reader.IsDBNull(reader.GetOrdinal("ImageUrl")) ? "" : reader.GetString("ImageUrl"),
                                    Stock = reader.GetInt32("Stock"),
                                    ProductSizes = new List<ProductSize>()
                                };
                            }
                        }
                    }

                    if (product == null)
                        return Json(new { success = false, message = "Product not found" }, JsonRequestBehavior.AllowGet);

                    // 2. Fetch Sizes
                    string sQuery = "SELECT Id, SizeName, Price FROM product_sizes WHERE Product_Id = @pid ORDER BY Price ASC";
                    using (var cmd = new MySqlCommand(sQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@pid", id);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                product.ProductSizes.Add(new ProductSize
                                {
                                    Id = reader.GetInt32("Id"),
                                    SizeName = reader.GetString("SizeName"),
                                    Price = reader.GetDecimal("Price")
                                });
                            }
                        }
                    }

                    // 3. Prepare clean JSON result
                    var cleanData = new
                    {
                        Id = product.Id,
                        Name = product.Name,
                        Description = product.Description,
                        ImageUrl = product.ImageUrl,
                        Stock = product.Stock,

                        ProductSizes = product.ProductSizes.Select(s => new
                        {
                            Id = s.Id,
                            SizeName = s.SizeName,
                            Price = s.Price
                        })
                    };

                    return Json(new { success = true, data = cleanData }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }


        // ====================================================


        // ====================================================
        // Cart Management Methods (Add, Get, Remove, Update)
        // ====================================================

        // ----------------------------------------------------
        // HELPER: Get Current User ID (Returns 0 if Guest)
        // ----------------------------------------------------
        private int CurrentUserId
        {
            get
            {
                // If not authenticated (cookie expired, logged out), we're done.
                if (!User.Identity.IsAuthenticated)
                {
                    return 0;
                }

                // 1. Try to read the ID from the Session (Fast path, works for subsequent requests)
                if (Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int sessionUserId) && sessionUserId > 0)
                {
                    return sessionUserId;
                }

                // 2. FALLBACK: Session lost (server restarted). 
                // We use the persistent Forms Identity (which stores the user's email) to reload the ID.
                string userEmail = User.Identity.Name;

                if (string.IsNullOrEmpty(userEmail))
                {
                    // Should not happen if Forms Auth is working correctly
                    return 0;
                }

                int userId = 0;
                // Lookup user ID from DB using the email
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    // Fetch Id and other required session data (FirstName, IsAdmin)
                    string query = "SELECT Id, FirstName, IsAdmin FROM users WHERE Email = @Email LIMIT 1";
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Email", userEmail);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                userId = reader.GetInt32("Id");

                                // CRITICAL: Re-set the Session variables for future requests
                                Session["UserId"] = userId;
                                Session["UserEmail"] = userEmail;
                                Session["UserName"] = reader.GetString("FirstName");
                                Session["IsAdmin"] = reader.GetBoolean("IsAdmin");
                            }
                        }
                    }
                }

                return userId;
            }
        }

        public JsonResult CheckUserStatus()
        {
            bool isLoggedIn = User.Identity.IsAuthenticated;

            return Json(new { isLoggedIn = isLoggedIn }, JsonRequestBehavior.AllowGet);
        }

        private int GetCurrentUserId()
        {
            if (User.Identity.IsAuthenticated)
            {
                var authCookie = Request.Cookies[FormsAuthentication.FormsCookieName];
                if (authCookie == null) return 0;

                try
                {
                    var ticket = FormsAuthentication.Decrypt(authCookie.Value);

                    int userId;
                    // Use TryParse for safe conversion: checks if parsing is successful
                    if (int.TryParse(ticket.UserData, out userId))
                    {
                        return userId; // Success
                    }
                }
                catch
                {
                    // Ignore decryption or parsing error and return 0
                }
            }
            return 0; // Not authenticated or ID could not be retrieved
        }

        private User GetUserById(int userId)
        {
            User user = null;
            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT Id, FirstName, LastName, Email, Address, PhoneNumber FROM users WHERE Id = @Id LIMIT 1";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            user = new User
                            {
                                Id = reader.GetInt32("Id"),
                                FirstName = reader.GetString("FirstName"),
                                LastName = reader.GetString("LastName"),
                                Email = reader.GetString("Email"),
                                Address = reader.IsDBNull(reader.GetOrdinal("Address")) ? string.Empty : reader.GetString("Address"),
                                PhoneNumber = reader.IsDBNull(reader.GetOrdinal("PhoneNumber")) ? string.Empty : reader.GetString("PhoneNumber")
                            };
                        }
                    }
                }
            }
            return user;
        }

        // --- NEW HELPER: Fetch Full User Object ---
        private coj.Models.User GetLoggedInUser()
        {
            coj.Models.User user = null;
            if (User.Identity.IsAuthenticated)
            {
                string email = User.Identity.Name;
                // The SQL logic below is based on your Profile() action method
                string query = @"SELECT Id, FirstName, LastName, Email, Address, PhoneNumber FROM users WHERE Email = @email;";

                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@email", email);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                user = new coj.Models.User
                                {
                                    Id = reader.GetInt32("Id"),
                                    FirstName = reader.GetString("FirstName"),
                                    LastName = reader.GetString("LastName"),
                                    Email = reader.GetString("Email"),
                                    Address = reader.IsDBNull(reader.GetOrdinal("Address")) ? string.Empty : reader.GetString("Address"),
                                    PhoneNumber = reader.IsDBNull(reader.GetOrdinal("PhoneNumber")) ? string.Empty : reader.GetString("PhoneNumber")
                                };
                            }
                        }
                    }
                }
            }
            return user;
        }

        // Helper to load Stock data into the CartItem objects before rendering the view
        private void LoadCartItemsStock(coj.Models.Cart cart)
        {
            if (cart == null || !cart.Items.Any())
            {
                return;
            }

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();

                // Collect all unique ProductIds in the cart
                var uniqueProductIds = cart.Items.Select(i => i.ProductId).Distinct().ToList();

                // Convert the list of IDs into a comma-separated string for the SQL 'IN' clause
                string idList = string.Join(",", uniqueProductIds);

                // Query to get Stock from the products table for all unique IDs at once
                string query = $"SELECT Id, Stock FROM products WHERE Id IN ({idList});";

                var productStocks = new Dictionary<int, int>();

                using (var cmd = new MySqlCommand(query, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int productId = reader.GetInt32("Id");
                        // Check for NULL or assign 0 if not a valid number
                        int stock = reader.IsDBNull(reader.GetOrdinal("Stock")) ? 0 : reader.GetInt32("Stock");
                        productStocks[productId] = stock;
                    }
                }

                // Apply the fetched stock to the corresponding CartItem objects
                foreach (var item in cart.Items)
                {
                    if (productStocks.ContainsKey(item.ProductId))
                    {
                        item.Stock = productStocks[item.ProductId];
                    }
                    else
                    {
                        item.Stock = 0; // Default to 0 if product was somehow deleted
                    }
                }
            }
        }

        // ----------------------------------------------------
        // HELPER: Get Session Cart (For Guests)
        // ----------------------------------------------------
        private Cart GetSessionCart()
        {
            Cart cart = Session["Cart"] as Cart;
            if (cart == null)
            {
                cart = new Cart();
                Session["Cart"] = cart;
            }
            return cart;
        }

        // ====================================================
        // 1. ADD TO CART (HYBRID: DB or SESSION)
        // ====================================================
        [HttpPost]
        public JsonResult AddToCart(int productId = 0, int sizeId = 0, int quantity = 0)
        {
            try
            {
                if (productId <= 0 || sizeId <= 0 || quantity <= 0)
                {
                    return Json(new { success = false, message = "Invalid data.", maxStock = 0 });
                }

                int userId = CurrentUserId;

                // BRANCH 1: LOGGED IN USER -> Save to Database
                if (userId > 0)
                {
                    return AddItemToDatabaseCart(userId, productId, sizeId, quantity);
                }
                // BRANCH 2: GUEST USER -> Save to Session
                else
                {
                    return AddItemToSessionCart(productId, sizeId, quantity);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // --- PRIVATE HELPER: Add to Database (For Logged In Users) ---
        private JsonResult AddItemToDatabaseCart(int userId, int productId, int sizeId, int quantity)
        {
            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();

                // 1. Get Product Details & Stock
                string productDetailQuery = @"SELECT p.Name, p.Stock FROM products p JOIN product_sizes s ON p.Id = s.Product_Id WHERE p.Id = @pid AND s.Id = @sid";
                int productStock = 0;
                string productName = "";

                using (var cmdDetail = new MySqlCommand(productDetailQuery, conn))
                {
                    cmdDetail.Parameters.AddWithValue("@pid", productId);
                    cmdDetail.Parameters.AddWithValue("@sid", sizeId);
                    using (var reader = cmdDetail.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            productStock = reader.GetInt32("Stock");
                            productName = reader.GetString("Name");
                        }
                        else return Json(new { success = false, message = "Product not found." });
                    }
                }

                // 2. Check Current Cart Stock (Logic: DB Total + New Qty)
                int currentQtyInCart = 0;
                int existingQtyOfThisSize = 0;
                string checkCartQuery = "SELECT ProductSizeId, Quantity FROM user_carts WHERE UserId = @UserId AND ProductId = @ProductId";

                using (var cmdCheck = new MySqlCommand(checkCartQuery, conn))
                {
                    cmdCheck.Parameters.AddWithValue("@UserId", userId);
                    cmdCheck.Parameters.AddWithValue("@ProductId", productId);
                    using (var reader = cmdCheck.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int q = reader.GetInt32("Quantity");
                            currentQtyInCart += q;
                            if (reader.GetInt32("ProductSizeId") == sizeId) existingQtyOfThisSize = q;
                        }
                    }
                }

                int qtyOfOtherSizes = currentQtyInCart - existingQtyOfThisSize;
                int newTotal = qtyOfOtherSizes + existingQtyOfThisSize + quantity;

                if (newTotal > productStock)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Stock limit reached. Limit is {productStock}. You have {currentQtyInCart} in cart.",
                        maxStock = productStock
                    });
                }

                // 3. Upsert to DB
                string upsertQuery = @"
                                    INSERT INTO user_carts (UserId, ProductId, ProductSizeId, Quantity)
                                    VALUES (@UserId, @ProductId, @ProductSizeId, @Qty)
                                    ON DUPLICATE KEY UPDATE Quantity = Quantity + @Qty"; // This is the key operation

                using (var cmdUpsert = new MySqlCommand(upsertQuery, conn))
                {
                    cmdUpsert.Parameters.AddWithValue("@UserId", userId);
                    cmdUpsert.Parameters.AddWithValue("@ProductId", productId);
                    cmdUpsert.Parameters.AddWithValue("@ProductSizeId", sizeId);
                    cmdUpsert.Parameters.AddWithValue("@Qty", quantity);
                    cmdUpsert.ExecuteNonQuery();
                }

                // 4. Get Final Count
                int finalCartItemCount = 0;
                string countQuery = "SELECT COALESCE(SUM(Quantity), 0) FROM user_carts WHERE UserId = @UserId";
                using (var cmdCount = new MySqlCommand(countQuery, conn))
                {
                    cmdCount.Parameters.AddWithValue("@UserId", userId);
                    finalCartItemCount = Convert.ToInt32(cmdCount.ExecuteScalar());
                }

                return Json(new { success = true, cartItemCount = finalCartItemCount, maxStock = productStock, message = "Added to persistent cart." });
            }
        }

        // --- PRIVATE HELPER: Add to Session (For Guests) ---
        private JsonResult AddItemToSessionCart(int productId, int sizeId, int quantity)
        {
            // We still need DB to check Name/Price/Stock, even for Session cart
            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                string query = @"SELECT p.Name, p.ImageUrl, p.Stock, s.SizeName, s.Price  
                                 FROM products p JOIN product_sizes s ON p.Id = s.Product_Id 
                                 WHERE p.Id = @pid AND s.Id = @sid";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@pid", productId);
                    cmd.Parameters.AddWithValue("@sid", sizeId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int productStock = reader.GetInt32("Stock");
                            Cart cart = GetSessionCart();

                            // Calculate Session Totals
                            int totalInSession = cart.Items.Where(x => x.ProductId == productId).Sum(x => x.Quantity);

                            if ((totalInSession + quantity) > productStock)
                            {
                                return Json(new { success = false, message = $"Stock limit reached. Limit: {productStock}. You have {totalInSession} in cart.", maxStock = productStock });
                            }

                            // Add/Update Session
                            var existing = cart.Items.FirstOrDefault(x => x.ProductId == productId && x.SizeId == sizeId);
                            if (existing != null)
                            {
                                existing.Quantity += quantity;
                            }
                            else
                            {
                                cart.Items.Add(new CartItem
                                {
                                    ProductId = productId,
                                    ProductName = reader.GetString("Name"),
                                    ProductImageUrl = reader.IsDBNull(reader.GetOrdinal("ImageUrl")) ? "" : reader.GetString("ImageUrl"),
                                    SizeId = sizeId,
                                    SizeName = reader.GetString("SizeName"),
                                    Price = reader.GetDecimal("Price"),
                                    Quantity = quantity
                                });
                            }

                            return Json(new { success = true, cartItemCount = cart.TotalItems, maxStock = productStock, message = "Added to session cart." });
                        }
                    }
                }
            }
            return Json(new { success = false, message = "Product not found." });
        }

        // ====================================================
        // 2. GET CART CONTENT (HYBRID)
        // ====================================================
        [HttpGet]
        public ActionResult GetCartContent()
        {
            int userId = CurrentUserId;
            List<coj.Models.CartItem> cartItems = new List<coj.Models.CartItem>();

            // BRANCH 1: LOGGED IN -> Read DB
            if (userId > 0)
            {
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT uc.ProductId, uc.ProductSizeId AS SizeId, uc.Quantity, p.Name, p.ImageUrl, ps.SizeName, ps.Price
                        FROM user_carts uc
                        JOIN products p ON uc.ProductId = p.Id
                        JOIN product_sizes ps ON uc.ProductSizeId = ps.Id
                        WHERE uc.UserId = @UserId";

                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                cartItems.Add(new coj.Models.CartItem
                                {
                                    ProductId = reader.GetInt32("ProductId"),
                                    SizeId = reader.GetInt32("SizeId"),
                                    Quantity = reader.GetInt32("Quantity"),
                                    ProductName = reader.GetString("Name"),
                                    ProductImageUrl = reader.IsDBNull(reader.GetOrdinal("ImageUrl")) ? "" : reader.GetString("ImageUrl"),
                                    SizeName = reader.GetString("SizeName"),
                                    Price = reader.GetDecimal("Price")
                                });
                            }
                        }
                    }
                }
            }
            // BRANCH 2: GUEST -> Read Session
            else
            {
                Cart sessionCart = GetSessionCart();
                cartItems = sessionCart.Items;
            }
            var cartViewModel = new Cart { Items = cartItems };
            return PartialView("_CartSidebar", cartItems);
        }

        // ====================================================
        // 3. GET CART COUNT (HYBRID)
        // ====================================================
        [HttpGet]
        public JsonResult GetCartCount()
        {
            int userId = CurrentUserId;
            int count = 0;

            if (userId > 0)
            {
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand("SELECT COALESCE(SUM(Quantity), 0) FROM user_carts WHERE UserId = @UserId", conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value) count = Convert.ToInt32(result);
                    }
                }
            }
            else
            {
                count = GetSessionCart().TotalItems;
            }

            return Json(new { cartItemCount = count }, JsonRequestBehavior.AllowGet);
        }

        // ====================================================
        // 4. REMOVE FROM CART (HYBRID)
        // ====================================================
        [HttpPost]
        public JsonResult RemoveFromCart(int productId, int sizeId)
        {
            int userId = CurrentUserId;

            if (userId > 0) // DB Remove (Signed-in User)
            {
                try
                {
                    int rowsAffected = 0;
                    using (var conn = new MySqlConnection(connectionString))
                    {
                        conn.Open();
                        using (var cmd = new MySqlCommand("DELETE FROM user_carts WHERE UserId=@uid AND ProductId=@pid AND ProductSizeId=@sid", conn))
                        {
                            cmd.Parameters.AddWithValue("@uid", userId);
                            cmd.Parameters.AddWithValue("@pid", productId);
                            cmd.Parameters.AddWithValue("@sid", sizeId);

                            rowsAffected = cmd.ExecuteNonQuery();
                        }
                    }

                    // 1. CRITICAL FIX: Return a JSON object with success: true
                    // The item is gone from the database, so we signal success to the client.
                    if (rowsAffected >= 0)
                    {
                        return Json(new { success = true, message = "Item removed successfully." });
                    }
                }
                catch (Exception ex)
                {
                    // If the database fails, return success: false to trigger the alert intentionally.
                    System.Diagnostics.Debug.WriteLine($"DB Cart Removal Error: {ex.Message}");
                    return Json(new { success = false, message = "Database error during removal." });
                }
            }
            else // Session Remove (Guest User)
            {
                var cart = GetSessionCart();
                var item = cart.Items.FirstOrDefault(x => x.ProductId == productId && x.SizeId == sizeId);
                if (item != null) cart.Items.Remove(item);

                // This already returns success: true, which is correct.
                return Json(new { success = true, cartItemCount = cart.TotalItems });
            }

            return Json(new { success = false, message = "Unknown error occurred." });
        }

        // ====================================================
        // 5. LOGIN WITH MERGE LOGIC (CRITICAL STEP)
        // ====================================================
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT Id, Email, PasswordHash, FirstName, IsAdmin, IsActive FROM users WHERE Email = @Email LIMIT 1";

                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Email", model.Email);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string storedHash = reader.GetString("PasswordHash");
                                int userId = reader.GetInt32("Id");
                                string userName = reader.GetString("FirstName");
                                bool isAdmin = reader.GetBoolean("IsAdmin");
                                bool isActive = reader.GetBoolean("IsActive");

                                if (BCrypt.Net.BCrypt.Verify(model.Password, storedHash))
                                {
                                    if (isActive == false)
                                    {
                                        ModelState.AddModelError("", "Account deactivated.");
                                        return View(model);
                                    }

                                    // --- AUTHENTICATION SUCCESS ---
                                    string roles = isAdmin ? "Admin" : "Customer";
                                    FormsAuthenticationTicket ticket = new FormsAuthenticationTicket(1, model.Email, DateTime.Now, DateTime.Now.AddDays(7), true, userId.ToString());
                                    string encTicket = FormsAuthentication.Encrypt(ticket);
                                    Response.Cookies.Add(new HttpCookie(FormsAuthentication.FormsCookieName, encTicket));

                                    // 3. Set Session
                                    Session["UserId"] = userId;
                                    Session["UserEmail"] = model.Email;
                                    Session["UserName"] = userName;
                                    Session["IsAdmin"] = isAdmin;

                                    // ============================================================
                                    // ⭐ MIGRATE SESSION CART TO DATABASE
                                    // ============================================================
                                    // We temporarily close the reader to allow new DB operations inside the Migration
                                    reader.Close();

                                    MigrateSessionCartToDatabase(userId); // Call the helper method below

                                    // ============================================================

                                    if (isAdmin) return RedirectToAction("Admin_Dashboard", "Dashboard");
                                    return RedirectToAction("Index", "Home");
                                }
                            }
                            ModelState.AddModelError("", "Invalid email or password.");
                        }
                    }
                }
            }
            return View(model);
        }

        // Helper to Move Session Items to DB
        private void MigrateSessionCartToDatabase(int userId)
        {
            Cart sessionCart = Session["Cart"] as Cart;
            if (sessionCart != null && sessionCart.Items.Count > 0)
            {
                // Loop through session items and add them to the DB
                foreach (var item in sessionCart.Items)
                {
                    // We reuse the helper method we created earlier. 
                    // This handles stock checks and upserts automatically!
                    AddItemToDatabaseCart(userId, item.ProductId, item.SizeId, item.Quantity);
                }

                // Clear the session cart now that items are in the DB
                Session["Cart"] = null;
            }
        }

        // ----------------------------------------------------
        // HELPER: Get Current Cart (Consolidated DB/Session fetch)
        // ----------------------------------------------------
        private Cart GetCurrentCart()
        {
            // ASSUMPTION: You have a helper property/method named CurrentUserId to get the logged-in user's ID.
            int userId = CurrentUserId;

            if (userId > 0) // Logged-in user (Database Cart)
            {
                var cart = new Cart { Items = new List<CartItem>() };

                // SQL query to join user_carts with product and size details
                string query = @"
            SELECT 
                uc.ProductId, uc.ProductSizeId, uc.Quantity,
                p.Name AS ProductName, p.ImageUrl,
                ps.SizeName, ps.Price
            FROM user_carts uc
            JOIN products p ON uc.ProductId = p.Id
            JOIN product_sizes ps ON uc.ProductSizeId = ps.Id
            WHERE uc.UserId = @UserId;";

                try
                {
                    using (var conn = new MySqlConnection(connectionString))
                    {
                        conn.Open();
                        using (var cmd = new MySqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    cart.Items.Add(new CartItem
                                    {
                                        ProductId = reader.GetInt32("ProductId"),
                                        SizeId = reader.GetInt32("ProductSizeId"),
                                        ProductName = reader.GetString("ProductName"),
                                        SizeName = reader.GetString("SizeName"),
                                        Price = reader.GetDecimal("Price"),
                                        Quantity = reader.GetInt32("Quantity"),
                                        ImageUrl = reader.GetString("ImageUrl")
                                    });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error fetching DB cart for checkout: {ex.Message}");
                    return new Cart(); // Return empty cart on error
                }
                return cart;
            }
            else // Guest user (Session Cart)
            {
                // ASSUMPTION: You have a GetSessionCart() method that reads Cart data from Session
                // If you haven't implemented this, return new Cart() for now.
                return GetSessionCart();
            }
        }

        // -----------------------------------------------------------------
        // 1. HELPER: Update Database Cart (Handles DB writes for logged-in users)
        // -----------------------------------------------------------------
        private void UpdateDatabaseCart(coj.Models.Cart cart, int userId)
        {
            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                var transaction = conn.BeginTransaction();
                try
                {
                    // A. Delete ALL existing cart items for the user
                    string deleteQuery = "DELETE FROM user_carts WHERE UserId = @UserId;";
                    using (var cmd = new MySqlCommand(deleteQuery, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.ExecuteNonQuery();
                    }

                    // B. Insert ALL items from the updated in-memory cart
                    string insertQuery = @"
                INSERT INTO user_carts (UserId, ProductId, ProductSizeId, Quantity)
                VALUES (@UserId, @ProductId, @ProductSizeId, @Quantity);";

                    foreach (var item in cart.Items)
                    {
                        using (var cmd = new MySqlCommand(insertQuery, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            cmd.Parameters.AddWithValue("@ProductId", item.ProductId);
                            cmd.Parameters.AddWithValue("@ProductSizeId", item.SizeId);
                            cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    System.Diagnostics.Debug.WriteLine($"Error updating database cart: {ex.Message}");
                    throw;
                }
            }
        }

        // -----------------------------------------------------------------
        // 2. HELPER: Central Save Method
        // -----------------------------------------------------------------
        private void SaveCart(coj.Models.Cart cart)
        {
            int userId = CurrentUserId;

            if (userId > 0)
            {
                UpdateDatabaseCart(cart, userId);
            }
            else
            {
                Session["Cart"] = cart;
            }
        }

        // -----------------------------------------------------------------
        // 3. AJAX ACTION: Remove Item
        // -----------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult RemoveCartItem(int productId, int sizeId)
        {
            try
            {
                var cart = GetCurrentCart();

                var itemToRemove = cart.Items
                    .FirstOrDefault(i => i.ProductId == productId && i.SizeId == sizeId);

                if (itemToRemove != null)
                {
                    cart.Items.Remove(itemToRemove);
                    // ⭐ FIX: Removed RecalculateCart(). SubTotal updates automatically.
                    SaveCart(cart);
                }

                // When we access cart.SubTotal here, it automatically recalculates based on the remaining items.
                return Json(new { success = true, newSubTotal = cart.SubTotal, cartItemCount = cart.Items.Count });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing item: {ex.Message}");
                return Json(new { success = false, message = "Error removing item." });
            }
        }

        // -----------------------------------------------------------------
        // 4. AJAX ACTION: Update Quantity (FINAL FIX with 'products.Stock' check)
        // -----------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult UpdateCartQuantity(int productId, int sizeId, int newQuantity)
        {
            if (newQuantity < 1)
            {
                return Json(new { success = false, message = "Quantity must be at least 1." });
            }

            try
            {
                // --- 1. GET MAX STOCK FOR THIS ITEM ---
                int maxStock = 0;

                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // ⭐ CORRECTED SQL: Selecting 'Stock' from the 'products' table
                    string stockQuery = @"
                SELECT Stock 
                FROM products 
                WHERE Id = @ProductId;";

                    using (var cmd = new MySqlCommand(stockQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@ProductId", productId);

                        object result = cmd.ExecuteScalar();

                        if (result != null && result != DBNull.Value)
                        {
                            if (int.TryParse(result.ToString(), out int stockValue))
                            {
                                maxStock = stockValue;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("DB ERROR: Stock value is not a valid integer.");
                                return Json(new { success = false, message = "Database error: Product stock data is invalid." });
                            }
                        }
                        else
                        {
                            return Json(new { success = false, message = "Product not found. Cannot update quantity." });
                        }
                    }
                }

                // --- 2. STOCK VALIDATION (Server-side check) ---
                if (newQuantity > maxStock)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Maximum stock for this item is {maxStock}.",
                        maxStock = maxStock
                    });
                }

                // --- 3. UPDATE CART (If stock check passes) ---
                var cart = GetCurrentCart();

                var itemToUpdate = cart.Items
                    .FirstOrDefault(i => i.ProductId == productId && i.SizeId == sizeId);

                if (itemToUpdate != null)
                {
                    itemToUpdate.Quantity = newQuantity;
                    SaveCart(cart);

                    return Json(new
                    {
                        success = true,
                        cartItemCount = cart.Items.Count,
                        newSubTotal = cart.SubTotal,
                        newItemTotal = itemToUpdate.Quantity * itemToUpdate.Price
                    });
                }

                return Json(new { success = false, message = "Item not found in the current cart." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FATAL ERROR in UpdateCartQuantity: {ex.Message}");
                return Json(new { success = false, message = "An internal server error occurred. Check the debug log." });
            }
        }



        // ----------------------------------------------------
        // 7. PLACE ORDER (Transaction-based)
        // ----------------------------------------------------
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public JsonResult PlaceOrder(FormCollection form) // Return type is JsonResult for AJAX
        {
            var user = GetLoggedInUser();
            var cart = GetCurrentCart();

            if (user == null || cart == null || !cart.Items.Any())
            {
                return Json(new { success = false, message = "Order failed. Cart is empty or user is not logged in." });
            }

            // Capture form data with robust fallbacks
            string recipientName = form["recipientName"];
            string deliveryAddress = form["deliveryAddress"];
            string phoneNumber = form["phoneNumber"];
            string paymentMethod = form["PaymentMethod"];

            // Safety Fallbacks: If form data is missing, use the user's saved profile data.
            string userFullName = $"{user.FirstName} {user.LastName}";
            recipientName = string.IsNullOrEmpty(recipientName) ? userFullName : recipientName;
            deliveryAddress = string.IsNullOrEmpty(deliveryAddress) ? (user.Address ?? "No Address Provided") : deliveryAddress;
            phoneNumber = string.IsNullOrEmpty(phoneNumber) ? (user.PhoneNumber ?? "0000000000") : phoneNumber;
            paymentMethod = string.IsNullOrEmpty(paymentMethod) ? "Cash" : paymentMethod;


            decimal shippingFee = 50.00m;
            decimal subtotal = cart.SubTotal;
            decimal totalAmount = subtotal + shippingFee;

            MySqlConnection conn = null;
            MySqlTransaction transaction = null;
            int orderId = 0;

            try
            {
                conn = new MySqlConnection(connectionString);
                conn.Open();

                transaction = conn.BeginTransaction();

                // 1. SAVE THE MAIN ORDER (Orders table)
                string orderQuery = @"
        INSERT INTO Orders (UserId, OrderDate, RecipientName, DeliveryAddress, PhoneNumber, PaymentMethod, ShippingFee, SubTotal, TotalAmount, Status)
        VALUES (@UserId, NOW(), @RecipientName, @DeliveryAddress, @PhoneNumber, @PaymentMethod, @ShippingFee, @SubTotal, @TotalAmount, 'Pending');
        SELECT LAST_INSERT_ID();";

                using (var orderCmd = new MySqlCommand(orderQuery, conn, transaction))
                {
                    orderCmd.Parameters.AddWithValue("@UserId", user.Id);
                    orderCmd.Parameters.AddWithValue("@RecipientName", recipientName);
                    orderCmd.Parameters.AddWithValue("@DeliveryAddress", deliveryAddress);
                    orderCmd.Parameters.AddWithValue("@PhoneNumber", phoneNumber);
                    orderCmd.Parameters.AddWithValue("@PaymentMethod", paymentMethod);
                    orderCmd.Parameters.AddWithValue("@ShippingFee", shippingFee);
                    orderCmd.Parameters.AddWithValue("@SubTotal", subtotal);
                    orderCmd.Parameters.AddWithValue("@TotalAmount", totalAmount);

                    orderId = Convert.ToInt32(orderCmd.ExecuteScalar());
                }

                // 2. SAVE THE ORDER ITEMS AND REDUCE STOCK
                string itemQuery = @"
            INSERT INTO OrderItems (OrderId, ProductId, SizeId, Name, SizeName, Price, Quantity)
            VALUES (@OrderId, @ProductId, @SizeId, @Name, @SizeName, @Price, @Quantity);";

                // ⭐ NEW: Query to reduce stock in the products table
                string stockUpdateQuery = @"
            UPDATE products 
            SET Stock = Stock - @Quantity 
            WHERE Id = @ProductId AND Stock >= @Quantity;";

                foreach (var item in cart.Items)
                {
                    // A. Insert Order Item (Your existing logic)
                    using (var itemCmd = new MySqlCommand(itemQuery, conn, transaction))
                    {
                        itemCmd.Parameters.AddWithValue("@OrderId", orderId);
                        itemCmd.Parameters.AddWithValue("@ProductId", item.ProductId);
                        itemCmd.Parameters.AddWithValue("@SizeId", item.SizeId);
                        itemCmd.Parameters.AddWithValue("@Name", item.ProductName);
                        itemCmd.Parameters.AddWithValue("@SizeName", item.SizeName);
                        itemCmd.Parameters.AddWithValue("@Price", item.Price);
                        itemCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                        itemCmd.ExecuteNonQuery();
                    }

                    // B. ⭐ NEW: Reduce Stock Logic
                    // This executes inside the same transaction. If it fails (e.g., not enough stock), everything rolls back.
                    using (var stockCmd = new MySqlCommand(stockUpdateQuery, conn, transaction))
                    {
                        stockCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                        stockCmd.Parameters.AddWithValue("@ProductId", item.ProductId);

                        int rowsAffected = stockCmd.ExecuteNonQuery();

                        // If rowsAffected is 0, it means the WHERE clause failed (Stock < Quantity)
                        if (rowsAffected == 0)
                        {
                            // Manually trigger a rollback by throwing an exception
                            throw new Exception($"Insufficient stock for product '{item.ProductName}'. Order cancelled.");
                        }
                    }
                }

                // 3. Delete the cart contents from the 'user_carts' table (for persistence)
                string deleteCartQuery = "DELETE FROM user_carts WHERE UserId = @UserId";
                using (var deleteCmd = new MySqlCommand(deleteCartQuery, conn, transaction))
                {
                    deleteCmd.Parameters.AddWithValue("@UserId", user.Id);
                    deleteCmd.ExecuteNonQuery();
                }

                // Commit and clear session cart
                transaction.Commit();
                Session["Cart"] = null;

                // Return success JSON
                return Json(new { success = true, orderId = orderId, redirectUrl = Url.Action("OrderHistory", "Home") });
            }
            catch (Exception ex)
            {
                // Attempt to roll back the transaction on error
                if (transaction != null)
                {
                    try { transaction.Rollback(); } catch { /* Ignore rollback error */ }
                }

                // ⭐ CRITICAL for debugging: Return the detailed database error message
                System.Diagnostics.Debug.WriteLine($"Order placement failed: {ex.Message}");
                return Json(new { success = false, message = "Database error during order processing: " + ex.Message });
            }
            finally
            {
                if (conn != null && conn.State == System.Data.ConnectionState.Open)
                {
                    conn.Close();
                }
            }
        }

        // Inside HomeController.cs

        private Product GetProductById(int id)
        {
            Product product = null;

            string query = @"
        SELECT Id, Name, BasePrice, Description, ImageUrl, IsActive
        FROM products 
        WHERE Id = @Id;";

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
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
                                // ⭐ FIX: Use the actual column name BasePrice
                                BasePrice = reader.GetDecimal("BasePrice"),

                                Description = reader.GetString("Description"),

                                // ⭐ FIX: Use the actual column name ImageUrl
                                ImageUrl = reader.GetString("ImageUrl"),

                                IsActive = reader.GetBoolean("IsActive")
                            };
                        }
                    }
                }
            }
            return product;
        }

        // Inside HomeController.cs

        [AllowAnonymous]
        public JsonResult SearchProducts(string searchTerm)
        {
            try
            {
                var searchResults = new List<ProductSearchViewModel>();

                if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 2)
                {
                    return Json(searchResults, JsonRequestBehavior.AllowGet);
                }

                string searchPattern = "%" + searchTerm + "%";

                string query = @"
        SELECT Id, Name, BasePrice, ImageUrl, Description
        FROM products 
        WHERE IsActive = 1 AND Name LIKE @SearchPattern 
        ORDER BY Name 
        LIMIT 10;";

                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@SearchPattern", searchPattern);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string fullDesc = reader.GetString("Description");
                                string shortDesc = fullDesc.Length > 50 ? fullDesc.Substring(0, 47) + "..." : fullDesc;

                                searchResults.Add(new ProductSearchViewModel
                                {
                                    Id = reader.GetInt32("Id"),
                                    Name = reader.GetString("Name"),

                                    // ⭐ FIX 1: Read BasePrice column, assign to BasePrice property
                                    BasePrice = reader.GetDecimal("BasePrice"),

                                    // ⭐ FIX 2: Read ImageUrl column, assign to ImageUrl property
                                    ImageUrl = reader.GetString("ImageUrl"),

                                    Description = shortDesc
                                });
                            }
                        }
                    }
                }

                return Json(searchResults, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // Use this to debug the error in your browser's Network tab
                return Json(new { error = ex.Message, message = "C# Controller Error" }, JsonRequestBehavior.AllowGet);
            }
        }

        // Inside HomeController.cs

        // This action returns a small HTML snippet (PartialView) for the modal body
        public ActionResult ProductDetails(int id)
        {
            // 1. Fetch the full Product model based on the ID
            var product = GetProductById(id); // (Assuming you have a method to fetch product details)

            if (product == null)
            {
                return HttpNotFound();
            }

            // 2. Return the partial view. 
            // You need to create this view: Views/Home/ProductDetails.cshtml
            return PartialView("_ProductDetailsPartial", product);
        }

        private List<Product> GetMenuProducts()
        {
            var products = new List<Product>();

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();

                // 1. Select ALL active products (IsActive = 1)
                string query =
                    "SELECT * FROM products WHERE IsActive = 1 ORDER BY Id;";

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

                            // NOTE: You must ensure these columns are NOT NULL in your DB, or handle DBNull properly
                            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? "" : reader.GetString("Description"),
                            ImageUrl = reader.IsDBNull(reader.GetOrdinal("ImageUrl")) ? "" : reader.GetString("ImageUrl"),
                            IsActive = reader.GetBoolean("IsActive"),
                            IsFeatured = reader.GetBoolean("IsFeatured"),
                            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? "Other" : reader.GetString("Category"), // Use 'Other' as default
                            ProductSizes = new List<ProductSize>()
                        });
                    }
                }
            }

            // 2. Load sizes for ALL fetched products
            // We'll run one query for all sizes and map them using LINQ (more efficient than querying sizes per product).
            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                // Only select sizes for products that were actually fetched (active ones)
                string sizeQuery = "SELECT Id, Product_Id, SizeName, Price FROM product_sizes;";

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

                        // Find the parent product in the list and attach the size
                        var parent = products.FirstOrDefault(p => p.Id == size.Product_Id);
                        parent?.ProductSizes.Add(size); // The '?' prevents error if product ID is not found (shouldn't happen)
                    }
                }
            }

            return products;
        }





        // ----------------------------------------------------
        // PUBLIC PAGES (Index, Menu)
        // ----------------------------------------------------
        [AllowAnonymous] // Allows unauthenticated access
        public ActionResult Index()
        {
            var products = GetProductsWithSizes();
            return View(products);
        }

        [AllowAnonymous] // Allows unauthenticated access
        public ActionResult Menu(string filter = "All Drinks")
        {
            // ... (Your GetMenuProducts setup) ...
            List<Product> allProducts = GetMenuProducts();
            List<Product> filteredProducts;

            // 1. Normalize the filter string once
            string normalizedFilter = filter.Trim();

            // Apply filtering logic
            switch (normalizedFilter)
            {
                case "Featured":
                    filteredProducts = allProducts.Where(p => p.IsFeatured).ToList();
                    break;
                case "All Drinks":
                    filteredProducts = allProducts.ToList();
                    break;
                default:
                    // 2. CRITICAL FIX: Use String.Equals with StringComparison.OrdinalIgnoreCase.
                    // This ignores case differences, which is crucial if the user selects 
                    // "Coffee" but the database has "Coffee", or vice versa, based on
                    // how your ViewBag.FilterOptions are defined.
                    filteredProducts = allProducts
                        .Where(p =>
                            !string.IsNullOrWhiteSpace(p.Category) &&
                            p.Category.Trim().Equals(normalizedFilter, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    break;
            }

            // Ensure ViewBag.FilterOptions matches the database categories
            ViewBag.FilterOptions = new List<string> { "All Drinks", "Featured", "Coffees", "Non-Coffees", "Strawberry Series" };
            ViewBag.CurrentFilter = normalizedFilter;

            return View(filteredProducts);
        }


        // ----------------------------------------------------
        // LOGIN ACTIONS
        // ----------------------------------------------------

        [AllowAnonymous]
        public ActionResult Login()
        {
            if (User.Identity.IsAuthenticated)
            {
                // FIX: Check Session["IsAdmin"] because User.IsInRole checks are failing
                if (Session["IsAdmin"] != null && (bool)Session["IsAdmin"])
                {
                    return RedirectToAction("Admin_Dashboard", "Dashboard");
                }

                return RedirectToAction("UserProfile", "Home");
            }
            return View();
        }

        // ----------------------------------------------------
        // SIGN UP ACTIONS
        // ----------------------------------------------------

        [AllowAnonymous]
        public ActionResult SignUp()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("UserProfile", "Home");
            }
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult SignUp(User model)
        {
            if (ModelState.IsValid)
            {
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Check if email already exists
                    string checkEmailQuery = "SELECT COUNT(*) FROM users WHERE Email = @Email";
                    using (var checkCmd = new MySqlCommand(checkEmailQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@Email", model.Email);
                        if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
                        {
                            ModelState.AddModelError("Email", "Email already registered.");
                            return View(model);
                        }
                    }

                    // Password Hashing: Hash the password before saving
                    string hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.Password, BCrypt.Net.BCrypt.GenerateSalt(12));

                    // NOTE: New signups will have IsAdmin = 0 (the default value)
                    string insertQuery = @"
                        INSERT INTO users (FirstName, LastName, Email, Address, PhoneNumber, PasswordHash)
                        VALUES (@FirstName, @LastName, @Email, @Address, @PhoneNumber, @PasswordHash)";

                    using (var cmd = new MySqlCommand(insertQuery, conn))
                    {
                        // SQL Injection Prevention: Parameterized queries for all inputs
                        cmd.Parameters.AddWithValue("@FirstName", model.FirstName);
                        cmd.Parameters.AddWithValue("@LastName", model.LastName);
                        cmd.Parameters.AddWithValue("@Email", model.Email);
                        cmd.Parameters.AddWithValue("@Address", model.Address);
                        cmd.Parameters.AddWithValue("@PhoneNumber", model.PhoneNumber);
                        cmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);

                        cmd.ExecuteNonQuery();
                    }
                }

                TempData["Message"] = "Registration successful! Please log in.";
                return RedirectToAction("Login", "Home");
            }
            return View(model);
        }

        // ----------------------------------------------------
        // PROTECTED ACTIONS (Profile, Logout)
        // ----------------------------------------------------

        [Authorize] // Requires login
        public ActionResult UserProfile()
        {
            string userEmail = User.Identity.Name;

            User user = null;

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                string query = @"
                    SELECT Id, FirstName, LastName, Email, Address, PhoneNumber 
                    FROM users 
                    WHERE Email = @Email LIMIT 1";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Email", userEmail);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            user = new User
                            {
                                Id = reader.GetInt32("Id"),
                                FirstName = reader.GetString("FirstName"),
                                LastName = reader.GetString("LastName"),
                                Email = reader.GetString("Email"),
                                // Ensure you handle potential DBNull for nullable columns in UserProfile as well
                                Address = reader.IsDBNull(reader.GetOrdinal("Address")) ? string.Empty : reader.GetString("Address"),
                                PhoneNumber = reader.IsDBNull(reader.GetOrdinal("PhoneNumber")) ? string.Empty : reader.GetString("PhoneNumber")
                            };
                        }
                    }
                }
            }

            if (user != null)
            {
                return View(user);
            }

            // Should not happen, but log out if user data is missing
            FormsAuthentication.SignOut();
            return RedirectToAction("Login");
        }



        [Authorize]
        public ActionResult Checkout()
        {
            // 1. Load the cart data
            var cart = GetCurrentCart();

            // 2. Prevent checkout if the cart is empty
            if (cart == null || !cart.Items.Any())
            {
                TempData["Message"] = "Your cart is empty. Please add items before checking out.";
                return RedirectToAction("Index");
            }

            // ⭐ NEW STEP: Load the Stock for each Cart Item from the database
            LoadCartItemsStock(cart);

            // 3. Load the logged-in user and pass it via ViewBag
            var user = GetLoggedInUser();
            ViewBag.LoggedInUser = user;

            // 4. Pass the full cart model to the view
            return View(cart);
        }


        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Login", "Home");
        }
        // ================================================
        // ORDER HISTORY PAGE
        // ================================================
        public ActionResult OrderHistory()
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Login");

            int userId = Convert.ToInt32(Session["UserId"]);
            List<UserOrder> orders = new List<UserOrder>();

            using (MySqlConnection con = new MySqlConnection(connectionString))
            {
                con.Open();

                string query = @"SELECT OrderId, OrderDate, TotalAmount, Status, PaymentMethod
                                 FROM orders
                                 WHERE UserId = @uid
                                 ORDER BY OrderDate DESC";

                using (MySqlCommand cmd = new MySqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@uid", userId);

                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            orders.Add(new UserOrder
                            {
                                OrderId = Convert.ToInt32(dr["OrderId"]),
                                OrderDate = Convert.ToDateTime(dr["OrderDate"]).ToString("yyyy-MM-dd"),
                                TotalAmount = Convert.ToDecimal(dr["TotalAmount"]),
                                Status = dr["Status"].ToString(),
                                PaymentMethod = dr["PaymentMethod"].ToString()
                            });
                        }
                    }
                }
            }

            return View(orders);
        }


        // ================================================
        // AJAX: GET ORDER ITEMS
        // ================================================
        public JsonResult GetOrderItems(int orderId)
        {
            try
            {
                List<UserOrderItem> items = new List<UserOrderItem>();

                using (MySqlConnection con = new MySqlConnection(connectionString))
                {
                    con.Open();

                    string query = @"
                SELECT 
                    oi.Name,
                    oi.Quantity,
                    (oi.Quantity * oi.Price) AS Subtotal,
                    p.ImageUrl
                FROM orderitems oi
                LEFT JOIN products p ON oi.ProductId = p.Id
                WHERE oi.OrderId = @oid
            ";

                    using (MySqlCommand cmd = new MySqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@oid", orderId);

                        using (MySqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                items.Add(new UserOrderItem
                                {
                                    Name = dr["Name"].ToString(),
                                    Quantity = Convert.ToInt32(dr["Quantity"]),
                                    Subtotal = Convert.ToDecimal(dr["Subtotal"]),
                                    ImageUrl = dr["ImageUrl"] != DBNull.Value ? dr["ImageUrl"].ToString() : ""
                                });
                            }
                        }
                    }
                }

                return Json(items, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // Inside HomeController.cs

        private int? ValidatePasswordToken(string token)
        {
            int? userId = null;

            // Check for existence, non-usage, and non-expiration
            string query = @"
        SELECT UserId 
        FROM PasswordResetTokens 
        WHERE Token = @Token 
          AND Used = FALSE 
          AND ExpiryDate > NOW();"; // NOW() is the MySQL function for current time

            try
            {
                using (MySqlConnection con = new MySqlConnection(connectionString))
                using (MySqlCommand cmd = new MySqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Token", token);
                    con.Open();

                    // ExecuteScalar retrieves the first column of the first row (the UserId)
                    object result = cmd.ExecuteScalar();

                    if (result != null)
                    {
                        userId = Convert.ToInt32(result);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error
                System.Diagnostics.Debug.WriteLine($"DB Error in ValidatePasswordToken: {ex.Message}");
            }

            return userId;
        }

        // Inside HomeController.cs

        private bool UpdateUserPasswordAndMarkTokenUsed(int userId, string token, string newPassword)
        {
            // 1. Hash the new password using BCrypt
            // Ensure you have: using BCrypt.Net; at the top of HomeController.cs
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
            bool success = false;

            // Use a transaction to ensure both updates happen or neither does
            using (MySqlConnection con = new MySqlConnection(connectionString))
            {
                con.Open();
                MySqlTransaction tran = con.BeginTransaction();

                try
                {
                    // 2. Update the User's Password
                    // ⭐️ FIX: Changed 'Password' to 'PasswordHash' ⭐️
                    string updatePasswordQuery = "UPDATE users SET PasswordHash = @PasswordHash WHERE Id = @UserId;";

                    using (MySqlCommand cmd = new MySqlCommand(updatePasswordQuery, con, tran))
                    {
                        cmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.ExecuteNonQuery();
                    }

                    // 3. Mark the Token as Used
                    string markTokenUsedQuery = "UPDATE PasswordResetTokens SET Used = TRUE WHERE Token = @Token;";
                    using (MySqlCommand cmd = new MySqlCommand(markTokenUsedQuery, con, tran))
                    {
                        cmd.Parameters.AddWithValue("@Token", token);
                        cmd.ExecuteNonQuery();
                    }

                    // Commit the transaction only if both commands succeeded
                    tran.Commit();
                    success = true;
                }
                catch (Exception ex)
                {
                    // If any error occurred, roll back the transaction
                    tran.Rollback();
                    System.Diagnostics.Debug.WriteLine($"--- FATAL PASSWORD RESET ERROR ---");
                    System.Diagnostics.Debug.WriteLine($"Message: {ex.Message}");
                    // Re-throw the error for debugging if needed, but logging is essential
                    success = false;
                }
            }

            return success;
        }

        // Inside HomeController.cs

        private User FindUserByEmail(string email)
        {
            User user = null;
            string query = "SELECT Id, FirstName, LastName, Email FROM users WHERE Email = @Email LIMIT 1";

            try
            {
                using (MySqlConnection con = new MySqlConnection(connectionString))
                using (MySqlCommand cmd = new MySqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    con.Open();
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            user = new User
                            {
                                Id = dr.GetInt32("Id"),
                                FirstName = dr.GetString("FirstName"),
                                LastName = dr.GetString("LastName"),
                                Email = dr.GetString("Email")
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB Error in FindUserByEmail: {ex.Message}");
            }
            return user;
        }

        // Inside HomeController.cs

        private string GenerateAndSaveToken(int userId)
        {
            string token = Guid.NewGuid().ToString("N");
            DateTime expiryDate = DateTime.Now.AddHours(2); // Token expires in 2 hours

            string query = @"
        INSERT INTO PasswordResetTokens (UserId, Token, ExpiryDate, Used) 
        VALUES (@UserId, @Token, @ExpiryDate, FALSE);";

            try
            {
                using (MySqlConnection con = new MySqlConnection(connectionString))
                using (MySqlCommand cmd = new MySqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@Token", token);
                    cmd.Parameters.AddWithValue("@ExpiryDate", expiryDate);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB Error in GenerateAndSaveToken: {ex.Message}");
                throw;
            }

            return token;
        }

        // Inside HomeController.cs

        private void SendPasswordResetEmail(string toEmail, string resetLink)
        {
            try
            {
                var mailMessage = new MailMessage
                {
                    From = new MailAddress("no-reply@cupofjoel.com", "Cup of Joel Support"),
                    Subject = "Password Reset Request",
                    Body = $"Dear Customer,\n\nYou requested a password reset. Please click the following link to choose a new password:\n\n{resetLink}\n\nThis link will expire in 2 hours.",
                    IsBodyHtml = false,
                };
                mailMessage.To.Add(toEmail);

                // ⭐️ Use your actual Gmail address for the username ⭐️
                string smtpUsername = "cupofjoel123@gmail.com";

                // ⭐️ Use the 16-character App Password generated by Google ⭐️
                string smtpAppPassword = "vrtr xksq zyfz alig"; // Replace with your generated App Password

                using (var client = new SmtpClient("smtp.gmail.com", 587)) // The SMTP host is smtp.gmail.com
                {
                    client.Credentials = new NetworkCredential(smtpUsername, smtpAppPassword);
                    client.EnableSsl = true;
                    client.Send(mailMessage);
                }
            }
            catch (Exception ex)
            {
                // Log the email failure, but do not interrupt the main process
                System.Diagnostics.Debug.WriteLine($"Email Sending Error to {toEmail}: {ex.Message}");
            }
        }

        // GET: /Home/ForgotPassword
        public ActionResult ForgotPassword()
        {
            return View();
        }

        // POST: /Home/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var user = FindUserByEmail(model.Email);

                    if (user != null)
                    {
                        string token = GenerateAndSaveToken(user.Id);

                        // Build the full reset URL, including the scheme (http or https)
                        string resetUrl = Url.Action("ResetPassword", "Home", new { token = token }, Request.Url.Scheme);

                        SendPasswordResetEmail(user.Email, resetUrl);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Critical Error: {ex.Message}");
                    // The process will still continue to the success message below.
                }

                // Security Best Practice: Always send a vague success message to prevent email enumeration.
                TempData["Message"] = "If an account exists for the email provided, a password reset link has been sent.";

                return RedirectToAction("Login"); // Send user back to the login page
            }

            return View(model);
        }

        // GET: /Home/ResetPassword?token=xxx
        public ActionResult ResetPassword(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                TempData["Message"] = "Invalid password reset link.";
                return RedirectToAction("Login");
            }

            // 1. Check if the token is valid, not expired, and not used
            // (This requires a helper method that you must implement based on your DB schema)
            int? userId = ValidatePasswordToken(token);

            if (userId == null)
            {
                TempData["Message"] = "The password reset link is invalid or has expired.";
                return RedirectToAction("Login");
            }

            // Pass the token to the view (hidden field)
            var model = new ResetPasswordViewModel { Token = token };
            return View(model);
        }

        // POST: /Home/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(ResetPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                // 1. Re-validate the token before proceeding
                int? userId = ValidatePasswordToken(model.Token);

                if (userId == null)
                {
                    TempData["Message"] = "The password reset link is invalid or has expired.";
                    return RedirectToAction("Login");
                }

                // 2. Update the user's password and mark the token as used (This requires a helper method)
                bool success = UpdateUserPasswordAndMarkTokenUsed(userId.Value, model.Token, model.NewPassword);

                if (success)
                {
                    TempData["Message"] = "Your password has been successfully reset. Please log in.";
                    return RedirectToAction("Login");
                }
                else
                {
                    ModelState.AddModelError("", "An error occurred while resetting the password. Please try again.");
                }
            }

            // If validation fails or update failed
            return View(model);
        }

        // Inside HomeController.cs

        // 1. Helper to retrieve the current user's password hash and ID
        private User GetCurrentUserCredentials(string email)
        {
            User user = null;
            // Assuming the database column name for the hash is PasswordHash
            string query = "SELECT Id, PasswordHash FROM users WHERE Email = @Email LIMIT 1";

            try
            {
                using (MySqlConnection con = new MySqlConnection(connectionString))
                using (MySqlCommand cmd = new MySqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    con.Open();
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            user = new User
                            {
                                Id = dr.GetInt32("Id"),
                                // Map the hash from the DB column to the model property
                                PasswordHash = dr.GetString("PasswordHash")
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB Error in GetCurrentUserCredentials: {ex.Message}");
            }
            return user;
        }

        // Updates the user's PasswordHash column with a new hashed password
        private bool UpdateUserPassword(int userId, string newHashedPassword)
        {
            // Uses the PasswordHash column name
            string query = "UPDATE users SET PasswordHash = @PasswordHash WHERE Id = @UserId";
            try
            {
                using (MySqlConnection con = new MySqlConnection(connectionString))
                using (MySqlCommand cmd = new MySqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@PasswordHash", newHashedPassword);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    con.Open();
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB Error in UpdateUserPassword: {ex.Message}");
                return false;
            }
        
        }

        // GET: /Home/ChangePassword
        [Authorize]
        public ActionResult ChangePassword()
        {
            // This action ensures the user is logged in before showing the form
            return View(new ChangePasswordViewModel());
        }

        // POST: /Home/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public ActionResult ChangePassword(ChangePasswordViewModel model)
        {
            // The user's email is stored in the FormsAuthentication ticket
            string userEmail = User.Identity.Name;

            if (ModelState.IsValid)
            {
                // 1. Retrieve user data (ID and stored hash)
                User currentUser = GetCurrentUserCredentials(userEmail);

                if (currentUser == null)
                {
                    ModelState.AddModelError("", "Session error. Please log in again.");
                    return View(model);
                }

                // 2. VERIFY the Current Password against the stored hash
                bool isCurrentPasswordValid = BCrypt.Net.BCrypt.Verify(model.CurrentPassword, currentUser.PasswordHash);

                if (!isCurrentPasswordValid)
                {
                    // Add error specific to the field
                    ModelState.AddModelError("CurrentPassword", "The current password you entered is incorrect.");
                    return View(model);
                }

                // 3. Hash the new password and update the database
                string newHashedPassword = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
                bool updateSuccess = UpdateUserPassword(currentUser.Id, newHashedPassword);

                if (updateSuccess)
                {
                    // Success: Display success message and redirect
                    TempData["Message"] = "Your password has been changed successfully. Please note that you may need to log in again.";
                    // You might want to sign the user out here if you want to force re-login
                    // FormsAuthentication.SignOut(); 
                    return RedirectToAction("UserProfile", "Home");
                }
                else
                {
                    ModelState.AddModelError("", "A database error occurred while updating your password. Please try again.");
                }
            }

            // If validation fails or update failed, return to view
            return View(model);
        }



    }
}