-- =====================================================================
-- ams_stockwatch :: 005 — signed RIS copies + default "Issued by"
-- Run once in MySQL Workbench. Safe to run again.
-- =====================================================================

USE ams_stockwatch;

-- The wet-signed RIS, scanned to PDF by AMS when the items are issued.
-- Stored in the database so both websites can show it and backups include it.
CREATE TABLE IF NOT EXISTS ris_signed_copies (
  ris_id             INT UNSIGNED NOT NULL PRIMARY KEY,
  original_file_name VARCHAR(255) NOT NULL,
  content            LONGBLOB     NOT NULL,
  size_bytes         INT UNSIGNED NOT NULL,
  uploaded_by        INT UNSIGNED NOT NULL,
  uploaded_at        DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT fk_signed_ris  FOREIGN KEY (ris_id)      REFERENCES ris_transactions (ris_id) ON DELETE CASCADE,
  CONSTRAINT fk_signed_user FOREIGN KEY (uploaded_by) REFERENCES users (user_id),
  CONSTRAINT chk_signed_size CHECK (size_bytes > 0 AND size_bytes <= 20971520)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Default "Issued by" printed on every slip (the AMS supply officer).
-- Placeholder until you choose the person; see the example below.
INSERT IGNORE INTO system_settings (setting_key, setting_value) VALUES ('ris.issued_by_personnel_id', '');

-- To set it, find the person's id in ro_personnel and run, for example:
--   UPDATE system_settings SET setting_value = '9' WHERE setting_key = 'ris.issued_by_personnel_id';

INSERT IGNORE INTO schema_migrations (version) VALUES ('005_signed_copies');
