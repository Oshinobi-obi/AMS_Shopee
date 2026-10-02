-- =====================================================================
-- ams_stockwatch :: 003 — default "Approved by" signatory on the RIS
-- Sets Ernesto M. Magdaong Jr., Administrative Officer V (AMS) as the
-- approving official printed on every Requisition and Issue Slip.
-- To change it later, update system_settings 'ris.approved_by_personnel_id'
-- to another ro_personnel.personnel_id. Safe to run more than once.
-- =====================================================================

USE ams_stockwatch;

INSERT INTO system_settings (setting_key, setting_value)
SELECT 'ris.approved_by_personnel_id', CAST(p.personnel_id AS CHAR)
  FROM ro_personnel p
 WHERE p.full_name = 'Ernesto M. Magdaong Jr.'
 ORDER BY p.personnel_id
 LIMIT 1
ON DUPLICATE KEY UPDATE setting_value = VALUES(setting_value);

-- Check: should return Ernesto M. Magdaong Jr. / Administrative Officer V
SELECT s.setting_value AS personnel_id, p.full_name, p.position
  FROM system_settings s
  JOIN ro_personnel p ON p.personnel_id = CAST(s.setting_value AS UNSIGNED)
 WHERE s.setting_key = 'ris.approved_by_personnel_id';

INSERT IGNORE INTO schema_migrations (version) VALUES ('003_default_approver');
