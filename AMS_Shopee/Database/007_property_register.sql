-- =====================================================================
-- ams_stockwatch :: 007 — PROPERTY REGISTER
-- One row per property (or per lot of identical semi-expendable items),
-- with its status, classification, cost and who is accountable for it.
-- Feeds the property charts on the AMS StockWatch dashboard.
-- Safe to run again. On MonsterASP, run it inside db64557 (no USE line).
-- =====================================================================

CREATE TABLE IF NOT EXISTS property_items (
  property_id      INT UNSIGNED  NOT NULL AUTO_INCREMENT PRIMARY KEY,
  property_no      VARCHAR(50)   NOT NULL,
  description      VARCHAR(500)  NOT NULL,
  classification   VARCHAR(60)   NOT NULL,
  property_type_id INT UNSIGNED  NOT NULL,                 -- 1 = Semi-Expendable, 2 = PPE
  quantity         INT UNSIGNED  NOT NULL DEFAULT 1,
  unit_cost        DECIMAL(14,2) NOT NULL DEFAULT 0.00,
  acquired_on      DATE          NULL,
  office_id        INT UNSIGNED  NULL,
  personnel_id     INT UNSIGNED  NULL,                     -- accountable person; NULL = unassigned
  status           VARCHAR(30)   NOT NULL DEFAULT 'Serviceable',
  document_id      INT UNSIGNED  NULL,                     -- the PAR / ICS it is recorded on (optional)
  remarks          VARCHAR(500)  NULL,
  created_by       INT UNSIGNED  NULL,
  created_at       DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at       DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uq_property_no (property_no),
  KEY idx_prop_status (status),
  KEY idx_prop_office (office_id),
  KEY idx_prop_class (classification),
  CONSTRAINT fk_prop_type   FOREIGN KEY (property_type_id) REFERENCES property_types (property_type_id),
  CONSTRAINT fk_prop_office FOREIGN KEY (office_id)        REFERENCES offices (office_id),
  CONSTRAINT fk_prop_person FOREIGN KEY (personnel_id)     REFERENCES ro_personnel (personnel_id) ON DELETE SET NULL,
  CONSTRAINT fk_prop_doc    FOREIGN KEY (document_id)      REFERENCES property_documents (document_id) ON DELETE SET NULL,
  CONSTRAINT fk_prop_user   FOREIGN KEY (created_by)       REFERENCES users (user_id) ON DELETE SET NULL,
  CONSTRAINT chk_prop_qty    CHECK (quantity >= 1),
  CONSTRAINT chk_prop_cost   CHECK (unit_cost >= 0),
  CONSTRAINT chk_prop_status CHECK (status IN ('Serviceable','For Repair','Under Repair','For Disposal',
                                               'Unserviceable','Missing/Unaccounted','Transferred',
                                               'Returned','Newly Acquired'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT IGNORE INTO schema_migrations (version) VALUES ('007_property_register');
