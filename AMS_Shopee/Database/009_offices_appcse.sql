-- =====================================================================
-- ams_stockwatch :: 009 — OFFICES MATCH THE APP-CSE
-- Makes the office list match the sheets of the Regional Office's APP-CSE
-- workbook, and adds "appcse_sheet" for offices whose sheet has another name.
-- Safe to run again. On MonsterASP: run it inside db64557 (no USE line).
-- =====================================================================

-- 1) Sheet name used for the office in the APP-CSE workbook (NULL = same as the acronym)
SET @has_col := (SELECT COUNT(*) FROM information_schema.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'offices' AND COLUMN_NAME = 'appcse_sheet');
SET @ddl := IF(@has_col = 0, 'ALTER TABLE offices ADD COLUMN appcse_sheet VARCHAR(50) NULL AFTER office_acronym', 'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 2) OARD is called ARD (same office, same id: its personnel, documents and accounts stay)
SET @has := (SELECT COUNT(*) FROM offices WHERE office_acronym = 'ARD');
UPDATE offices SET office_acronym = 'ARD' WHERE office_acronym = 'OARD' AND @has = 0;

-- 3) ADMIN is the Administrative Services Division (ASD)
SET @has := (SELECT COUNT(*) FROM offices WHERE office_acronym = 'ASD');
UPDATE offices SET office_acronym = 'ASD', office_name = 'Administrative Services Division', office_type = 'Division'
 WHERE office_acronym = 'ADMIN' AND @has = 0;

-- 4) Offices that have an APP-CSE sheet but were missing
INSERT INTO offices (office_name, office_acronym, office_type, can_requisition, is_active, sort_order)
SELECT * FROM (
    SELECT 'Finance Division' AS n, 'FINANCE' AS a, 'Division' AS t, 1 AS c, 1 AS act, 40 AS s UNION ALL
    SELECT 'General Services Unit', 'GSU', 'Unit', 1, 1, 41 UNION ALL
    SELECT 'Records Unit', 'RECORDS', 'Unit', 1, 1, 42 UNION ALL
    SELECT 'Regional Payroll Services Unit', 'RPSU', 'Unit', 1, 1, 43
) AS new_offices
WHERE NOT EXISTS (SELECT 1 FROM offices o WHERE o.office_acronym = new_offices.a);

-- Where the new units belong (shown as "Division" on their RIS). Change here if different.
SET @asd := (SELECT office_id FROM offices WHERE office_acronym = 'ASD');
SET @fin := (SELECT office_id FROM offices WHERE office_acronym = 'FINANCE');
UPDATE offices SET parent_office_id = @asd WHERE office_acronym IN ('GSU', 'RECORDS') AND parent_office_id IS NULL;
UPDATE offices SET parent_office_id = @fin WHERE office_acronym = 'RPSU' AND parent_office_id IS NULL;

-- 5) Sheets named differently from the office acronym
UPDATE offices SET appcse_sheet = 'SHNU' WHERE office_acronym = 'ESSD-SHNU';
UPDATE offices SET appcse_sheet = 'LRMS' WHERE office_acronym = 'CLMD-LRMS';
UPDATE offices SET appcse_sheet = 'ICT'  WHERE office_acronym = 'ICTU';

-- 6) Units that request supplies through their division's APP-CSE
SET @essd := (SELECT office_id FROM offices WHERE office_acronym = 'ESSD');
SET @hrdd := (SELECT office_id FROM offices WHERE office_acronym = 'HRDD');
UPDATE offices SET parent_office_id = @essd, can_requisition = 0 WHERE office_acronym IN ('ESSD-EFS', 'ESSD-SPPS', 'LRPO');
UPDATE offices SET parent_office_id = @hrdd, can_requisition = 0 WHERE office_acronym = 'HRDD-NEAP';

-- 7) COA has its own APP-CSE sheet, so it requests supplies too
UPDATE offices SET can_requisition = 1 WHERE office_acronym = 'COA';

INSERT IGNORE INTO schema_migrations (version) VALUES ('009_offices_appcse');

-- Result: offices that request supplies, and the APP-CSE sheet each one reads
SELECT office_acronym, office_name, COALESCE(appcse_sheet, office_acronym) AS appcse_sheet,
       can_requisition, (SELECT p.office_acronym FROM offices p WHERE p.office_id = o.parent_office_id) AS under
  FROM offices o ORDER BY can_requisition DESC, office_acronym;
