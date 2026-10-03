-- =====================================================================
-- ams_stockwatch :: 006 — item photos (uploaded in AMS StockWatch,
-- shown on the AMS Supplies catalog). Safe to run again.
-- On MonsterASP: run it inside db64557 (no USE line needed).
-- =====================================================================

CREATE TABLE IF NOT EXISTS item_images (
  item_id      INT UNSIGNED NOT NULL PRIMARY KEY,
  content_type VARCHAR(50)  NOT NULL,
  content      MEDIUMBLOB   NOT NULL,
  updated_at   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  CONSTRAINT fk_item_image FOREIGN KEY (item_id) REFERENCES supply_items (item_id) ON DELETE CASCADE,
  CONSTRAINT chk_item_image_type CHECK (content_type IN ('image/jpeg','image/png','image/webp'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Low-stock threshold used for new catalog items
INSERT IGNORE INTO system_settings (setting_key, setting_value) VALUES ('stock.default_reorder', '10');

INSERT IGNORE INTO schema_migrations (version) VALUES ('006_item_images');
