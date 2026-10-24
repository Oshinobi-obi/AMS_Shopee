-- -------------------------------------------------------------------
-- ams_stockwatch, 014: RETURNED PROPERTY ON PAR / ICS
-- This is for the "Returned" switch on Property documents. When property is returned,
-- its signed Return Slip is uploaded and the PAR / ICS records the return.
--   property_documents.returned_on      DATE (date signed on the return slip, NULL means not returned)
--   property_documents.return_slip_id   the Return Slip document uploaded for it
-- Run after 013. Safe to run again.
-- -------------------------------------------------------------------

SET @has_col := (SELECT COUNT(*) FROM information_schema.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'property_documents' AND COLUMN_NAME = 'returned_on');
SET @ddl := IF(@has_col = 0, 'ALTER TABLE property_documents ADD COLUMN returned_on DATE NULL AFTER date_signed', 'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @has_col := (SELECT COUNT(*) FROM information_schema.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'property_documents' AND COLUMN_NAME = 'return_slip_id');
SET @ddl := IF(@has_col = 0, 'ALTER TABLE property_documents ADD COLUMN return_slip_id INT UNSIGNED NULL AFTER returned_on', 'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

INSERT IGNORE INTO schema_migrations (version) VALUES ('014_property_returns');

-- Result: you should see 2 rows
SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'property_documents' AND COLUMN_NAME IN ('returned_on', 'return_slip_id');
