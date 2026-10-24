-- -------------------------------------------------------------------
-- ams_stockwatch, 012: END USERS ON SPLV / SPHV DELIVERIES
-- Each delivered item can have an end user: an office or an RO personnel.
--   se_deliveries.end_user_type   'Office' or 'Personnel' (NULL means not recorded)
--   se_delivery_items.office_id / personnel_id / end_user (name saved as entered)
-- Run after 011.
-- Safe to run again.
-- -------------------------------------------------------------------

SET @has_col := (SELECT COUNT(*) FROM information_schema.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'se_deliveries' AND COLUMN_NAME = 'end_user_type');
SET @ddl := IF(@has_col = 0, 'ALTER TABLE se_deliveries ADD COLUMN end_user_type ENUM(''Office'',''Personnel'') NULL AFTER supplier_name', 'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @has_col := (SELECT COUNT(*) FROM information_schema.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'se_delivery_items' AND COLUMN_NAME = 'office_id');
SET @ddl := IF(@has_col = 0, 'ALTER TABLE se_delivery_items ADD COLUMN office_id INT UNSIGNED NULL AFTER unit_cost', 'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @has_col := (SELECT COUNT(*) FROM information_schema.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'se_delivery_items' AND COLUMN_NAME = 'personnel_id');
SET @ddl := IF(@has_col = 0, 'ALTER TABLE se_delivery_items ADD COLUMN personnel_id INT UNSIGNED NULL AFTER office_id', 'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @has_col := (SELECT COUNT(*) FROM information_schema.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'se_delivery_items' AND COLUMN_NAME = 'end_user');
SET @ddl := IF(@has_col = 0, 'ALTER TABLE se_delivery_items ADD COLUMN end_user VARCHAR(250) NULL AFTER personnel_id', 'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

INSERT IGNORE INTO schema_migrations (version) VALUES ('012_se_end_users');

-- Result: you should see 4 rows
SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = DATABASE() AND COLUMN_NAME IN ('end_user_type', 'office_id', 'personnel_id', 'end_user')
   AND TABLE_NAME IN ('se_deliveries', 'se_delivery_items');
