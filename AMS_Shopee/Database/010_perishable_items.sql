-- =====================================================================
-- ams_stockwatch :: 010 — PERISHABLE SUPPLIES (MFD / EXP)
-- Some supplies expire (printer ink, toner, correction fluid, glue...).
-- Adds to supply_items:
--   is_perishable    1 = the item has an expiration date
--   manufactured_on  MFD, optional (some packs don't show it)
--   expires_on       EXP, required by AMS StockWatch when is_perishable = 1
-- Run this BEFORE publishing the updated websites: both read these columns.
-- Safe to run again. On MonsterASP: run it inside db64557 (no USE line).
-- =====================================================================

SET @has_col := (SELECT COUNT(*) FROM information_schema.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'supply_items' AND COLUMN_NAME = 'is_perishable');
SET @ddl := IF(@has_col = 0, 'ALTER TABLE supply_items ADD COLUMN is_perishable TINYINT(1) NOT NULL DEFAULT 0 AFTER reorder_level', 'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @has_col := (SELECT COUNT(*) FROM information_schema.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'supply_items' AND COLUMN_NAME = 'manufactured_on');
SET @ddl := IF(@has_col = 0, 'ALTER TABLE supply_items ADD COLUMN manufactured_on DATE NULL AFTER is_perishable', 'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @has_col := (SELECT COUNT(*) FROM information_schema.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'supply_items' AND COLUMN_NAME = 'expires_on');
SET @ddl := IF(@has_col = 0, 'ALTER TABLE supply_items ADD COLUMN expires_on DATE NULL AFTER manufactured_on', 'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

INSERT IGNORE INTO schema_migrations (version) VALUES ('010_perishable_items');

-- Result: the new columns
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT
  FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'supply_items'
   AND COLUMN_NAME IN ('is_perishable', 'manufactured_on', 'expires_on');
