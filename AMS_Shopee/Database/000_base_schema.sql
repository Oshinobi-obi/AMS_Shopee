-- =====================================================================
-- 000 — BASE SCHEMA for a brand-new ams_stockwatch database.
-- Run this FIRST on an empty database, then 001 → 006 in order.
-- (An existing database already has these tables: skip this file.)
--
-- Creates: users, offices, RO personnel, property types and documents,
-- login attempts, user activity and sign-in key storage.
--
-- Starter account (AMS StockWatch):  superadmin  /  ChangeMe@2026
-- It MUST be changed at the first sign-in (the site enforces this).
-- =====================================================================

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

CREATE TABLE IF NOT EXISTS `data_protection_keys` (
  `id` int unsigned NOT NULL AUTO_INCREMENT,
  `friendly_name` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `xml_data` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS `login_attempts` (
  `attempt_id` int unsigned NOT NULL AUTO_INCREMENT,
  `username` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `ip_address` varchar(45) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `was_successful` tinyint(1) NOT NULL,
  `attempted_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`attempt_id`),
  KEY `idx_username_time` (`username`,`attempted_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS `offices` (
  `office_id` int unsigned NOT NULL AUTO_INCREMENT,
  `office_name` varchar(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `office_acronym` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`office_id`),
  UNIQUE KEY `office_acronym` (`office_acronym`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS `property_documents` (
  `document_id` int unsigned NOT NULL AUTO_INCREMENT,
  `office_id` int unsigned NOT NULL,
  `document_type` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `property_type_id` int unsigned NOT NULL,
  `personnel_id` int unsigned NOT NULL,
  `file_path` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `original_file_name` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `uploaded_by` int unsigned NOT NULL,
  `uploaded_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`document_id`),
  KEY `property_type_id` (`property_type_id`),
  KEY `uploaded_by` (`uploaded_by`),
  KEY `idx_office` (`office_id`),
  KEY `idx_personnel` (`personnel_id`),
  CONSTRAINT `property_documents_ibfk_1` FOREIGN KEY (`office_id`) REFERENCES `offices` (`office_id`),
  CONSTRAINT `property_documents_ibfk_2` FOREIGN KEY (`property_type_id`) REFERENCES `property_types` (`property_type_id`),
  CONSTRAINT `property_documents_ibfk_3` FOREIGN KEY (`personnel_id`) REFERENCES `ro_personnel` (`personnel_id`),
  CONSTRAINT `property_documents_ibfk_4` FOREIGN KEY (`uploaded_by`) REFERENCES `users` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS `property_types` (
  `property_type_id` int unsigned NOT NULL AUTO_INCREMENT,
  `type_name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`property_type_id`),
  UNIQUE KEY `type_name` (`type_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS `ro_personnel` (
  `personnel_id` int unsigned NOT NULL AUTO_INCREMENT,
  `office_id` int unsigned NOT NULL,
  `full_name` varchar(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `position` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`personnel_id`),
  KEY `idx_office` (`office_id`),
  CONSTRAINT `ro_personnel_ibfk_1` FOREIGN KEY (`office_id`) REFERENCES `offices` (`office_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS `user_activities` (
  `activity_id` int NOT NULL AUTO_INCREMENT,
  `user_id` int unsigned NOT NULL,
  `activity_type` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `ip_address` varchar(45) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`activity_id`),
  KEY `idx_user_activities_user_id` (`user_id`),
  KEY `idx_user_activities_created_at` (`created_at`),
  CONSTRAINT `user_activities_ibfk_1` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS `users` (
  `user_id` int unsigned NOT NULL AUTO_INCREMENT,
  `username` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `email` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `full_name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `display_name` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `profile_picture_path` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `password_hash` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `role` enum('SuperAdmin','Admin') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Admin',
  `is_active` tinyint(1) NOT NULL DEFAULT '1',
  `require_password_change` tinyint(1) NOT NULL DEFAULT '0',
  `failed_login_attempts` int unsigned NOT NULL DEFAULT '0',
  `locked_until` datetime DEFAULT NULL,
  `last_login_at` datetime DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`user_id`),
  UNIQUE KEY `username` (`username`),
  UNIQUE KEY `email` (`email`),
  KEY `idx_username` (`username`),
  KEY `idx_role` (`role`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- The original offices (001 adds the remaining 16) and the two types of property
INSERT IGNORE INTO `offices` VALUES (8,'Asset Management Section','AMS');
INSERT IGNORE INTO `offices` VALUES (9,'Commission on Audit','COA');
INSERT IGNORE INTO `offices` VALUES (10,'Office of the Regional Director','ORD');
INSERT IGNORE INTO `offices` VALUES (11,'Office of the Assistant Regional Director','OARD');
INSERT IGNORE INTO `offices` VALUES (12,'Legal Unit','Legal');
INSERT IGNORE INTO `offices` VALUES (13,'Information and Communications Technology Unit','ICTU');
INSERT IGNORE INTO `offices` VALUES (14,'Public Affairs Unit','PAU');
INSERT IGNORE INTO `property_types` VALUES (2,'Property, Plant & Equipment (PPE)');
INSERT IGNORE INTO `property_types` VALUES (1,'Semi-Expendable (SE)');

-- Starter Super Admin (temporary password ChangeMe@2026, must be changed at first sign-in)
INSERT IGNORE INTO `users` (`user_id`, `username`, `email`, `full_name`, `display_name`, `password_hash`, `role`, `is_active`, `require_password_change`) VALUES (1, 'superadmin', 'superadmin@ams.local', 'AMS Super Admin', 'Super Admin', '$2b$12$1Ls5Ja48hMXLcPDcOm1mYuooYZ0e5baydqVGwPGRA2k3P/2sWyxDW', 'SuperAdmin', 1, 1);

SET FOREIGN_KEY_CHECKS = 1;
