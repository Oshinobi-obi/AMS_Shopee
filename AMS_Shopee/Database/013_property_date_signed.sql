-- -------------------------------------------------------------------
-- ams_stockwatch, 013: DATE SIGNED ON PROPERTY DOCUMENTS
-- This saves the date written on each signed PAR, ICS or return slip.
--   property_documents.date_signed   DATE (NULL means uploaded before I added this)
-- Run after 012. Safe to run again.
-- -------------------------------------------------------------------

SET @has_col := (SELECT COUNT(*) FROM information_schema.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'property_documents' AND COLUMN_NAME = 'date_signed');
SET @ddl := IF(@has_col = 0, 'ALTER TABLE property_documents ADD COLUMN date_signed DATE NULL AFTER original_file_name', 'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

INSERT IGNORE INTO schema_migrations (version) VALUES ('013_property_date_signed');

-- Result: you should see 1 row
SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'property_documents' AND COLUMN_NAME = 'date_signed';
