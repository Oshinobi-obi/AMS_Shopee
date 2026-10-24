-- -------------------------------------------------------------------
-- ams_stockwatch, 015: RIS CODES OF THE OFFICES
-- The code written in the RIS No. column of the RSMI (one per office):
--   01 ORD, 02 ARD, 03 ASD, 04 PERSONNEL, 05 AMS, 06 RECORDS, 07 CASH, 08 LEGAL,
--   09 PPRD, 010 FTAD, 011 SHNU, 012 HRDD, 013 QAD, 014 CLMD, 015 FINANCE, 016 RPSU,
--   017 ICTU, 018 LRMD, 019 GSU, 020 COA, 021 PAU, 022 ESSD
-- A unit without its own code (like ESSD-EFS or PROCUREMENT) uses its division's code.
-- Codes can be changed later in e-PINS: Settings > Offices > RIS code.
-- Run after 014. Safe to run again (codes already set are kept).
-- -------------------------------------------------------------------

SET @has_col := (SELECT COUNT(*) FROM information_schema.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'offices' AND COLUMN_NAME = 'ris_code');
SET @ddl := IF(@has_col = 0, 'ALTER TABLE offices ADD COLUMN ris_code VARCHAR(5) NULL AFTER responsibility_center_code', 'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

UPDATE offices o
  JOIN (
      SELECT 'ORD' AS a, '01' AS c UNION ALL SELECT 'ARD', '02' UNION ALL SELECT 'ASD', '03' UNION ALL
      SELECT 'PERSONNEL', '04' UNION ALL SELECT 'AMS', '05' UNION ALL SELECT 'RECORDS', '06' UNION ALL
      SELECT 'CASH', '07' UNION ALL SELECT 'LEGAL', '08' UNION ALL SELECT 'PPRD', '09' UNION ALL
      SELECT 'FTAD', '010' UNION ALL SELECT 'SHNU', '011' UNION ALL SELECT 'ESSD-SHNU', '011' UNION ALL
      SELECT 'HRDD', '012' UNION ALL SELECT 'QAD', '013' UNION ALL SELECT 'CLMD', '014' UNION ALL
      SELECT 'FINANCE', '015' UNION ALL SELECT 'RPSU', '016' UNION ALL SELECT 'ICTU', '017' UNION ALL
      SELECT 'LRMD', '018' UNION ALL SELECT 'LRMS', '018' UNION ALL SELECT 'CLMD-LRMS', '018' UNION ALL
      SELECT 'GSU', '019' UNION ALL SELECT 'COA', '020' UNION ALL SELECT 'PAU', '021' UNION ALL
      SELECT 'ESSD', '022'
  ) AS codes ON UPPER(o.office_acronym) = codes.a
   SET o.ris_code = codes.c
 WHERE o.ris_code IS NULL OR o.ris_code = '';

INSERT IGNORE INTO schema_migrations (version) VALUES ('015_office_ris_codes');

-- Result: every office and its RIS code (empty means it uses its division's code, or set it in Settings)
SELECT o.office_acronym, o.ris_code, p.office_acronym AS part_of, p.ris_code AS division_code
  FROM offices o LEFT JOIN offices p ON p.office_id = o.parent_office_id
 ORDER BY CAST(COALESCE(NULLIF(o.ris_code, ''), p.ris_code, '999') AS UNSIGNED), o.office_acronym;
