-- -------------------------------------------------------------------
-- ams_stockwatch, 018: EQUIPMENT TYPE and DELIVERY DOCUMENTS
--   property_documents.equipment_type  what the PAR / ICS covers (Laptop, Printer and so on, several separated by ";")
--   property_items.equipment_type      the kind of equipment of each register item
--   supply_deliveries                  one row per delivery received in Supplies inventory, with the
--                                      scanned DR, Sales Invoice and IAR in one PDF
--   stock_movements.delivery_id        the delivery a RECEIPT line belongs to
-- Run after 017. I wrote it without information_schema so it runs in the MonsterASP phpMyAdmin.
-- If an ALTER says "#1060 Duplicate column name", that part is already done, so run the rest.
-- -------------------------------------------------------------------

ALTER TABLE property_documents ADD COLUMN equipment_type VARCHAR(255) NULL AFTER property_type_id;

ALTER TABLE property_items ADD COLUMN equipment_type VARCHAR(80) NULL AFTER classification;

CREATE TABLE IF NOT EXISTS supply_deliveries (
  delivery_id        INT UNSIGNED  NOT NULL AUTO_INCREMENT PRIMARY KEY,
  reference_no       VARCHAR(50)   NOT NULL,          -- IAR / DR no.
  received_on        DATE          NOT NULL,
  supplier           VARCHAR(200)  NULL,
  remarks            VARCHAR(500)  NULL,
  file_path          VARCHAR(255)  NULL,              -- saved PDF name (DR, Sales Invoice and IAR)
  original_file_name VARCHAR(255)  NULL,
  recorded_by        INT UNSIGNED  NOT NULL,
  recorded_at        DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
  KEY idx_deliv_date (received_on),
  KEY idx_deliv_ref (reference_no)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

ALTER TABLE stock_movements ADD COLUMN delivery_id INT UNSIGNED NULL AFTER ris_id, ADD KEY idx_mov_delivery (delivery_id);

-- Deliveries recorded before this update (no PDF yet, attach it in Reports > Deliveries)
INSERT INTO supply_deliveries (reference_no, received_on, remarks, recorded_by, recorded_at)
SELECT COALESCE(reference_no, '(no number)'), DATE(created_at), MAX(remarks), MIN(performed_by), MIN(created_at)
  FROM stock_movements
 WHERE movement_type = 'RECEIPT' AND delivery_id IS NULL
 GROUP BY COALESCE(reference_no, '(no number)'), DATE(created_at);

UPDATE stock_movements m
  JOIN supply_deliveries d ON d.reference_no = COALESCE(m.reference_no, '(no number)') AND d.received_on = DATE(m.created_at)
   SET m.delivery_id = d.delivery_id
 WHERE m.movement_type = 'RECEIPT' AND m.delivery_id IS NULL;

INSERT IGNORE INTO schema_migrations (version) VALUES ('018_equipment_and_deliveries');
