-- =====================================================================
-- ams_stockwatch :: Migration 001 — Requisition Portal (APP-CSE + RIS)
-- Target  : MySQL 8.0.16+ (CHECK constraints are enforced from 8.0.16)
-- Run ONCE. MySQL DDL auto-commits, so this script is NOT atomic.
-- Back up first:
--   mysqldump -u root -p --routines --triggers ams_stockwatch > pre_001.sql
-- =====================================================================

USE ams_stockwatch;
SET NAMES utf8mb4;

-- ---------------------------------------------------------------------
-- 0. Migration bookkeeping
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS schema_migrations (
  version     VARCHAR(50)  NOT NULL PRIMARY KEY,
  applied_at  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- 1. OFFICES — hierarchy + requisition flag (keeps existing rows/FKs)
-- ---------------------------------------------------------------------
ALTER TABLE offices
  ADD COLUMN parent_office_id INT UNSIGNED NULL AFTER office_acronym,
  ADD COLUMN office_type ENUM('Office','Division','Section','Unit','External')
             NOT NULL DEFAULT 'Unit' AFTER parent_office_id,
  ADD COLUMN responsibility_center_code VARCHAR(30) NULL,
  ADD COLUMN can_requisition TINYINT(1) NOT NULL DEFAULT 1,
  ADD COLUMN is_active       TINYINT(1) NOT NULL DEFAULT 1,
  ADD COLUMN sort_order      SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  ADD KEY idx_offices_parent (parent_office_id),
  ADD CONSTRAINT fk_offices_parent
      FOREIGN KEY (parent_office_id) REFERENCES offices (office_id);

-- Existing rows (ids 8–14). COA stays (ro_personnel references it) but
-- cannot requisition.
UPDATE offices SET office_type='Office', sort_order=1  WHERE office_acronym='ORD';
UPDATE offices SET office_type='Office', sort_order=2  WHERE office_acronym='OARD';
UPDATE offices SET office_type='Unit',   sort_order=3  WHERE office_acronym='Legal';
UPDATE offices SET office_type='Unit',   sort_order=4  WHERE office_acronym='ICTU';
UPDATE offices SET office_type='Unit',   sort_order=5  WHERE office_acronym='PAU';
UPDATE offices SET office_type='Section',sort_order=22 WHERE office_acronym='AMS';
UPDATE offices SET office_type='External', can_requisition=0, sort_order=99
 WHERE office_acronym='COA';

-- The 16 offices not yet in the table. Review names/acronyms before running.
INSERT INTO offices (office_name, office_acronym, office_type, sort_order) VALUES
 ('Learner Rights and Protection Office',              'LRPO',        'Office',   6),
 ('Curriculum and Learning Management Division',       'CLMD',        'Division', 7),
 ('Learning Resource Management Section',              'CLMD-LRMS',   'Section',  8),
 ('Education Support Services Division',               'ESSD',        'Division', 9),
 ('Special Programs and Projects Section',             'ESSD-SPPS',   'Section', 10),
 ('School Health and Nutrition Unit',                  'ESSD-SHNU',   'Unit',    11),
 ('Education Facilities Section',                      'ESSD-EFS',    'Section', 12),
 ('Field Technical Assistance Division',               'FTAD',        'Division',13),
 ('Quality Assurance Division',                        'QAD',         'Division',14),
 ('Human Resource Development Division',               'HRDD',        'Division',15),
 ('National Educators Academy of the Philippines',     'HRDD-NEAP',   'Unit',    16),
 ('Policy, Planning and Research Division',            'PPRD',        'Division',17),
 ('Administrative Division',                           'ADMIN',       'Division',18),
 ('Cash Section',                                      'CASH',        'Section', 19),
 ('Personnel Section',                                 'PERSONNEL',   'Section', 20),
 ('Procurement Unit',                                  'PROCUREMENT', 'Unit',    21);

-- Parent links (drive the "Division" vs "Office" fields on the RIS)
UPDATE offices c JOIN offices p ON p.office_acronym='CLMD'
   SET c.parent_office_id=p.office_id WHERE c.office_acronym='CLMD-LRMS';
UPDATE offices c JOIN offices p ON p.office_acronym='ESSD'
   SET c.parent_office_id=p.office_id
 WHERE c.office_acronym IN ('ESSD-SPPS','ESSD-SHNU','ESSD-EFS');
UPDATE offices c JOIN offices p ON p.office_acronym='HRDD'
   SET c.parent_office_id=p.office_id WHERE c.office_acronym='HRDD-NEAP';
UPDATE offices c JOIN offices p ON p.office_acronym='ADMIN'
   SET c.parent_office_id=p.office_id
 WHERE c.office_acronym IN ('CASH','PERSONNEL','PROCUREMENT','AMS');

-- ---------------------------------------------------------------------
-- 2. USERS — add office link and an 'Office' (client) role
-- ---------------------------------------------------------------------
ALTER TABLE users
  MODIFY COLUMN role ENUM('SuperAdmin','Admin','Office')
         COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Office',
  ADD COLUMN office_id INT UNSIGNED NULL AFTER role,
  ADD KEY idx_users_office (office_id),
  ADD CONSTRAINT fk_users_office FOREIGN KEY (office_id) REFERENCES offices (office_id);

UPDATE users
   SET office_id = (SELECT office_id FROM offices WHERE office_acronym='AMS')
 WHERE role IN ('SuperAdmin','Admin');

ALTER TABLE users
  ADD CONSTRAINT chk_users_office_role CHECK (role <> 'Office' OR office_id IS NOT NULL);

-- ---------------------------------------------------------------------
-- 3. SYSTEM SETTINGS (RIS header values, thresholds)
-- ---------------------------------------------------------------------
CREATE TABLE system_settings (
  setting_key   VARCHAR(100) NOT NULL PRIMARY KEY,
  setting_value VARCHAR(500) NOT NULL,
  updated_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO system_settings (setting_key, setting_value) VALUES
 ('ris.entity_name',          'Department of Education - Regional Office'),
 ('ris.default_fund_cluster', ''),
 ('ris.number_format',        'yyyy-MM-{seq:0000}'),
 ('stock.default_reorder',    '10'),
 ('cart.allow_beyond_stock',  'true');

-- ---------------------------------------------------------------------
-- 4. CATALOG
-- ---------------------------------------------------------------------
CREATE TABLE item_categories (
  category_id   INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  category_name VARCHAR(100) NOT NULL,
  icon          VARCHAR(50)  NULL,
  sort_order    SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  UNIQUE KEY uq_category_name (category_name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE suppliers (
  supplier_id    INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  supplier_name  VARCHAR(200) NOT NULL,
  contact_person VARCHAR(150) NULL,
  contact_no     VARCHAR(50)  NULL,
  is_active      TINYINT(1)   NOT NULL DEFAULT 1,
  UNIQUE KEY uq_supplier_name (supplier_name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE supply_items (
  item_id          INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  stock_no         VARCHAR(30)  NOT NULL,
  item_name        VARCHAR(200) NOT NULL,
  specifications   TEXT         NULL,
  unit_of_measure  VARCHAR(30)  NOT NULL,
  category_id      INT UNSIGNED NULL,
  supplier_id      INT UNSIGNED NULL,
  property_type_id INT UNSIGNED NULL,          -- NULL for consumables; SE/PPE if ever listed
  unit_price       DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  image_path       VARCHAR(500) NULL,
  stock_on_hand    INT          NOT NULL DEFAULT 0,  -- live AMS warehouse count
  reorder_level    INT UNSIGNED NOT NULL DEFAULT 10, -- <= this => yellow badge
  is_active        TINYINT(1)   NOT NULL DEFAULT 1,
  row_version      INT UNSIGNED NOT NULL DEFAULT 0,  -- EF concurrency token for admin edits
  created_at       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uq_stock_no (stock_no),
  KEY idx_items_category (category_id),
  KEY idx_items_active (is_active, item_name),
  FULLTEXT KEY ft_items (item_name, specifications),
  CONSTRAINT fk_items_category FOREIGN KEY (category_id) REFERENCES item_categories (category_id),
  CONSTRAINT fk_items_supplier FOREIGN KEY (supplier_id) REFERENCES suppliers (supplier_id),
  CONSTRAINT fk_items_ptype    FOREIGN KEY (property_type_id) REFERENCES property_types (property_type_id),
  CONSTRAINT chk_items_stock   CHECK (stock_on_hand >= 0),
  CONSTRAINT chk_items_price   CHECK (unit_price >= 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- 5. APP-CSE UPLOAD BATCHES (audit trail of each spreadsheet AMS loads)
-- ---------------------------------------------------------------------
CREATE TABLE app_cse_uploads (
  upload_id          INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  fiscal_year        SMALLINT UNSIGNED NOT NULL,
  original_file_name VARCHAR(255) NOT NULL,
  file_path          VARCHAR(500) NOT NULL,
  row_count          INT UNSIGNED NOT NULL DEFAULT 0,
  notes              VARCHAR(500) NULL,
  uploaded_by        INT UNSIGNED NOT NULL,
  uploaded_at        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  KEY idx_upload_year (fiscal_year),
  CONSTRAINT fk_upload_user FOREIGN KEY (uploaded_by) REFERENCES users (user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- 6. APP-CSE ALLOCATIONS (one row per office × item × year)
--    remaining = allocated_qty - issued_qty - (qty in PendingApproval RIS)
-- ---------------------------------------------------------------------
CREATE TABLE app_cse_allocations (
  allocation_id INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  office_id     INT UNSIGNED NOT NULL,
  item_id       INT UNSIGNED NOT NULL,
  fiscal_year   SMALLINT UNSIGNED NOT NULL,
  allocated_qty INT UNSIGNED NOT NULL,
  q1_qty INT UNSIGNED NULL, q2_qty INT UNSIGNED NULL,   -- optional quarterly
  q3_qty INT UNSIGNED NULL, q4_qty INT UNSIGNED NULL,   -- breakdown from APP
  issued_qty    INT UNSIGNED NOT NULL DEFAULT 0,        -- bumped on approval
  upload_id     INT UNSIGNED NULL,
  updated_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uq_alloc (office_id, item_id, fiscal_year),
  KEY idx_alloc_item_year (item_id, fiscal_year),
  CONSTRAINT fk_alloc_office FOREIGN KEY (office_id) REFERENCES offices (office_id),
  CONSTRAINT fk_alloc_item   FOREIGN KEY (item_id)   REFERENCES supply_items (item_id),
  CONSTRAINT fk_alloc_upload FOREIGN KEY (upload_id) REFERENCES app_cse_uploads (upload_id),
  CONSTRAINT chk_alloc_issued CHECK (issued_qty <= allocated_qty)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- 7. CART (server-side, survives refresh / circuit reconnect)
-- ---------------------------------------------------------------------
CREATE TABLE cart_items (
  cart_item_id INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  user_id      INT UNSIGNED NOT NULL,
  item_id      INT UNSIGNED NOT NULL,
  quantity     INT UNSIGNED NOT NULL,
  remarks      VARCHAR(255) NULL,
  added_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uq_cart_user_item (user_id, item_id),
  CONSTRAINT fk_cart_user FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE CASCADE,
  CONSTRAINT fk_cart_item FOREIGN KEY (item_id) REFERENCES supply_items (item_id),
  CONSTRAINT chk_cart_qty CHECK (quantity > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- 8. RIS (Appendix 63)
-- ---------------------------------------------------------------------
CREATE TABLE ris_number_sequences (
  fiscal_year SMALLINT UNSIGNED NOT NULL,
  seq_month   TINYINT UNSIGNED  NOT NULL,
  last_seq    INT UNSIGNED      NOT NULL DEFAULT 0,
  PRIMARY KEY (fiscal_year, seq_month)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE ris_transactions (
  ris_id                     INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  ris_no                     VARCHAR(30)  NOT NULL,
  office_id                  INT UNSIGNED NOT NULL,
  fiscal_year                SMALLINT UNSIGNED NOT NULL,
  -- header snapshot (frozen at submission so later edits don't alter the slip)
  entity_name                VARCHAR(200) NOT NULL,
  fund_cluster               VARCHAR(10)  NOT NULL,
  division_name              VARCHAR(150) NOT NULL,
  office_name                VARCHAR(150) NOT NULL,
  responsibility_center_code VARCHAR(30)  NULL,
  purpose                    VARCHAR(500) NOT NULL,
  status ENUM('PendingApproval','ApprovedForIssuance','Issued','Rejected','Cancelled')
         NOT NULL DEFAULT 'PendingApproval',
  requested_by_user_id       INT UNSIGNED NOT NULL,
  requested_by_personnel_id  INT UNSIGNED NULL,
  requested_at               DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  approved_by_user_id        INT UNSIGNED NULL,   -- AMS account that clicked Approve
  approved_by_personnel_id   INT UNSIGNED NULL,   -- signatory printed on the slip
  approved_at                DATETIME NULL,
  issued_by_personnel_id     INT UNSIGNED NULL,
  received_by_personnel_id   INT UNSIGNED NULL,
  issued_at                  DATETIME NULL,
  rejected_by_user_id        INT UNSIGNED NULL,
  rejected_at                DATETIME NULL,
  rejection_reason           VARCHAR(1000) NULL,
  pdf_path                   VARCHAR(500) NULL,
  row_version                INT UNSIGNED NOT NULL DEFAULT 0,
  created_at                 DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at                 DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uq_ris_no (ris_no),
  KEY idx_ris_status (status, requested_at),
  KEY idx_ris_office_year (office_id, fiscal_year, status),
  CONSTRAINT fk_ris_office     FOREIGN KEY (office_id)                REFERENCES offices (office_id),
  CONSTRAINT fk_ris_req_user   FOREIGN KEY (requested_by_user_id)     REFERENCES users (user_id),
  CONSTRAINT fk_ris_req_pers   FOREIGN KEY (requested_by_personnel_id) REFERENCES ro_personnel (personnel_id),
  CONSTRAINT fk_ris_appr_user  FOREIGN KEY (approved_by_user_id)      REFERENCES users (user_id),
  CONSTRAINT fk_ris_appr_pers  FOREIGN KEY (approved_by_personnel_id) REFERENCES ro_personnel (personnel_id),
  CONSTRAINT fk_ris_iss_pers   FOREIGN KEY (issued_by_personnel_id)   REFERENCES ro_personnel (personnel_id),
  CONSTRAINT fk_ris_rcv_pers   FOREIGN KEY (received_by_personnel_id) REFERENCES ro_personnel (personnel_id),
  CONSTRAINT fk_ris_rej_user   FOREIGN KEY (rejected_by_user_id)      REFERENCES users (user_id),
  CONSTRAINT chk_ris_reject_reason CHECK (status <> 'Rejected' OR rejection_reason IS NOT NULL)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE ris_items (
  ris_item_id      INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  ris_id           INT UNSIGNED NOT NULL,
  item_id          INT UNSIGNED NOT NULL,
  line_no          SMALLINT UNSIGNED NOT NULL,
  -- snapshots (catalog may change later; the slip must not)
  stock_no         VARCHAR(30)  NOT NULL,
  unit_of_measure  VARCHAR(30)  NOT NULL,
  item_description VARCHAR(700) NOT NULL,
  unit_cost        DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  requested_qty    INT UNSIGNED NOT NULL,
  stock_available  TINYINT(1)   NULL,   -- RIS "Stock Available? Yes/No"
  issued_qty       INT UNSIGNED NULL,   -- may be < requested (partial issue)
  remarks          VARCHAR(255) NULL,
  UNIQUE KEY uq_ris_item (ris_id, item_id),
  KEY idx_ris_items_item (item_id),
  CONSTRAINT fk_risitem_ris  FOREIGN KEY (ris_id)  REFERENCES ris_transactions (ris_id) ON DELETE CASCADE,
  CONSTRAINT fk_risitem_item FOREIGN KEY (item_id) REFERENCES supply_items (item_id),
  CONSTRAINT chk_risitem_req CHECK (requested_qty > 0),
  CONSTRAINT chk_risitem_iss CHECK (issued_qty IS NULL OR issued_qty <= requested_qty)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE ris_status_history (
  history_id  BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  ris_id      INT UNSIGNED NOT NULL,
  from_status VARCHAR(30)  NULL,
  to_status   VARCHAR(30)  NOT NULL,
  changed_by  INT UNSIGNED NOT NULL,
  note        VARCHAR(1000) NULL,
  changed_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  KEY idx_hist_ris (ris_id, changed_at),
  CONSTRAINT fk_hist_ris  FOREIGN KEY (ris_id)     REFERENCES ris_transactions (ris_id) ON DELETE CASCADE,
  CONSTRAINT fk_hist_user FOREIGN KEY (changed_by) REFERENCES users (user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Stock ledger: every change to supply_items.stock_on_hand gets a row.
-- Feeds Stock Card, Supplies Ledger Card and RSMI reports.
CREATE TABLE stock_movements (
  movement_id    BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  item_id        INT UNSIGNED NOT NULL,
  movement_type  ENUM('OPENING','RECEIPT','ISSUE','RETURN','ADJUSTMENT_IN','ADJUSTMENT_OUT') NOT NULL,
  quantity       INT UNSIGNED NOT NULL,
  balance_after  INT NOT NULL,
  unit_cost      DECIMAL(12,2) NULL,
  reference_type VARCHAR(20) NULL,      -- 'RIS', 'IAR', 'ADJ', ...
  reference_no   VARCHAR(50) NULL,
  ris_id         INT UNSIGNED NULL,
  office_id      INT UNSIGNED NULL,     -- receiving office for ISSUE rows
  remarks        VARCHAR(500) NULL,
  performed_by   INT UNSIGNED NOT NULL,
  created_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  KEY idx_mov_item_date (item_id, created_at),
  KEY idx_mov_ref (reference_type, reference_no),
  CONSTRAINT fk_mov_item   FOREIGN KEY (item_id)      REFERENCES supply_items (item_id),
  CONSTRAINT fk_mov_ris    FOREIGN KEY (ris_id)       REFERENCES ris_transactions (ris_id),
  CONSTRAINT fk_mov_office FOREIGN KEY (office_id)    REFERENCES offices (office_id),
  CONSTRAINT fk_mov_user   FOREIGN KEY (performed_by) REFERENCES users (user_id),
  CONSTRAINT chk_mov_qty   CHECK (quantity > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- 9. SUPPLY SUGGESTIONS (guests allowed → office/user nullable)
-- ---------------------------------------------------------------------
CREATE TABLE supply_suggestions (
  suggestion_id        INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  office_id            INT UNSIGNED NULL,
  user_id              INT UNSIGNED NULL,
  submitter_name       VARCHAR(150) NULL,
  submitter_email      VARCHAR(100) NULL,
  item_name            VARCHAR(200) NOT NULL,
  description          TEXT NULL,
  justification        TEXT NULL,
  estimated_annual_qty INT UNSIGNED NULL,
  status ENUM('New','UnderReview','AddedToCatalog','ForNextAPP','Declined') NOT NULL DEFAULT 'New',
  admin_response       VARCHAR(1000) NULL,
  reviewed_by          INT UNSIGNED NULL,
  reviewed_at          DATETIME NULL,
  ip_address           VARCHAR(45) NULL,
  created_at           DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  KEY idx_sugg_status (status, created_at),
  CONSTRAINT fk_sugg_office   FOREIGN KEY (office_id)   REFERENCES offices (office_id),
  CONSTRAINT fk_sugg_user     FOREIGN KEY (user_id)     REFERENCES users (user_id) ON DELETE SET NULL,
  CONSTRAINT fk_sugg_reviewer FOREIGN KEY (reviewed_by) REFERENCES users (user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- 10. READ VIEW for the allocation banner
--     "APP-CSE Total | Requested to Date | Remaining Balance"
-- ---------------------------------------------------------------------
CREATE OR REPLACE VIEW v_office_item_balance AS
SELECT a.office_id,
       a.item_id,
       a.fiscal_year,
       a.allocated_qty,
       a.issued_qty,
       COALESCE(p.pending_qty, 0)                               AS pending_qty,
       a.issued_qty + COALESCE(p.pending_qty, 0)                AS requested_to_date,
       CAST(a.allocated_qty AS SIGNED) - a.issued_qty
         - COALESCE(p.pending_qty, 0)                           AS remaining_qty
  FROM app_cse_allocations a
  LEFT JOIN (
        SELECT r.office_id, r.fiscal_year, ri.item_id, SUM(ri.requested_qty) AS pending_qty
          FROM ris_transactions r
          JOIN ris_items ri ON ri.ris_id = r.ris_id
         WHERE r.status = 'PendingApproval'
         GROUP BY r.office_id, r.fiscal_year, ri.item_id
       ) p
    ON p.office_id = a.office_id AND p.item_id = a.item_id AND p.fiscal_year = a.fiscal_year;

INSERT INTO schema_migrations (version) VALUES ('001_requisition_portal');
