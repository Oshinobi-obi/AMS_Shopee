-- -------------------------------------------------------------------
-- ams_stockwatch, 016: LRPO REQUESTS SUPPLIES ON THE ESSD APP-CSE
-- LRPO (Learner Rights and Protection Office) is under ESSD. It can now submit requisitions,
-- and what it requests and receives counts against the APP-CSE of ESSD.
--   offices.appcse_office_id   the office whose APP-CSE this office uses (NULL means its own)
--   LRPO: RIS code 024, can request supplies, uses the APP-CSE of ESSD
-- Run after 015. Safe to run again.
-- -------------------------------------------------------------------

SET @has_col := (SELECT COUNT(*) FROM information_schema.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'offices' AND COLUMN_NAME = 'appcse_office_id');
SET @ddl := IF(@has_col = 0, 'ALTER TABLE offices ADD COLUMN appcse_office_id INT UNSIGNED NULL AFTER appcse_sheet', 'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @essd := (SELECT office_id FROM offices WHERE office_acronym = 'ESSD' LIMIT 1);
UPDATE offices
   SET ris_code = '024', can_requisition = 1, appcse_office_id = @essd
 WHERE office_acronym = 'LRPO' AND @essd IS NOT NULL;

INSERT IGNORE INTO schema_migrations (version) VALUES ('016_lrpo_shares_essd_appcse');

-- Result: LRPO with code 024, can request = 1, APP-CSE of ESSD
SELECT o.office_acronym, o.ris_code, o.can_requisition, a.office_acronym AS uses_appcse_of
  FROM offices o LEFT JOIN offices a ON a.office_id = o.appcse_office_id
 WHERE o.office_acronym IN ('LRPO', 'ESSD');
