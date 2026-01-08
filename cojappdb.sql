-- MySQL dump 10.13  Distrib 8.0.44, for Win64 (x86_64)
--
-- Host: 127.0.0.1    Database: cojappdb
-- ------------------------------------------------------
-- Server version	5.5.5-10.4.32-MariaDB

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!50503 SET NAMES utf8 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Table structure for table `orderitems`
--

DROP TABLE IF EXISTS `orderitems`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `orderitems` (
  `OrderItemId` int(11) NOT NULL AUTO_INCREMENT,
  `OrderId` int(11) NOT NULL,
  `ProductId` int(11) NOT NULL,
  `SizeId` int(11) NOT NULL,
  `Name` varchar(255) NOT NULL,
  `SizeName` varchar(50) NOT NULL,
  `Price` decimal(10,2) NOT NULL,
  `Quantity` int(11) NOT NULL,
  PRIMARY KEY (`OrderItemId`),
  KEY `OrderId` (`OrderId`),
  KEY `ProductId` (`ProductId`),
  KEY `orderitems_ibfk_3` (`SizeId`),
  CONSTRAINT `orderitems_ibfk_1` FOREIGN KEY (`OrderId`) REFERENCES `orders` (`OrderId`),
  CONSTRAINT `orderitems_ibfk_2` FOREIGN KEY (`ProductId`) REFERENCES `products` (`Id`),
  CONSTRAINT `orderitems_ibfk_3` FOREIGN KEY (`SizeId`) REFERENCES `product_sizes` (`Id`) ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=19 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `orderitems`
--

LOCK TABLES `orderitems` WRITE;
/*!40000 ALTER TABLE `orderitems` DISABLE KEYS */;
INSERT INTO `orderitems` VALUES (1,1,1,57,'Spanish Latte','16oz',89.00,3),(2,1,3,61,'C&C Spanish Latte','16oz',79.00,2),(3,1,4,63,'Americano','16oz',59.00,1),(4,1,4,64,'Americano','20oz',69.00,1),(5,1,15,93,'Cookies & Cream Milk','16oz',69.00,2),(6,1,17,97,'Ube Milkimon','16oz',69.00,1),(7,2,2,113,'Caramel Macchiato','16oz',69.00,1),(8,3,4,63,'Americano','16oz',59.00,1),(9,4,4,63,'Americano','16oz',59.00,1),(10,5,2,113,'Caramel Macchiato','16oz',69.00,2),(11,5,4,63,'Americano','16oz',59.00,1),(12,6,1,57,'Spanish Latte','16oz',89.00,1),(13,6,3,61,'C&C Spanish Latte','16oz',79.00,1),(14,7,22,107,'Strawberry Matcha Latte','16oz',69.00,1),(15,8,1,57,'Spanish Latte','16oz',69.00,1),(16,9,1,57,'Spanish Latte','16oz',69.00,1),(17,10,2,114,'Caramel Macchiato','20oz',79.00,1),(18,11,1,57,'Spanish Latte','16oz',69.00,10);
/*!40000 ALTER TABLE `orderitems` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `orders`
--

DROP TABLE IF EXISTS `orders`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `orders` (
  `OrderId` int(11) NOT NULL AUTO_INCREMENT,
  `UserId` int(11) NOT NULL,
  `OrderDate` datetime NOT NULL,
  `RecipientName` varchar(255) NOT NULL,
  `DeliveryAddress` varchar(500) NOT NULL,
  `PhoneNumber` varchar(20) NOT NULL,
  `PaymentMethod` varchar(50) NOT NULL,
  `ShippingFee` decimal(10,2) NOT NULL,
  `SubTotal` decimal(10,2) NOT NULL,
  `TotalAmount` decimal(10,2) NOT NULL,
  `Status` varchar(50) NOT NULL,
  PRIMARY KEY (`OrderId`),
  KEY `UserId` (`UserId`),
  CONSTRAINT `orders_ibfk_1` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=12 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `orders`
--

LOCK TABLES `orders` WRITE;
/*!40000 ALTER TABLE `orders` DISABLE KEYS */;
INSERT INTO `orders` VALUES (1,3,'2025-11-23 16:05:55','Boss  Oleg','jahan jahan, malapit na cho. ang talap, ang talap','09290653479','Credit Card',50.00,760.00,810.00,'Delivered'),(2,3,'2025-11-23 16:15:59','Boss  Oleg','jahan jahan, malapit na cho. ang talap, ang talap','09290653479','Maya',50.00,69.00,119.00,'Delivered'),(3,3,'2025-11-23 16:28:26','Boss  Oleg','jahan jahan, malapit na cho. ang talap, ang talap','09290653479','Credit Card',50.00,59.00,109.00,'Canceled'),(4,3,'2025-11-23 16:33:27','Boss  Oleg','jahan jahan, malapit na cho. ang talap, ang talap','09290653479','Credit Card',50.00,59.00,109.00,'Canceled'),(5,2,'2025-11-23 16:38:09','Bisha Sama','sa kanto lang','12345678910','GCash',50.00,197.00,247.00,'Delivered'),(6,3,'2025-11-23 17:11:57','Boss  Oleg','jahan jahan, malapit na cho. ang talap, ang talap','09290653479','GCash',50.00,168.00,218.00,'Delivered'),(7,4,'2025-11-24 17:28:49','Junie Ricky','basta','09818218218','GCash',50.00,69.00,119.00,'Delivered'),(8,3,'2025-11-25 10:13:28','Boss  Oleg','jahan jahan, malapit na cho. ang talap, ang talap','09290653479','GCash',50.00,69.00,119.00,'Delivered'),(9,3,'2025-11-25 10:30:58','Boss  Oleg','jahan jahan, malapit na cho. ang talap, ang talap','09290653479','GCash',50.00,69.00,119.00,'Delivered'),(10,4,'2025-11-25 10:32:25','Junie Ricky','basta','09818218218','GCash',50.00,79.00,129.00,'Delivered'),(11,3,'2025-11-25 10:33:30','Boss  Oleg','jahan jahan, malapit na cho. ang talap, ang talap','09290653479','GCash',50.00,690.00,740.00,'Delivered');
/*!40000 ALTER TABLE `orders` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `passwordresettokens`
--

DROP TABLE IF EXISTS `passwordresettokens`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `passwordresettokens` (
  `TokenId` int(11) NOT NULL AUTO_INCREMENT,
  `UserId` int(11) NOT NULL,
  `Token` varchar(100) NOT NULL,
  `ExpiryDate` datetime NOT NULL,
  `Used` tinyint(1) DEFAULT 0,
  `CreatedAt` timestamp NOT NULL DEFAULT current_timestamp(),
  PRIMARY KEY (`TokenId`),
  UNIQUE KEY `Token` (`Token`),
  KEY `UserId` (`UserId`),
  CONSTRAINT `passwordresettokens_ibfk_1` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=5 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `passwordresettokens`
--

LOCK TABLES `passwordresettokens` WRITE;
/*!40000 ALTER TABLE `passwordresettokens` DISABLE KEYS */;
INSERT INTO `passwordresettokens` VALUES (1,2,'bed494ae0f4b4242a6898614a8a82aac','2025-11-30 16:42:21',0,'2025-11-30 06:42:21'),(2,2,'335bdf6f97a6451fb45802af188ef103','2025-11-30 16:51:55',0,'2025-11-30 06:51:55'),(3,2,'de2beed1e0cd416f8769ac6027224c5a','2025-11-30 16:55:51',1,'2025-11-30 06:55:51'),(4,2,'da9fe7a48626495082890717efd1f021','2025-11-30 21:40:02',0,'2025-11-30 11:40:02');
/*!40000 ALTER TABLE `passwordresettokens` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `product_sizes`
--

DROP TABLE IF EXISTS `product_sizes`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `product_sizes` (
  `Id` int(11) NOT NULL AUTO_INCREMENT,
  `Product_Id` int(11) NOT NULL,
  `SizeName` varchar(50) NOT NULL,
  `Price` decimal(10,2) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `fk_product_sizes_product_id` (`Product_Id`),
  CONSTRAINT `fk_product_sizes_product_id` FOREIGN KEY (`Product_Id`) REFERENCES `products` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=147 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `product_sizes`
--

LOCK TABLES `product_sizes` WRITE;
/*!40000 ALTER TABLE `product_sizes` DISABLE KEYS */;
INSERT INTO `product_sizes` VALUES (57,1,'16oz',69.00),(58,1,'20oz',79.00),(61,3,'16oz',79.00),(62,3,'20oz',89.00),(63,4,'16oz',59.00),(64,4,'20oz',69.00),(93,15,'16oz',69.00),(94,15,'20oz',79.00),(97,17,'16oz',69.00),(98,17,'20oz',79.00),(107,22,'16oz',79.00),(108,22,'20oz',89.00),(113,2,'16oz',69.00),(114,2,'20oz',79.00),(117,5,'16oz',69.00),(118,5,'20oz',79.00),(119,6,'16oz',69.00),(120,6,'20oz',79.00),(121,7,'16oz',69.00),(122,7,'20oz',79.00),(123,8,'16oz',69.00),(124,8,'20oz',79.00),(125,9,'16oz',69.00),(126,9,'20oz',79.00),(127,10,'16oz',69.00),(128,10,'20oz',79.00),(129,11,'16oz',69.00),(130,11,'20oz',79.00),(131,12,'16oz',69.00),(132,12,'20oz',79.00),(133,13,'16oz',69.00),(134,13,'20oz',79.00),(135,14,'16oz',69.00),(136,14,'20oz',79.00),(137,16,'16oz',69.00),(138,16,'20oz',79.00),(139,18,'16oz',69.00),(140,18,'20oz',79.00),(141,19,'16oz',69.00),(142,19,'20oz',79.00),(143,20,'16oz',69.00),(144,20,'20oz',79.00),(145,21,'16oz',69.00),(146,21,'20oz',79.00);
/*!40000 ALTER TABLE `product_sizes` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `products`
--

DROP TABLE IF EXISTS `products`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `products` (
  `Id` int(11) NOT NULL AUTO_INCREMENT,
  `Name` varchar(255) NOT NULL,
  `Description` text DEFAULT NULL,
  `ImageUrl` varchar(255) DEFAULT NULL,
  `BasePrice` decimal(10,2) NOT NULL,
  `Stock` int(11) NOT NULL DEFAULT 0,
  `IsActive` tinyint(4) NOT NULL DEFAULT 1,
  `CreatedAt` datetime DEFAULT current_timestamp(),
  `UpdatedAt` datetime DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  `IsFeatured` tinyint(1) NOT NULL DEFAULT 0,
  `Category` varchar(100) NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=24 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `products`
--

LOCK TABLES `products` WRITE;
/*!40000 ALTER TABLE `products` DISABLE KEYS */;
INSERT INTO `products` VALUES (1,'Spanish Latte','A sweet and creamy fusion of espresso and milk, elevated with a rich caramelized flavor. Perfect for those who love a smooth, dessert-like coffee.','/Uploads/Images/spanish_latte_253321183353643.png',69.00,10,1,'2025-11-20 07:49:10','2025-11-29 18:40:21',1,'Coffees'),(2,'Caramel Macchiato','Layered espresso, milk, and caramel come together for a rich, buttery treat with a perfect sweet kick.','/Uploads/Images/caramel_macchiato_254921184952148.jpg',69.00,19,1,'2025-11-20 07:49:46','2025-11-29 18:40:40',1,'Coffees'),(3,'C&C Spanish Latte','A sweet and creamy fusion of espresso and milk, elevated with a rich caramelized flavor. Perfect for those who love a smooth, dessert-like coffee.','/Uploads/Images/cc_spanish_latte_253421183409668.png',79.00,20,1,'2025-11-20 07:50:14','2025-11-29 18:40:51',1,'Coffees'),(4,'Americano','A bold and refreshing blend of espresso and cold water. Smooth, crisp, and perfect for coffee lovers who enjoy a pure, clean taste','/Uploads/Images/americano_253421183415775.png',59.00,20,1,'2025-11-20 07:50:36','2025-11-29 18:41:03',1,'Coffees'),(5,'Latte','A classic balance of espresso and chilled milk. Light, silky, and perfect for an everyday coffee fix.','/Uploads/Images/latte_254021184006408.png',69.00,20,1,'2025-11-20 07:51:17','2025-11-29 18:41:17',0,'Coffees'),(6,'Hazelnut Latte','A creamy espresso drink infused with warm hazelnut notes. Nutty, smooth, and irresistibly comforting.','/Uploads/Images/hazelnut_latte_254021184014093.png',69.00,20,1,'2025-11-20 07:51:44','2025-11-29 18:41:28',0,'Coffees'),(7,'Fluffy Latte','A velvety latte topped with light, airy cream. Soft, smooth, and delightfully refreshing.','/Uploads/Images/fluffy_latte_254021184026999.png',69.00,20,1,'2025-11-20 08:09:49','2025-11-29 18:42:18',0,'Coffees'),(8,'Vanilla Latte','A sweet, aromatic latte with a touch of classic vanilla. Smooth, fragrant, and perfectly balanced.','/Uploads/Images/vanilla_latte_254021184037220.png',69.00,20,1,'2025-11-20 08:10:13','2025-11-29 18:42:32',0,'Coffees'),(9,'Salted Caramel','A blend of espresso, milk, and salted caramel that creates the perfect sweet-and-salty harmony.','/Uploads/Images/salted_caramel_254021184043561.png',69.00,20,1,'2025-11-20 08:11:59','2025-11-29 18:42:47',0,'Coffees'),(10,'Mocha Latte','A rich mix of espresso and chocolate, topped with creamy milk. Bold, smooth, and perfect for chocolate lovers.','/Uploads/Images/mocha_latte_254121184105858.png',69.00,20,1,'2025-11-20 08:12:23','2025-11-29 18:42:57',0,'Coffees'),(11,'C&C Mocha Latte','A chocolate-and-creamer twist on your favorite mocha. Extra creamy, extra chocolatey, extra satisfying.','/Uploads/Images/cc_mocha_latte_254121184111294.png',69.00,20,1,'2025-11-20 08:12:47','2025-11-29 18:43:11',0,'Coffees'),(12,'Ube Spanish Latte','A sweeter, creamier take on the classic Spanish Latte with the signature C&C flavor blend.','/Uploads/Images/ube_spanish_latte_254121184122391.png',69.00,20,1,'2025-11-20 08:13:16','2025-11-29 18:43:37',0,'Coffees'),(13,'Milkimon Coffee','A rich coffee blend with a creamy milk base for a satisfying, full-bodied drink.','/Uploads/Images/milkimon_coffee_254121184135183.png',69.00,20,1,'2025-11-20 08:13:43','2025-11-29 18:43:50',0,'Coffees'),(14,'Chocolate','A smooth and creamy chocolate drink served ice-cold. Rich, sweet, and perfectly comforting.','/Uploads/Images/chocolate_254121184148015.jpg',69.00,20,1,'2025-11-20 08:16:41','2025-11-29 18:44:02',1,'Non-Coffees'),(15,'Cookies & Cream Milk','A rich and creamy milk drink with the signature C&C flavor—sweet, smooth, and satisfying.','/Uploads/Images/ccmilk_254221184208895.png',69.00,20,1,'2025-11-20 08:22:38','2025-11-29 18:44:18',1,'Non-Coffees'),(16,'Milkimon','Creamy, silky milk served chilled for a simple and refreshing treat.','/Uploads/Images/milkimon_254221184215961.png',69.00,20,1,'2025-11-20 08:22:56','2025-11-29 18:44:27',0,'Non-Coffees'),(17,'Ube Milkimon','A sweet, velvety ube milk blend with a creamy finish. A Filipino classic turned into a refreshing iced drink.','/Uploads/Images/ube_milkimon_254221184221836.png',69.00,20,1,'2025-11-20 08:23:26','2025-11-29 18:44:52',0,'Non-Coffees'),(18,'Strawberry C&C','A creamy and sweet strawberry drink blended with C&C for a refreshing fruity twist.','/Uploads/Images/strawberry_cc_254221184230788.png',69.00,20,1,'2025-11-20 08:24:55','2025-11-29 18:45:02',1,'Strawberry Series'),(19,'Strawberry Con Leche','A sweet, milk-based strawberry drink inspired by classic “leche” blends. Smooth, fruity, and comforting.','/Uploads/Images/strawberry_con_leche_254221184236926.png',69.00,20,1,'2025-11-20 08:25:48','2025-11-29 18:45:13',1,'Strawberry Series'),(20,'Strawberry Matcha','Earthy matcha paired with sweet strawberry for a perfect balance of bold and fruity.','/Uploads/Images/strawberry_matcha_254221184245277.png',69.00,20,1,'2025-11-20 08:26:28','2025-11-29 18:45:22',1,'Strawberry Series'),(21,'Strawberry Latte','Strawberries and milk meet espresso for a unique, fruity-creamy latte experience.','/Uploads/Images/strawberry_latte_254221184252247.png',69.00,20,1,'2025-11-20 08:26:47','2025-11-30 17:08:45',0,'Strawberry Series'),(22,'Strawberry Matcha Latte','A silky matcha latte uplifted with a sweet strawberry swirl. Layered, creamy, and perfectly refreshing.','/Uploads/Images/strawberry_matcha_latte_254221184257731.png',79.00,20,0,'2025-11-20 08:27:10','2025-11-30 17:32:30',0,'Strawberry Series');
/*!40000 ALTER TABLE `products` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `user_carts`
--

DROP TABLE IF EXISTS `user_carts`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `user_carts` (
  `UserId` int(11) NOT NULL,
  `ProductId` int(11) NOT NULL,
  `ProductSizeId` int(11) NOT NULL,
  `Quantity` int(11) NOT NULL DEFAULT 1,
  `Created_at` timestamp NOT NULL DEFAULT current_timestamp(),
  `Updated_at` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`UserId`,`ProductId`,`ProductSizeId`),
  KEY `ProductId` (`ProductId`),
  KEY `ProductSizeId` (`ProductSizeId`),
  CONSTRAINT `user_carts_ibfk_1` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `user_carts_ibfk_2` FOREIGN KEY (`ProductId`) REFERENCES `products` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `user_carts_ibfk_3` FOREIGN KEY (`ProductSizeId`) REFERENCES `product_sizes` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `user_carts`
--

LOCK TABLES `user_carts` WRITE;
/*!40000 ALTER TABLE `user_carts` DISABLE KEYS */;
INSERT INTO `user_carts` VALUES (2,1,57,10,'2025-11-30 08:13:20','2025-11-30 08:13:20'),(2,10,128,1,'2025-11-30 08:13:11','2025-11-30 08:13:11'),(2,11,129,1,'2025-11-30 08:13:01','2025-11-30 08:13:01');
/*!40000 ALTER TABLE `user_carts` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `users`
--

DROP TABLE IF EXISTS `users`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `users` (
  `Id` int(11) NOT NULL AUTO_INCREMENT,
  `FirstName` varchar(50) NOT NULL,
  `LastName` varchar(50) NOT NULL,
  `Email` varchar(100) NOT NULL,
  `Address` varchar(200) NOT NULL,
  `PhoneNumber` varchar(20) NOT NULL,
  `PasswordHash` varchar(255) NOT NULL,
  `CreatedAt` datetime DEFAULT current_timestamp(),
  `UpdatedAt` datetime DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  `IsActive` tinyint(1) NOT NULL DEFAULT 1,
  `IsAdmin` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `Email` (`Email`)
) ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `users`
--

LOCK TABLES `users` WRITE;
/*!40000 ALTER TABLE `users` DISABLE KEYS */;
INSERT INTO `users` VALUES (1,'System','Admin','admin@joel.com','','','$2a$11$YQsCB9BhaQ4vsdaq1HNoP.ITd0cnB4okwc4olNUbX5sLPDWsd4XGa','2025-11-20 15:06:27','2025-11-20 15:06:27',1,1),(2,'Bisha','Sama','bishatengen17@gmail.com','sa kanto lang','09123456789','$2a$11$KzhFv8mW719jgVNAzHvgK.a60C5/L.RcjR6rxNOYOGGZPZ9VrLk62','2025-11-20 16:14:09','2025-11-30 16:07:15',1,0),(3,'Boss ','Oleg','balanceking@gmail.com','jahan jahan, malapit na cho. ang talap, ang talap','09290653479','$2a$12$sMZrDJ8TPbWn/f9N1FaJB.H2Szb9685TJ6IhSqK/IyDUe/soBfVIO','2025-11-20 18:03:20','2025-11-23 14:08:10',1,0),(4,'Junie','Ricky','junjun@gmail.com','basta','09818218218','$2a$12$Fn.LQ8qqZu1JdY3E3GbJIe1EUMFM6nMO02fXD2xchSYCUx8nWHzMC','2025-11-22 17:36:08','2025-11-30 17:32:52',1,0);
/*!40000 ALTER TABLE `users` ENABLE KEYS */;
UNLOCK TABLES;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2025-11-30 20:44:33
