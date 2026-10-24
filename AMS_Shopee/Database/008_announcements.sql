-- -------------------------------------------------------------------
-- ams_stockwatch, 008: ANNOUNCEMENTS
-- Notices AMS posts in e-PINS. e-SRS shows them as a banner
-- between the start and end date. Safe to run again.
-- On MonsterASP run it inside db64557 (no USE line needed).
-- -------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS announcements (
  announcement_id INT UNSIGNED  NOT NULL AUTO_INCREMENT PRIMARY KEY,
  title           VARCHAR(150)  NOT NULL,
  message         VARCHAR(1000) NOT NULL,
  level           VARCHAR(10)   NOT NULL DEFAULT 'info',
  starts_at       DATETIME      NOT NULL,                -- Philippine time, set by the app
  ends_at         DATETIME      NULL,                    -- NULL means until it's turned off
  is_active       TINYINT(1)    NOT NULL DEFAULT 1,
  created_by      INT UNSIGNED  NULL,
  created_at      DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
  KEY idx_ann_live (is_active, starts_at, ends_at),
  CONSTRAINT fk_ann_user FOREIGN KEY (created_by) REFERENCES users (user_id) ON DELETE SET NULL,
  CONSTRAINT chk_ann_level CHECK (level IN ('info', 'warning', 'urgent')),
  CONSTRAINT chk_ann_dates CHECK (ends_at IS NULL OR ends_at > starts_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT IGNORE INTO schema_migrations (version) VALUES ('008_announcements');
