-- -------------------------------------------------------------------
-- ams_stockwatch, 011: SPLV / SPHV DELIVERIES (ICS and PAR numbers)
-- Semi-expendable property delivered to AMS. Each item line gets a number:
--   SPLV               ICS
--   SPHV below 50,000  ICS      SPHV 50,000 and up  PAR
-- Number = year-month delivered - running number(s), for example 2026-01-001-007 for 7 units.
-- The running number keeps counting the whole year (separately for SPLV ICS, SPHV ICS and
-- SPHV PAR) and goes back to 001 every new year.
-- Safe to run again. On MonsterASP use 011_semi_expendable_deliveries_MonsterASP.sql.
-- -------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS se_deliveries (
  delivery_id    INT UNSIGNED  NOT NULL AUTO_INCREMENT,
  category       ENUM('SPLV','SPHV') NOT NULL,
  delivered_on   DATE          NOT NULL,
  supplier_id    INT UNSIGNED  NULL,
  supplier_name  VARCHAR(200)  NOT NULL,
  reference_no   VARCHAR(100)  NULL,                 -- DR / invoice / PO no.
  remarks        VARCHAR(500)  NULL,
  total_amount   DECIMAL(14,2) NOT NULL DEFAULT 0,
  created_by     INT UNSIGNED  NULL,
  created_at     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (delivery_id),
  KEY ix_sed_date (delivered_on),
  KEY ix_sed_cat (category, delivered_on)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS se_delivery_items (
  line_id      INT UNSIGNED      NOT NULL AUTO_INCREMENT,
  delivery_id  INT UNSIGNED      NOT NULL,
  line_no      SMALLINT UNSIGNED NOT NULL,
  description  VARCHAR(500)      NOT NULL,
  unit         VARCHAR(30)       NOT NULL,
  quantity     INT UNSIGNED      NOT NULL,
  unit_cost    DECIMAL(14,2)     NOT NULL,
  category     ENUM('SPLV','SPHV') NOT NULL,
  doc_type     ENUM('ICS','PAR') NOT NULL,
  doc_year     SMALLINT UNSIGNED NOT NULL,
  seq_start    INT UNSIGNED      NOT NULL,
  seq_end      INT UNSIGNED      NOT NULL,
  doc_no       VARCHAR(40)       NOT NULL,           -- for example 2026-01-001-007
  PRIMARY KEY (line_id),
  UNIQUE KEY ux_sedi_number (doc_year, category, doc_type, seq_start),
  KEY ix_sedi_delivery (delivery_id),
  KEY ix_sedi_no (doc_no),
  CONSTRAINT fk_sedi_delivery FOREIGN KEY (delivery_id) REFERENCES se_deliveries (delivery_id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Last running number used, per year and series
CREATE TABLE IF NOT EXISTS se_number_series (
  doc_year   SMALLINT UNSIGNED   NOT NULL,
  category   ENUM('SPLV','SPHV') NOT NULL,
  doc_type   ENUM('ICS','PAR')   NOT NULL,
  last_seq   INT UNSIGNED        NOT NULL DEFAULT 0,
  updated_at DATETIME            NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (doc_year, category, doc_type)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT IGNORE INTO schema_migrations (version) VALUES ('011_semi_expendable_deliveries');

SELECT 'se_deliveries, se_delivery_items and se_number_series are ready' AS result;
