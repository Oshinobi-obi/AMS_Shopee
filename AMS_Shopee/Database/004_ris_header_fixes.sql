-- =====================================================================
-- ams_stockwatch :: 004 — RIS header: clean entity name, blank Fund Cluster
-- Safe to run more than once.
-- =====================================================================

USE ams_stockwatch;

UPDATE system_settings SET setting_value = 'Department of Education - Regional Office'
 WHERE setting_key = 'ris.entity_name';

-- Fund Cluster is left blank so it can be filled in on the printed slip.
UPDATE system_settings SET setting_value = ''
 WHERE setting_key = 'ris.default_fund_cluster';

-- Requisitions already submitted while testing keep a copy of the old header;
-- correct those copies too (only the pending ones, which are still test data).
UPDATE ris_transactions
   SET entity_name  = 'Department of Education - Regional Office',
       fund_cluster = ''
 WHERE status = 'PendingApproval'
   AND ris_id > 0;

SELECT setting_key, CONCAT('[', setting_value, ']') AS value
  FROM system_settings
 WHERE setting_key IN ('ris.entity_name', 'ris.default_fund_cluster');

INSERT IGNORE INTO schema_migrations (version) VALUES ('004_ris_header_fixes');
